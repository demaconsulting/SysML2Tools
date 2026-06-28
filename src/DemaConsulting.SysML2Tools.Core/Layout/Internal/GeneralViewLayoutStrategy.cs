// <copyright file="GeneralViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for GeneralView diagrams. Renders every user-defined <c>def</c> element
/// (part, port, interface, requirement, action, …) as a keyword-labelled box, groups boxes by
/// their owning package inside folder-shaped containers, and routes specialization edges
/// orthogonally around the boxes.
/// </summary>
/// <remarks>
/// Box placement uses <see cref="ContainmentPacker"/> at two levels — definition boxes within a
/// package folder, and the folders themselves across the canvas. Specialization (generalization)
/// edges are routed with <see cref="ChannelRouter"/> so they avoid unrelated boxes. Standard-library
/// declarations are excluded via <see cref="StdlibFilter"/>.
/// </remarks>
internal sealed class GeneralViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a definition box in logical pixels.</summary>
    private const double MinBoxWidth = 130.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Clearance kept between routed edges and boxes.</summary>
    private const double EdgeClearance = 12.0;

    /// <summary>A feature membership: the keyword and the raw typing reference of one owned feature.</summary>
    private sealed record FeatureMembership(string Keyword, string TypeName);

    /// <summary>A user-defined definition together with its computed box size and supertypes.</summary>
    private sealed record DefBox(
        string QualifiedName,
        string SimpleName,
        string Keyword,
        IReadOnlyList<string> SupertypeNames,
        IReadOnlyList<FeatureMembership> Memberships,
        IReadOnlyList<LayoutCompartment> Compartments,
        double Width,
        double Height);

    /// <summary>A placed definition box with absolute coordinates, used for edge anchoring.</summary>
    private sealed record PlacedBox(string QualifiedName, string SimpleName, double X, double Y, double Width, double Height);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        // Collect all user-defined definitions, sized for rendering.
        var defs = CollectDefinitions(context.Workspace, theme);
        if (defs.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Group definitions by their owning package (prefix before the last "::").
        var groups = GroupByPackage(defs);

        // Place package folders and standalone definition boxes across the canvas.
        var hGap = 4.0 * theme.LabelPadding;
        var vGap = 5.0 * theme.LabelPadding;
        var (nodes, placed, canvasWidth, canvasHeight) = PlaceGroups(groups, theme, options.DepthLimit, hGap, vGap);

        // Route edges for the placement.
        var (specEdges, specCrossings) = BuildSpecializationEdges(defs, placed);
        var (memberEdges, memberCrossings) = BuildMembershipEdges(defs, placed);

        nodes.AddRange(specEdges);
        nodes.AddRange(memberEdges);

        var warnings = LayoutWarnings.ForCrossings(context.ViewName, specCrossings + memberCrossings);
        return new LayoutTree(canvasWidth, canvasHeight, nodes) { Warnings = warnings };
    }

    /// <summary>
    /// Collects every user-defined <see cref="SysmlDefinitionNode"/> from the workspace and computes
    /// each box's intrinsic size from its keyword and name.
    /// </summary>
    private static IReadOnlyList<DefBox> CollectDefinitions(SysmlWorkspace workspace, Theme theme)
    {
        var result = new List<DefBox>();

        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            if (declaration is not SysmlDefinitionNode def)
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            var simpleName = def.Name ?? qualifiedName;
            var keyword = string.IsNullOrEmpty(def.DefinitionKeyword) ? "def" : def.DefinitionKeyword;

            // Build compartments from the definition's owned usages (attributes, ports, parts, …).
            var compartments = BuildCompartments(def);

            var memberships = CollectMemberships(def);
            var (width, height) = ComputeBoxSize(simpleName, keyword, compartments, theme);
            result.Add(new DefBox(qualifiedName, simpleName, keyword, def.SupertypeNames, memberships, compartments, width, height));
        }

        return result;
    }

    /// <summary>
    /// Builds compartments for a definition by grouping its owned usage features by keyword and
    /// formatting each as a <c>name : Type [n]</c> row.
    /// </summary>
    private static IReadOnlyList<LayoutCompartment> BuildCompartments(SysmlDefinitionNode def)
    {
        // Preserve keyword first-seen order so compartments appear in declaration order.
        var order = new List<string>();
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var child in def.Children)
        {
            if (child is not SysmlFeatureNode feature)
            {
                continue;
            }

            var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
            if (!groups.TryGetValue(keyword, out var rows))
            {
                rows = [];
                groups[keyword] = rows;
                order.Add(keyword);
            }

            rows.Add(FormatFeatureRow(feature));
        }

        return [.. order.Select(k => new LayoutCompartment(Pluralize(k), groups[k]))];
    }

    /// <summary>Formats a usage feature as a compartment row: <c>name : Type [n]</c>.</summary>
    private static string FormatFeatureRow(SysmlFeatureNode feature)
    {
        var name = feature.Name ?? string.Empty;
        var typing = feature.FeatureTyping is { Length: > 0 } t ? $" : {t}" : string.Empty;
        var multiplicity = feature.Multiplicity is { Length: > 0 } m ? $" {m}" : string.Empty;
        var row = $"{name}{typing}{multiplicity}".Trim();
        return row.Length == 0 ? "\u2014" : row;
    }

    /// <summary>Returns a simple plural form of a usage keyword for use as a compartment title.</summary>
    private static string Pluralize(string keyword) => keyword switch
    {
        "ref" => "references",
        _ => keyword + "s",
    };

    /// <summary>
    /// Collects the feature memberships of a definition: the keyword and type reference of each
    /// owned feature that carries a type annotation.
    /// </summary>
    private static IReadOnlyList<FeatureMembership> CollectMemberships(SysmlDefinitionNode def)
    {
        var result = new List<FeatureMembership>();
        foreach (var child in def.Children)
        {
            if (child is not SysmlFeatureNode feature)
            {
                continue;
            }

            if (feature.FeatureTyping is { Length: > 0 } typing)
            {
                var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
                result.Add(new FeatureMembership(keyword, typing));
            }
        }

        return result;
    }

    /// <summary>Computes the intrinsic box size needed for the title and any compartments.</summary>
    private static (double Width, double Height) ComputeBoxSize(
        string name,
        string keyword,
        IReadOnlyList<LayoutCompartment> compartments,
        Theme theme)
    {
        var nameWidth = (name.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var keywordWidth = ((keyword.Length + 2) * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinBoxWidth, Math.Max(nameWidth, keywordWidth));

        // Widen to fit the longest compartment title or row.
        foreach (var compartment in compartments)
        {
            if (compartment.Title is { } title)
            {
                width = Math.Max(width, (title.Length * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding));
            }

            foreach (var row in compartment.Rows)
            {
                width = Math.Max(width, (row.Length * theme.FontSizeBody * CharWidthFactor) + (3.0 * theme.LabelPadding));
            }
        }

        // Title area holds the keyword line and the name line; add a little body breathing room.
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        foreach (var compartment in compartments)
        {
            height += ComputeCompartmentHeight(compartment, theme);
        }

        return (width, height);
    }

    /// <summary>
    /// Computes the rendered height of a compartment, matching the renderer's layout: an optional
    /// title row followed by one row per entry.
    /// </summary>
    private static double ComputeCompartmentHeight(LayoutCompartment compartment, Theme theme)
    {
        var height = 0.0;
        if (compartment.Title is not null)
        {
            height += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
        }

        height += compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody);

        // Bottom gap added by the renderer after the last row.
        height += theme.LabelPadding;
        return height;
    }

    /// <summary>
    /// Groups definitions by their parent package name (the qualified-name prefix before the last
    /// <c>::</c>), preserving first-seen order. Top-level definitions use an empty package key.
    /// </summary>
    private static IReadOnlyList<(string Package, List<DefBox> Items)> GroupByPackage(IReadOnlyList<DefBox> defs)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<DefBox>>(StringComparer.Ordinal);

        foreach (var def in defs)
        {
            var sep = def.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            var package = sep >= 0 ? def.QualifiedName[..sep] : string.Empty;

            if (!map.TryGetValue(package, out var list))
            {
                list = [];
                map[package] = list;
                order.Add(package);
            }

            list.Add(def);
        }

        return [.. order.Select(p => (p, map[p]))];
    }

    /// <summary>
    /// Places each package group as a folder box (with its definitions packed inside) and each
    /// top-level definition as a standalone box, packing all blocks across the canvas.
    /// </summary>
    private static (List<LayoutNode> Nodes, List<PlacedBox> Placed, double Width, double Height) PlaceGroups(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups,
        Theme theme,
        int depthLimit,
        double hGap,
        double vGap)
    {
        var margin = 2.0 * theme.LabelPadding;

        // Reserve the full title area (package keyword + name) above a folder's contents so the
        // label never overlaps the first child box. The renderer draws the smaller tab notch within.
        var folderTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        // Folder contents sit at depth 1; truncate them when the depth limit forbids that level.
        var truncateFolderContents = depthLimit > 0 && depthLimit <= 1;

        // Pre-compute the outer size of each top-level block (folder or standalone box).
        var blocks = new List<BlockPlan>();
        foreach (var (package, items) in groups)
        {
            if (string.IsNullOrEmpty(package))
            {
                // Top-level definitions are individual blocks (no folder).
                foreach (var def in items)
                {
                    blocks.Add(new BlockPlan(null, [def], def.Width, def.Height));
                }
            }
            else if (truncateFolderContents)
            {
                // Replace the folder's definition boxes with a single ellipsis indicator.
                var ellipsisWidth = Math.Max(MinBoxWidth, (2.0 * margin) + (items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length * 8.0) + 60.0);
                var ellipsisHeight = (2.0 * margin) + theme.FontSizeTitle;
                blocks.Add(new BlockPlan(package, items, ellipsisWidth, folderTitleHeight + ellipsisHeight) { Truncated = true });
            }
            else
            {
                // Pack the package's definitions to size the folder content region.
                var inner = ContainmentPacker.Pack(
                    [.. items.Select(d => new PackItem(d.Width, d.Height))],
                    maxContentWidth: ComputePackWidth(items),
                    horizontalGap: hGap,
                    verticalGap: vGap,
                    padding: margin);

                var folderWidth = inner.Width;
                var folderHeight = folderTitleHeight + inner.Height;
                blocks.Add(new BlockPlan(package, items, folderWidth, folderHeight) { Inner = inner });
            }
        }

        // Pack the blocks across the canvas.
        var outer = ContainmentPacker.Pack(
            [.. blocks.Select(b => new PackItem(b.Width, b.Height))],
            maxContentWidth: ComputeCanvasWidth(blocks),
            horizontalGap: hGap,
            verticalGap: vGap,
            padding: margin);

        var nodes = new List<LayoutNode>();
        var placed = new List<PlacedBox>();

        for (var i = 0; i < blocks.Count; i++)
        {
            PlaceBlock(blocks[i], outer.Rects[i], folderTitleHeight, theme, nodes, placed);
        }

        return (nodes, placed, outer.Width, outer.Height);
    }

    /// <summary>Emits the layout nodes for one placed block and records its definition boxes.</summary>
    private static void PlaceBlock(
        BlockPlan block,
        PackedRect rect,
        double folderTitleHeight,
        Theme theme,
        List<LayoutNode> nodes,
        List<PlacedBox> placed)
    {
        if (block.Package is null)
        {
            // Standalone top-level definition box.
            var def = block.Items[0];
            nodes.Add(MakeDefBox(def, rect.X, rect.Y, depth: 0));
            placed.Add(new PlacedBox(def.QualifiedName, def.SimpleName, rect.X, rect.Y, def.Width, def.Height));
            return;
        }

        var children = new List<LayoutNode>();

        if (block.Truncated)
        {
            // Show a visible truncation indicator instead of the hidden definition boxes.
            children.Add(new LayoutLabel(
                X: rect.X + theme.LabelPadding,
                Y: rect.Y + folderTitleHeight + theme.LabelPadding + (theme.FontSizeTitle / 2.0),
                MaxWidth: block.Width - (2.0 * theme.LabelPadding),
                Text: $"+{block.Items.Count} more\u2026",
                Align: TextAlign.Center,
                Weight: FontWeight.Regular,
                Style: FontStyle.Normal,
                FontSize: theme.FontSizeTitle));
        }
        else
        {
            // Folder containing packed definition boxes.
            var inner = block.Inner!;
            for (var k = 0; k < block.Items.Count; k++)
            {
                var def = block.Items[k];
                var childRect = inner.Rects[k];
                var absX = rect.X + childRect.X;
                var absY = rect.Y + folderTitleHeight + childRect.Y;
                children.Add(MakeDefBox(def, absX, absY, depth: 1));
                placed.Add(new PlacedBox(def.QualifiedName, def.SimpleName, absX, absY, def.Width, def.Height));
            }
        }

        nodes.Add(new LayoutBox(
            X: rect.X,
            Y: rect.Y,
            Width: block.Width,
            Height: block.Height,
            Label: SimplePackageName(block.Package),
            Depth: 0,
            Shape: BoxShape.Folder,
            Compartments: [],
            Children: children,
            Keyword: "package"));
    }

    /// <summary>Creates a definition <see cref="LayoutBox"/> at the given absolute position.</summary>
    private static LayoutBox MakeDefBox(DefBox def, double x, double y, int depth) =>
        new(
            X: x,
            Y: y,
            Width: def.Width,
            Height: def.Height,
            Label: def.SimpleName,
            Depth: depth,
            Shape: BoxShape.Rectangle,
            Compartments: def.Compartments,
            Children: [],
            Keyword: def.Keyword);

    /// <summary>
    /// Builds specialization (generalization) edges between placed definition boxes, returning the
    /// edges and the number that could not be routed without crossing a box.
    /// </summary>
    private static (List<LayoutNode> Edges, int Crossings) BuildSpecializationEdges(
        IReadOnlyList<DefBox> defs,
        IReadOnlyList<PlacedBox> placed)
    {
        var edges = new List<LayoutNode>();
        var crossings = 0;

        // Index placed boxes by both qualified and simple name for supertype resolution.
        var byQualified = new Dictionary<string, PlacedBox>(StringComparer.Ordinal);
        var bySimple = new Dictionary<string, PlacedBox>(StringComparer.Ordinal);
        foreach (var p in placed)
        {
            byQualified.TryAdd(p.QualifiedName, p);
            bySimple.TryAdd(p.SimpleName, p);
        }

        foreach (var def in defs)
        {
            if (!byQualified.TryGetValue(def.QualifiedName, out var fromBox))
            {
                continue;
            }

            foreach (var supertype in def.SupertypeNames)
            {
                if (!TryResolve(supertype, byQualified, bySimple, out var target) ||
                    target!.QualifiedName == def.QualifiedName)
                {
                    continue;
                }

                var (edge, crossed) = RouteEdge(fromBox, target, placed);
                edges.Add(edge);
                if (crossed)
                {
                    crossings++;
                }
            }
        }

        return (edges, crossings);
    }

    /// <summary>
    /// Builds feature-membership edges between placed definition boxes, returning the edges and
    /// the number that could not be routed without crossing a box.
    /// </summary>
    /// <remarks>
    /// Only structural-membership features with keyword <c>part</c> or <c>port</c> emit an edge.
    /// Each emitted edge carries a filled-diamond arrowhead at the owner end, routed from the
    /// member-type box toward the owner box so the diamond sits on the owner.
    /// </remarks>
    private static (List<LayoutNode> Edges, int Crossings) BuildMembershipEdges(
        IReadOnlyList<DefBox> defs,
        IReadOnlyList<PlacedBox> placed)
    {
        var edges = new List<LayoutNode>();
        var crossings = 0;

        // Index placed boxes by both qualified and simple name for type resolution.
        var byQualified = new Dictionary<string, PlacedBox>(StringComparer.Ordinal);
        var bySimple = new Dictionary<string, PlacedBox>(StringComparer.Ordinal);
        foreach (var p in placed)
        {
            byQualified.TryAdd(p.QualifiedName, p);
            bySimple.TryAdd(p.SimpleName, p);
        }

        foreach (var def in defs)
        {
            if (!byQualified.TryGetValue(def.QualifiedName, out var ownerBox))
            {
                continue;
            }

            foreach (var membership in def.Memberships)
            {
                // Structural keywords emit diamond edges; non-structural keywords (attribute, value,
                // item, etc.) are already shown as compartment text and do not need a separate arrow.
                var arrowhead = membership.Keyword switch
                {
                    "part" or "port" => ArrowheadStyle.FilledDiamond,
                    "ref" => ArrowheadStyle.Diamond,
                    _ => ArrowheadStyle.None,
                };

                if (arrowhead == ArrowheadStyle.None)
                {
                    continue;
                }

                if (!TryResolve(membership.TypeName, byQualified, bySimple, out var memberTypeBox) ||
                    memberTypeBox!.QualifiedName == def.QualifiedName)
                {
                    continue;
                }

                // Route from member-type box to owner box; diamond (TargetArrowhead) sits at the owner.
                var (edge, crossed) = RouteMembershipEdge(memberTypeBox, ownerBox, placed, arrowhead);
                edges.Add(edge);
                if (crossed)
                {
                    crossings++;
                }
            }
        }

        return (edges, crossings);
    }

    /// <summary>
    /// Routes a single membership edge from the member-type box to the owner box, placing the
    /// diamond arrowhead at the owner (target) end.
    /// </summary>
    private static (LayoutLine Edge, bool Crossed) RouteMembershipEdge(
        PlacedBox memberType,
        PlacedBox owner,
        IReadOnlyList<PlacedBox> placed,
        ArrowheadStyle diamond)
    {
        var memberCenter = new Point2D(memberType.X + (memberType.Width / 2.0), memberType.Y + (memberType.Height / 2.0));
        var ownerCenter = new Point2D(owner.X + (owner.Width / 2.0), owner.Y + (owner.Height / 2.0));

        var (source, sourceSide) = AnchorToward(memberType, ownerCenter);
        var (target, targetSide) = AnchorToward(owner, memberCenter);

        var obstacles = placed
            .Where(b => b.QualifiedName != memberType.QualifiedName && b.QualifiedName != owner.QualifiedName)
            .Select(b => new Rect(b.X, b.Y, b.Width, b.Height))
            .ToList();

        var route = ChannelRouter.RouteWithStatus(source, target, obstacles, EdgeClearance, sourceSide, targetSide);

        var edge = new LayoutLine(
            Waypoints: route.Waypoints,
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: diamond,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);
        return (edge, route.Crossed);
    }

    /// <summary>Resolves a supertype reference to a placed box by qualified then simple name.</summary>
    private static bool TryResolve(
        string reference,
        Dictionary<string, PlacedBox> byQualified,
        Dictionary<string, PlacedBox> bySimple,
        out PlacedBox? target)
    {
        if (byQualified.TryGetValue(reference, out var q))
        {
            target = q;
            return true;
        }

        // Fall back to the last segment of the reference matched against simple names.
        var sep = reference.LastIndexOf("::", StringComparison.Ordinal);
        var simple = sep >= 0 ? reference[(sep + 2)..] : reference;
        if (bySimple.TryGetValue(simple, out var s))
        {
            target = s;
            return true;
        }

        target = null;
        return false;
    }

    /// <summary>
    /// Routes a single specialization edge from a subtype box to its supertype box, returning the
    /// edge and whether it had to cross another box.
    /// </summary>
    private static (LayoutLine Edge, bool Crossed) RouteEdge(PlacedBox from, PlacedBox to, IReadOnlyList<PlacedBox> placed)
    {
        var fromCenter = new Point2D(from.X + (from.Width / 2.0), from.Y + (from.Height / 2.0));
        var toCenter = new Point2D(to.X + (to.Width / 2.0), to.Y + (to.Height / 2.0));

        var (source, sourceSide) = AnchorToward(from, toCenter);
        var (target, targetSide) = AnchorToward(to, fromCenter);

        // Obstacles are all boxes except the two endpoints of this edge.
        var obstacles = placed
            .Where(b => b.QualifiedName != from.QualifiedName && b.QualifiedName != to.QualifiedName)
            .Select(b => new Rect(b.X, b.Y, b.Width, b.Height))
            .ToList();

        var route = ChannelRouter.RouteWithStatus(source, target, obstacles, EdgeClearance, sourceSide, targetSide);

        // Generalization: open arrowhead points at the supertype (target) end.
        var edge = new LayoutLine(
            Waypoints: route.Waypoints,
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: ArrowheadStyle.Open,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);
        return (edge, route.Crossed);
    }

    /// <summary>
    /// Returns the midpoint of the box side whose outward normal best points at the target, along
    /// with that side.
    /// </summary>
    private static (Point2D Point, PortSide Side) AnchorToward(PlacedBox box, Point2D target)
    {
        var cx = box.X + (box.Width / 2.0);
        var cy = box.Y + (box.Height / 2.0);
        var dx = target.X - cx;
        var dy = target.Y - cy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            // Left or right side.
            return dx >= 0
                ? (new Point2D(box.X + box.Width, cy), PortSide.Right)
                : (new Point2D(box.X, cy), PortSide.Left);
        }

        // Top or bottom side.
        return dy >= 0
            ? (new Point2D(cx, box.Y + box.Height), PortSide.Bottom)
            : (new Point2D(cx, box.Y), PortSide.Top);
    }

    /// <summary>Computes the packing width used to lay out the definitions within a package folder.</summary>
    private static double ComputePackWidth(IReadOnlyList<DefBox> items)
    {
        // Target a roughly 4:3 region by packing to the square root of the total item area.
        var totalArea = items.Sum(d => (d.Width + 20.0) * (d.Height + 20.0));
        var maxItemWidth = items.Max(d => d.Width);
        var target = Math.Sqrt(totalArea) * 1.15;
        return Math.Max(maxItemWidth, target);
    }

    /// <summary>Computes the packing width used to lay out top-level blocks across the canvas.</summary>
    private static double ComputeCanvasWidth(IReadOnlyList<BlockPlan> blocks)
    {
        // Target a roughly 4:3 canvas by packing to the square root of the total block area.
        var totalArea = blocks.Sum(b => (b.Width + 30.0) * (b.Height + 30.0));
        var maxBlockWidth = blocks.Max(b => b.Width);
        var target = Math.Sqrt(totalArea) * 1.25;
        return Math.Max(maxBlockWidth, target);
    }

    /// <summary>Returns the last segment of a qualified package name for use as a folder label.</summary>
    private static string SimplePackageName(string package)
    {
        var sep = package.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? package[(sep + 2)..] : package;
    }

    /// <summary>Internal plan for one top-level block (a folder or a standalone definition box).</summary>
    private sealed record BlockPlan(string? Package, List<DefBox> Items, double Width, double Height)
    {
        /// <summary>Packed inner layout of the folder's definition boxes, when this block is a folder.</summary>
        public PackResult? Inner { get; init; }

        /// <summary>When true, the folder's contents are replaced by an ellipsis truncation indicator.</summary>
        public bool Truncated { get; init; }
    }
}
