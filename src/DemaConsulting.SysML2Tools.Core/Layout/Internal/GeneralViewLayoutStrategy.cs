// <copyright file="GeneralViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;
using DemaConsulting.SysML2Tools.Layout.Engine.Layered;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for GeneralView diagrams. Renders every user-defined <c>def</c> element
/// (part, port, interface, requirement, action, …) as a keyword-labelled box, groups boxes by
/// their owning package inside folder-shaped containers, and routes specialization and membership
/// edges orthogonally between the boxes.
/// </summary>
/// <remarks>
/// Box placement and intra-package edge routing use the reusable layered pipeline
/// (<see cref="LayeredLayoutPipeline"/>) running left-to-right with a <see cref="ComponentPacker"/>
/// stage: each package's definitions and the edges between them are laid out together inside the
/// package folder, with disconnected definitions (such as standalone interface or attribute defs)
/// packed beside the connected core. The folders themselves are packed across the canvas with
/// <see cref="ContainmentPacker"/> so they never overlap. The rare cross-package edge (an endpoint
/// in a different package folder) falls back to <see cref="ChannelRouter"/>, which routes orthogonally
/// around the placed folders. Standard-library declarations are excluded via <see cref="StdlibFilter"/>.
/// </remarks>
internal sealed class GeneralViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a definition box in logical pixels.</summary>
    private const double MinBoxWidth = 130.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Clearance kept between cross-package routed edges and boxes.</summary>
    private const double EdgeClearance = 12.0;

    /// <summary>A feature membership: the keyword and the raw typing reference of one owned feature.</summary>
    private sealed record FeatureMembership(string Keyword, string TypeName);

    /// <summary>An intra-package edge expressed in package-local node indices, plus its target decoration.</summary>
    /// <param name="SourceLocal">Index of the source definition within its package group.</param>
    /// <param name="TargetLocal">Index of the target definition within its package group.</param>
    /// <param name="Arrowhead">Arrowhead drawn at the target (supertype or owner) end.</param>
    private sealed record IntraEdge(int SourceLocal, int TargetLocal, ArrowheadStyle Arrowhead);

    /// <summary>A cross-package edge between two definitions in different package groups.</summary>
    /// <param name="SourceQualified">Qualified name of the source definition.</param>
    /// <param name="TargetQualified">Qualified name of the target (supertype or owner) definition.</param>
    /// <param name="Arrowhead">Arrowhead drawn at the target end.</param>
    private sealed record CrossEdge(string SourceQualified, string TargetQualified, ArrowheadStyle Arrowhead);

    /// <summary>
    /// The package-local placement of one group: each definition's top-left relative to the group's
    /// content origin, the content size, and the routed intra-group edge polylines (also content-local).
    /// </summary>
    /// <param name="LocalPos">Content-local top-left of each definition box, by group index.</param>
    /// <param name="ContentWidth">Width of the group's content bounding box.</param>
    /// <param name="ContentHeight">Height of the group's content bounding box.</param>
    /// <param name="Edges">Routed intra-group edges (content-local polyline + target arrowhead).</param>
    private sealed record GroupLayout(
        IReadOnlyList<(double X, double Y)> LocalPos,
        double ContentWidth,
        double ContentHeight,
        IReadOnlyList<(IReadOnlyList<Point2D> Points, ArrowheadStyle Arrowhead)> Edges);

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

        // Resolve the specialization/membership edge set into intra-package edges (handled by the
        // layered pipeline inside each folder) and cross-package edges (routed around the folders).
        var (intraByGroup, crossEdges) = BuildEdges(groups);

        // Place package folders (and the top-level frameless block) across the canvas. Each folder's
        // definitions and intra-package edges are laid out together by the layered pipeline.
        var hGap = 4.0 * theme.LabelPadding;
        var vGap = 5.0 * theme.LabelPadding;
        var (nodes, placed, intraLines, canvasWidth, canvasHeight) =
            PlaceGroups(groups, intraByGroup, theme, options.DepthLimit, hGap, vGap);

        // Emit the intra-package edges (already routed by the pipeline) and the cross-package edges.
        nodes.AddRange(intraLines);
        nodes.AddRange(RouteCrossEdges(crossEdges, placed));

        return new LayoutTree(canvasWidth, canvasHeight, nodes);
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
    /// Lays out each package group (with the layered pipeline) inside a folder box, emits the
    /// top-level definitions as a single frameless block, and packs all blocks across the canvas so
    /// folders never overlap. Returns the emitted nodes, the absolute box placements (for
    /// cross-package routing), the routed intra-package edge lines, and the canvas size.
    /// </summary>
    private static (List<LayoutNode> Nodes, List<PlacedBox> Placed, List<LayoutLine> IntraEdges, double Width, double Height) PlaceGroups(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups,
        IReadOnlyList<IReadOnlyList<IntraEdge>> intraByGroup,
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

        // Pre-compute the outer size of each top-level block (package folder or the frameless block of
        // top-level definitions). Folders reserve a title area; the frameless block does not.
        var blocks = new List<BlockPlan>();
        for (var g = 0; g < groups.Count; g++)
        {
            var (package, items) = groups[g];
            var isFolder = !string.IsNullOrEmpty(package);

            if (isFolder && truncateFolderContents)
            {
                // Replace the folder's definition boxes with a single ellipsis indicator.
                var ellipsisWidth = Math.Max(MinBoxWidth, (2.0 * margin) + (items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length * 8.0) + 60.0);
                var ellipsisHeight = (2.0 * margin) + theme.FontSizeTitle;
                blocks.Add(new BlockPlan(package, items, ellipsisWidth, folderTitleHeight + ellipsisHeight) { Truncated = true });
                continue;
            }

            // Lay out the group's definitions and intra-group edges with the layered pipeline.
            var layout = LayoutGroup(items, intraByGroup[g]);
            var titleArea = isFolder ? folderTitleHeight : 0.0;
            var blockWidth = layout.ContentWidth + (2.0 * margin);
            var blockHeight = titleArea + layout.ContentHeight + (2.0 * margin);
            blocks.Add(new BlockPlan(package, items, blockWidth, blockHeight) { Layout = layout });
        }

        // Pack the blocks across the canvas (atomic blocks → folders never overlap).
        var outer = ContainmentPacker.Pack(
            [.. blocks.Select(b => new PackItem(b.Width, b.Height))],
            maxContentWidth: ComputeCanvasWidth(blocks),
            horizontalGap: hGap,
            verticalGap: vGap,
            padding: margin);

        var nodes = new List<LayoutNode>();
        var placed = new List<PlacedBox>();
        var intraEdges = new List<LayoutLine>();

        for (var i = 0; i < blocks.Count; i++)
        {
            PlaceBlock(blocks[i], outer.Rects[i], folderTitleHeight, margin, theme, nodes, placed, intraEdges);
        }

        return (nodes, placed, intraEdges, outer.Width, outer.Height);
    }

    /// <summary>
    /// Lays out one package group with the layered pipeline plus a <see cref="ComponentPacker"/> stage,
    /// then reads back each definition's package-local top-left and each intra-group edge's polyline,
    /// normalized against the group's content bounding box.
    /// </summary>
    /// <param name="items">The group's definitions, in group order.</param>
    /// <param name="intraEdges">The intra-group edges in package-local node indices.</param>
    /// <returns>The package-local placement of the group's definitions and routed edges.</returns>
    private static GroupLayout LayoutGroup(IReadOnlyList<DefBox> items, IReadOnlyList<IntraEdge> intraEdges)
    {
        var layerNodes = items.Select(d => new LayerNode(d.Width, d.Height)).ToList();
        var layerEdges = intraEdges.Select(e => new LayerEdge(e.SourceLocal, e.TargetLocal)).ToList();

        // Run the layered pipeline left-to-right; ComponentPacker packs disconnected definitions
        // (e.g. standalone interface/attribute defs) beside the connected core within the folder.
        var graph = new LayeredGraph(layerNodes, layerEdges, LayoutDirection.Right);
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(LayoutDirection.Right)
            .Hierarchy(HierarchyHandling.Flat)
            .AddStage(ComponentPacker.WithDefaultStages())
            .Build();
        pipeline.Run(graph);

        // Compute the content bounding box over the real definition nodes (indices [0, items.Count)).
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        for (var i = 0; i < items.Count; i++)
        {
            minX = Math.Min(minX, graph.AugX[i]);
            minY = Math.Min(minY, graph.AugY[i]);
            maxX = Math.Max(maxX, graph.AugX[i] + items[i].Width);
            maxY = Math.Max(maxY, graph.AugY[i] + items[i].Height);
        }

        // Normalize node positions so the content bounding box starts at the local origin (0, 0).
        var localPos = new (double X, double Y)[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            localPos[i] = (graph.AugX[i] - minX, graph.AugY[i] - minY);
        }

        // Translate each edge polyline into the same content-local frame, paired with its arrowhead.
        var edges = new List<(IReadOnlyList<Point2D> Points, ArrowheadStyle Arrowhead)>(intraEdges.Count);
        for (var k = 0; k < intraEdges.Count; k++)
        {
            var points = graph.Waypoints[k].Select(p => new Point2D(p.X - minX, p.Y - minY)).ToList();
            edges.Add((points, intraEdges[k].Arrowhead));
        }

        return new GroupLayout(localPos, maxX - minX, maxY - minY, edges);
    }

    /// <summary>
    /// Emits the layout nodes for one placed block: a package folder with its child definition boxes,
    /// the frameless top-level definitions, or a truncated folder with an ellipsis indicator. Records
    /// each rendered definition's absolute placement and the absolute intra-group edge lines.
    /// </summary>
    private static void PlaceBlock(
        BlockPlan block,
        PackedRect rect,
        double folderTitleHeight,
        double margin,
        Theme theme,
        List<LayoutNode> nodes,
        List<PlacedBox> placed,
        List<LayoutLine> intraEdges)
    {
        var isFolder = !string.IsNullOrEmpty(block.Package);

        if (block.Truncated)
        {
            // Show a visible truncation indicator instead of the hidden definition boxes.
            var indicator = new List<LayoutNode>
            {
                new LayoutLabel(
                    X: rect.X + theme.LabelPadding,
                    Y: rect.Y + folderTitleHeight + theme.LabelPadding + (theme.FontSizeTitle / 2.0),
                    MaxWidth: block.Width - (2.0 * theme.LabelPadding),
                    Text: $"+{block.Items.Count} more\u2026",
                    Align: TextAlign.Center,
                    Weight: FontWeight.Regular,
                    Style: FontStyle.Normal,
                    FontSize: theme.FontSizeTitle),
            };

            nodes.Add(MakeFolderBox(block, rect, indicator));
            return;
        }

        var layout = block.Layout!;
        var titleArea = isFolder ? folderTitleHeight : 0.0;
        var contentOriginX = rect.X + margin;
        var contentOriginY = rect.Y + titleArea + margin;

        // Place the definition boxes. Folder children sit at depth 1 inside the folder; the frameless
        // top-level block emits its definitions directly at depth 0.
        var children = new List<LayoutNode>();
        for (var k = 0; k < block.Items.Count; k++)
        {
            var def = block.Items[k];
            var absX = contentOriginX + layout.LocalPos[k].X;
            var absY = contentOriginY + layout.LocalPos[k].Y;
            var box = MakeDefBox(def, absX, absY, depth: isFolder ? 1 : 0);
            if (isFolder)
            {
                children.Add(box);
            }
            else
            {
                nodes.Add(box);
            }

            placed.Add(new PlacedBox(def.QualifiedName, def.SimpleName, absX, absY, def.Width, def.Height));
        }

        if (isFolder)
        {
            nodes.Add(MakeFolderBox(block, rect, children));
        }

        // Offset the pipeline-routed intra-group edges into absolute canvas coordinates.
        foreach (var (points, arrowhead) in layout.Edges)
        {
            var absPoints = points.Select(p => new Point2D(contentOriginX + p.X, contentOriginY + p.Y)).ToList();
            intraEdges.Add(new LayoutLine(
                Waypoints: absPoints,
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: arrowhead,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }
    }

    /// <summary>Creates the folder <see cref="LayoutBox"/> for a package block with the given children.</summary>
    private static LayoutBox MakeFolderBox(BlockPlan block, PackedRect rect, List<LayoutNode> children) =>
        new(
            X: rect.X,
            Y: rect.Y,
            Width: block.Width,
            Height: block.Height,
            Label: SimplePackageName(block.Package),
            Depth: 0,
            Shape: BoxShape.Folder,
            Compartments: [],
            Children: children,
            Keyword: "package");

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
    /// Resolves the specialization and membership edge set into intra-package edges (both endpoints in
    /// the same package group, laid out together by the layered pipeline) and cross-package edges (both
    /// endpoints resolved but in different package groups, routed around the placed folders).
    /// </summary>
    /// <param name="groups">The definitions grouped by owning package.</param>
    /// <returns>One intra-edge list per group (parallel to <paramref name="groups"/>) and the cross-edge list.</returns>
    private static (IReadOnlyList<List<IntraEdge>> Intra, List<CrossEdge> Cross) BuildEdges(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups)
    {
        // Index every definition by qualified and simple name, mapping to its (group, local) position.
        var locByQualified = new Dictionary<string, (int Group, int Local)>(StringComparer.Ordinal);
        var locBySimple = new Dictionary<string, (int Group, int Local)>(StringComparer.Ordinal);
        for (var g = 0; g < groups.Count; g++)
        {
            var items = groups[g].Items;
            for (var l = 0; l < items.Count; l++)
            {
                locByQualified.TryAdd(items[l].QualifiedName, (g, l));
                locBySimple.TryAdd(items[l].SimpleName, (g, l));
            }
        }

        var intra = groups.Select(_ => new List<IntraEdge>()).ToList();
        var cross = new List<CrossEdge>();

        for (var g = 0; g < groups.Count; g++)
        {
            var items = groups[g].Items;
            for (var l = 0; l < items.Count; l++)
            {
                var def = items[l];

                // Specialization: subtype → supertype, open arrowhead at the supertype (target) end.
                foreach (var supertype in def.SupertypeNames)
                {
                    if (TryResolveLoc(supertype, locByQualified, locBySimple, out var target) &&
                        !(target.Group == g && target.Local == l))
                    {
                        if (target.Group == g)
                        {
                            intra[g].Add(new IntraEdge(l, target.Local, ArrowheadStyle.Open));
                        }
                        else
                        {
                            cross.Add(new CrossEdge(def.QualifiedName, groups[target.Group].Items[target.Local].QualifiedName, ArrowheadStyle.Open));
                        }
                    }
                }

                // Membership: member-type → owner, diamond at the owner (target) end. Structural keywords
                // (part/port) use a filled diamond; reference (ref) uses a hollow diamond; others emit none.
                foreach (var membership in def.Memberships)
                {
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

                    if (TryResolveLoc(membership.TypeName, locByQualified, locBySimple, out var memberType) &&
                        !(memberType.Group == g && memberType.Local == l))
                    {
                        if (memberType.Group == g)
                        {
                            // Source = member-type local, target = owner local (the diamond sits on the owner).
                            intra[g].Add(new IntraEdge(memberType.Local, l, arrowhead));
                        }
                        else
                        {
                            cross.Add(new CrossEdge(groups[memberType.Group].Items[memberType.Local].QualifiedName, def.QualifiedName, arrowhead));
                        }
                    }
                }
            }
        }

        return (intra, cross);
    }

    /// <summary>Resolves a supertype/type reference to a definition's (group, local) position by qualified then simple name.</summary>
    private static bool TryResolveLoc(
        string reference,
        Dictionary<string, (int Group, int Local)> byQualified,
        Dictionary<string, (int Group, int Local)> bySimple,
        out (int Group, int Local) location)
    {
        if (byQualified.TryGetValue(reference, out location))
        {
            return true;
        }

        var sep = reference.LastIndexOf("::", StringComparison.Ordinal);
        var simple = sep >= 0 ? reference[(sep + 2)..] : reference;
        return bySimple.TryGetValue(simple, out location);
    }

    /// <summary>
    /// Routes the cross-package edges around the placed folders with <see cref="ChannelRouter"/>.
    /// Cross-package edges are rare in practice (most General-view models are single-package); each is
    /// drawn cost-neutrally between the two folders, with the recorded arrowhead at the target end.
    /// </summary>
    /// <param name="crossEdges">The cross-package edges to route.</param>
    /// <param name="placed">The absolute placements of every rendered definition box.</param>
    /// <returns>One routed <see cref="LayoutLine"/> per cross-package edge whose endpoints are both placed.</returns>
    private static List<LayoutLine> RouteCrossEdges(IReadOnlyList<CrossEdge> crossEdges, IReadOnlyList<PlacedBox> placed)
    {
        var lines = new List<LayoutLine>();
        if (crossEdges.Count == 0)
        {
            return lines;
        }

        // Index placed boxes by qualified name so cross edges resolve to absolute box geometry. Edges
        // touching a truncated (unrendered) definition are skipped because the box is absent.
        var byQualified = new Dictionary<string, PlacedBox>(StringComparer.Ordinal);
        foreach (var p in placed)
        {
            byQualified.TryAdd(p.QualifiedName, p);
        }

        foreach (var edge in crossEdges)
        {
            if (!byQualified.TryGetValue(edge.SourceQualified, out var fromBox) ||
                !byQualified.TryGetValue(edge.TargetQualified, out var toBox))
            {
                continue;
            }

            lines.Add(RouteCrossEdge(fromBox, toBox, placed, edge.Arrowhead));
        }

        return lines;
    }

    /// <summary>
    /// Routes a single cross-package edge from the source box to the target box, placing the supplied
    /// arrowhead at the target (supertype or owner) end.
    /// </summary>
    private static LayoutLine RouteCrossEdge(PlacedBox from, PlacedBox to, IReadOnlyList<PlacedBox> placed, ArrowheadStyle targetArrowhead)
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

        return new LayoutLine(
            Waypoints: route.Waypoints,
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: targetArrowhead,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);
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

    /// <summary>Internal plan for one top-level block (a package folder or the frameless top-level block).</summary>
    /// <param name="Package">Owning package name; empty for the frameless top-level block.</param>
    /// <param name="Items">The definitions placed within this block, in group order.</param>
    /// <param name="Width">Outer width of the block in logical pixels.</param>
    /// <param name="Height">Outer height of the block in logical pixels.</param>
    private sealed record BlockPlan(string Package, IReadOnlyList<DefBox> Items, double Width, double Height)
    {
        /// <summary>Package-local placement of the block's definitions and routed edges; null when truncated.</summary>
        public GroupLayout? Layout { get; init; }

        /// <summary>When true, the folder's contents are replaced by an ellipsis truncation indicator.</summary>
        public bool Truncated { get; init; }
    }
}
