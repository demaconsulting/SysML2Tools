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

    /// <summary>A user-defined definition together with its computed box size and supertypes.</summary>
    private sealed record DefBox(
        string QualifiedName,
        string SimpleName,
        string Keyword,
        IReadOnlyList<string> SupertypeNames,
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

        // Place groups (folders) and standalone definitions across the canvas.
        var (nodes, placed, canvasWidth, canvasHeight) = PlaceGroups(groups, theme, options.DepthLimit);

        // Route specialization edges between placed boxes.
        var edges = BuildSpecializationEdges(defs, placed);
        nodes.AddRange(edges);

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

            var (width, height) = ComputeBoxSize(simpleName, keyword, theme);
            result.Add(new DefBox(qualifiedName, simpleName, keyword, def.SupertypeNames, width, height));
        }

        return result;
    }

    /// <summary>Computes the intrinsic box size needed to show a keyword line and a name line.</summary>
    private static (double Width, double Height) ComputeBoxSize(string name, string keyword, Theme theme)
    {
        var nameWidth = (name.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var keywordWidth = ((keyword.Length + 2) * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinBoxWidth, Math.Max(nameWidth, keywordWidth));

        // Title area holds the keyword line and the name line; add a little body breathing room.
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;

        return (width, height);
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
        int depthLimit)
    {
        var margin = 2.0 * theme.LabelPadding;
        var hGap = 3.0 * theme.LabelPadding;
        var vGap = 2.0 * theme.LabelPadding;

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
            Compartments: [],
            Children: [],
            Keyword: def.Keyword);

    /// <summary>Builds specialization (generalization) edges between placed definition boxes.</summary>
    private static List<LayoutNode> BuildSpecializationEdges(IReadOnlyList<DefBox> defs, IReadOnlyList<PlacedBox> placed)
    {
        var edges = new List<LayoutNode>();

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

                edges.Add(RouteEdge(fromBox, target, placed));
            }
        }

        return edges;
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

    /// <summary>Routes a single specialization edge from a subtype box to its supertype box.</summary>
    private static LayoutLine RouteEdge(PlacedBox from, PlacedBox to, IReadOnlyList<PlacedBox> placed)
    {
        var fromCenter = new Point2D(from.X + (from.Width / 2.0), from.Y + (from.Height / 2.0));
        var toCenter = new Point2D(to.X + (to.Width / 2.0), to.Y + (to.Height / 2.0));

        var source = AnchorToward(from, toCenter);
        var target = AnchorToward(to, fromCenter);

        // Obstacles are all boxes except the two endpoints of this edge.
        var obstacles = placed
            .Where(b => b.QualifiedName != from.QualifiedName && b.QualifiedName != to.QualifiedName)
            .Select(b => new Rect(b.X, b.Y, b.Width, b.Height))
            .ToList();

        var waypoints = ChannelRouter.Route(source, target, obstacles, EdgeClearance);

        // Generalization: open arrowhead points at the supertype (target) end.
        return new LayoutLine(
            Waypoints: waypoints,
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: ArrowheadStyle.Open,
            LineStyle: LineStyle.Solid,
            MidpointLabel: null);
    }

    /// <summary>Returns the midpoint of the box side whose outward normal best points at the target.</summary>
    private static Point2D AnchorToward(PlacedBox box, Point2D target)
    {
        var cx = box.X + (box.Width / 2.0);
        var cy = box.Y + (box.Height / 2.0);
        var dx = target.X - cx;
        var dy = target.Y - cy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            // Left or right side.
            return dx >= 0
                ? new Point2D(box.X + box.Width, cy)
                : new Point2D(box.X, cy);
        }

        // Top or bottom side.
        return dy >= 0
            ? new Point2D(cx, box.Y + box.Height)
            : new Point2D(cx, box.Y);
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
