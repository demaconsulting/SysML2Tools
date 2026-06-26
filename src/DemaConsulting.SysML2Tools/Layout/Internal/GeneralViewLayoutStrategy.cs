// <copyright file="GeneralViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for GeneralView diagrams that renders all user-defined <c>part def</c>
/// elements grouped by their parent package in a two-column grid.
/// </summary>
/// <remarks>
/// Standard-library declarations are filtered out using <see cref="StdlibFilter"/>. Only
/// <see cref="SysmlDefinitionNode"/> instances with
/// <see cref="SysmlDefinitionNode.DefinitionKeyword"/> equal to <c>"part def"</c> are laid out.
/// When no user-defined part defs are found, a minimal canvas
/// <c>LayoutTree(200.0, 100.0, [])</c> is returned.
/// </remarks>
internal sealed class GeneralViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Layout margin around the entire diagram canvas.</summary>
    private const double Margin = 20.0;

    /// <summary>Horizontal gap between the two layout columns.</summary>
    private const double ColumnGap = 30.0;

    /// <summary>Vertical gap between group rows.</summary>
    private const double RowGap = 20.0;

    /// <summary>Minimum box width in logical pixels.</summary>
    private const double MinBoxWidth = 120.0;

    /// <summary>Minimum box height in logical pixels.</summary>
    private const double MinBoxHeight = 40.0;

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        // Collect all user-defined part defs from the workspace
        var userPartDefs = CollectUserPartDefs(context.Workspace);

        // Return minimal canvas when no user part defs are present
        if (userPartDefs.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Group part defs by parent package (prefix before the last "::")
        var groups = GroupByPackage(userPartDefs);

        // Lay out groups in a two-column grid and collect all top-level nodes
        return BuildGridLayout(groups, options.Theme);
    }

    /// <summary>
    /// Collects all user-defined <c>part def</c> declarations from the workspace,
    /// filtering out standard-library elements.
    /// </summary>
    /// <param name="workspace">The workspace whose declarations are scanned.</param>
    /// <returns>
    /// A list of (qualifiedName, node) pairs for every user-defined part def.
    /// </returns>
    private static IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> CollectUserPartDefs(
        SysmlWorkspace workspace)
    {
        var result = new List<(string, SysmlDefinitionNode)>();

        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            // Skip non-definition nodes and non-part-def definitions
            if (declaration is not SysmlDefinitionNode def ||
                def.DefinitionKeyword != "part def")
            {
                continue;
            }

            // Skip stdlib elements identified by their qualified-name prefix
            if (StdlibFilter.IsStdlibElement(qualifiedName))
            {
                continue;
            }

            result.Add((qualifiedName, def));
        }

        return result;
    }

    /// <summary>
    /// Groups part-def entries by their parent package.
    /// </summary>
    /// <param name="partDefs">User-defined part defs to group.</param>
    /// <returns>
    /// An ordered list of (packageName, items) groups, where <c>packageName</c> is
    /// the prefix before the last <c>::</c> separator, or <c>""</c> for top-level defs.
    /// </returns>
    private static IReadOnlyList<(string PackageName, IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> Items)>
        GroupByPackage(IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> partDefs)
    {
        // Use ordered dictionary to preserve insertion order of groups
        var groups = new Dictionary<string, List<(string, SysmlDefinitionNode)>>(StringComparer.Ordinal);

        foreach (var (qualifiedName, node) in partDefs)
        {
            // Extract parent package name from the qualified name
            var lastSeparator = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            var packageName = lastSeparator >= 0
                ? qualifiedName[..lastSeparator]
                : string.Empty;

            if (!groups.TryGetValue(packageName, out var group))
            {
                group = [];
                groups[packageName] = group;
            }

            group.Add((qualifiedName, node));
        }

        return groups
            .Select(kvp => (kvp.Key, (IReadOnlyList<(string, SysmlDefinitionNode)>)kvp.Value))
            .ToList();
    }

    /// <summary>
    /// Builds a two-column grid layout from the grouped part defs.
    /// </summary>
    /// <param name="groups">Part-def groups ordered by package name.</param>
    /// <param name="theme">Visual theme providing size and color parameters.</param>
    /// <returns>A fully resolved <see cref="LayoutTree"/> with all box positions computed.</returns>
    private static LayoutTree BuildGridLayout(
        IReadOnlyList<(string PackageName, IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> Items)> groups,
        Theme theme)
    {
        // Compute the width required for each group box
        var groupWidths = groups.Select(g => ComputeGroupWidth(g.PackageName, g.Items, theme)).ToList();
        var groupHeights = groups.Select(g => ComputeGroupHeight(g.Items, theme)).ToList();

        // Determine column widths from the maximum group width in each column
        var col0Width = 0.0;
        var col1Width = 0.0;
        for (var i = 0; i < groups.Count; i++)
        {
            if (i % 2 == 0)
            {
                col0Width = Math.Max(col0Width, groupWidths[i]);
            }
            else
            {
                col1Width = Math.Max(col1Width, groupWidths[i]);
            }
        }

        // Suppress unused variable warning when col1Width is never consumed in grid positioning
        _ = col1Width;

        // Position each group box in the grid
        var nodes = new List<LayoutNode>();
        var cursorX1 = Margin + col0Width + ColumnGap;
        var cursorY0 = Margin;
        var cursorY1 = Margin;
        var maxX = Margin;
        var maxY = Margin;

        for (var i = 0; i < groups.Count; i++)
        {
            var (packageName, items) = groups[i];
            var gw = groupWidths[i];
            var gh = groupHeights[i];

            double boxX;
            double boxY;
            if (i % 2 == 0)
            {
                // Left column
                boxX = Margin;
                boxY = cursorY0;
                cursorY0 += gh + RowGap;
            }
            else
            {
                // Right column
                boxX = cursorX1;
                boxY = cursorY1;
                cursorY1 += gh + RowGap;
            }

            // Build child part-def boxes
            var children = BuildChildBoxes(items, boxX, boxY, gw, theme);
            var label = string.IsNullOrEmpty(packageName) ? null : packageName;
            var groupBox = new LayoutBox(boxX, boxY, gw, gh, label, 0, BoxShape.Rectangle, [], children);
            nodes.Add(groupBox);

            maxX = Math.Max(maxX, boxX + gw);
            maxY = Math.Max(maxY, boxY + gh);
        }

        // Add specialization lines for part defs with supertypes
        AddSpecializationLines(groups, nodes);

        var canvasWidth = maxX + Margin;
        var canvasHeight = maxY + Margin;
        return new LayoutTree(canvasWidth, canvasHeight, nodes);
    }

    /// <summary>
    /// Computes the minimum width of a group box based on its label and child part defs.
    /// </summary>
    /// <param name="packageName">Parent package label text.</param>
    /// <param name="items">Part-def items in the group.</param>
    /// <param name="theme">Visual theme for font measurements.</param>
    /// <returns>Minimum required width in logical pixels.</returns>
    private static double ComputeGroupWidth(
        string packageName,
        IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> items,
        Theme theme)
    {
        // Start with the package label width
        var labelWidth = packageName.Length * theme.FontSizeTitle * 0.6 + 2 * theme.LabelPadding;
        var maxWidth = Math.Max(MinBoxWidth, labelWidth);

        // Expand to fit each child label
        foreach (var (qualifiedName, node) in items)
        {
            var childLabel = node.Name ?? qualifiedName;
            var childWidth = childLabel.Length * theme.FontSizeTitle * 0.6 + 4 * theme.LabelPadding;
            maxWidth = Math.Max(maxWidth, Math.Max(MinBoxWidth, childWidth));
        }

        return maxWidth + 2 * theme.LabelPadding;
    }

    /// <summary>
    /// Computes the total height of a group box based on its title area and child box heights.
    /// </summary>
    /// <param name="items">Part-def items in the group.</param>
    /// <param name="theme">Visual theme for font measurements.</param>
    /// <returns>Minimum required height in logical pixels.</returns>
    private static double ComputeGroupHeight(
        IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> items,
        Theme theme)
    {
        // Title area height plus padding
        var titleHeight = theme.FontSizeTitle + 2 * theme.LabelPadding;

        // Sum child box heights with vertical spacing
        var childrenHeight = items.Count * MinBoxHeight + (items.Count + 1) * theme.LabelPadding;

        return titleHeight + childrenHeight;
    }

    /// <summary>
    /// Builds the child <see cref="LayoutBox"/> nodes for part defs within a group.
    /// </summary>
    /// <param name="items">Part-def items to lay out.</param>
    /// <param name="groupX">Left edge X of the parent group box.</param>
    /// <param name="groupY">Top edge Y of the parent group box.</param>
    /// <param name="groupWidth">Width of the parent group box.</param>
    /// <param name="theme">Visual theme for font and size measurements.</param>
    /// <returns>List of child <see cref="LayoutBox"/> nodes with absolute coordinates.</returns>
    private static IReadOnlyList<LayoutNode> BuildChildBoxes(
        IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> items,
        double groupX,
        double groupY,
        double groupWidth,
        Theme theme)
    {
        var children = new List<LayoutNode>();

        // Start below the group title area
        var titleHeight = theme.FontSizeTitle + 2 * theme.LabelPadding;
        var childX = groupX + theme.LabelPadding;
        var childWidth = groupWidth - 2 * theme.LabelPadding;
        var cursorY = groupY + titleHeight + theme.LabelPadding;

        foreach (var (qualifiedName, node) in items)
        {
            var label = node.Name ?? qualifiedName;
            var childBox = new LayoutBox(
                X: childX,
                Y: cursorY,
                Width: childWidth,
                Height: MinBoxHeight,
                Label: label,
                Depth: 1,
                Shape: BoxShape.Rectangle,
                Compartments: [],
                Children: []);
            children.Add(childBox);
            cursorY += MinBoxHeight + theme.LabelPadding;
        }

        return children;
    }

    /// <summary>
    /// Adds <see cref="LayoutLine"/> nodes for specialization relationships between part defs
    /// that declare supertypes.
    /// </summary>
    /// <param name="groups">All part-def groups used to resolve supertype positions.</param>
    /// <param name="nodes">Top-level node list to which lines are appended.</param>
    /// <remarks>
    /// Lines use <see cref="ArrowheadStyle.None"/> at the source (subtype) end and
    /// <see cref="ArrowheadStyle.Open"/> at the target (supertype) end, following the
    /// SysML convention that the open arrowhead points toward the general type.
    /// Only supertypes resolvable within the same set of user part defs are connected;
    /// missing supertypes produce no line.
    /// </remarks>
    private static void AddSpecializationLines(
        IReadOnlyList<(string PackageName, IReadOnlyList<(string QualifiedName, SysmlDefinitionNode Node)> Items)> groups,
        List<LayoutNode> nodes)
    {
        // Build a lookup table from qualifiedName -> LayoutBox position
        var boxPositions = BuildBoxPositionLookup(nodes);

        foreach (var (_, items) in groups)
        {
            foreach (var (qualifiedName, node) in items)
            {
                foreach (var supertypeName in node.SupertypeNames)
                {
                    // Only draw lines to supertypes that are in our layout
                    if (!boxPositions.TryGetValue(qualifiedName, out var fromBox) ||
                        !boxPositions.TryGetValue(supertypeName, out var toBox))
                    {
                        continue;
                    }

                    // Create a simple 3-segment orthogonal line from subtype bottom to supertype bottom
                    var fromX = fromBox.X + fromBox.Width / 2.0;
                    var fromY = fromBox.Y + fromBox.Height;
                    var toX = toBox.X + toBox.Width / 2.0;
                    var toY = toBox.Y + toBox.Height;
                    var midY = (fromY + toY) / 2.0;

                    var waypoints = new List<Point2D>
                    {
                        new(fromX, fromY),
                        new(fromX, midY),
                        new(toX, midY),
                        new(toX, toY)
                    };

                    nodes.Add(new LayoutLine(
                        Waypoints: waypoints,
                        SourceArrowhead: ArrowheadStyle.None,
                        TargetArrowhead: ArrowheadStyle.Open,
                        LineStyle: LineStyle.Solid,
                        MidpointLabel: null));
                }
            }
        }
    }

    /// <summary>
    /// Builds a flat lookup dictionary from qualified name to <see cref="LayoutBox"/> by
    /// walking the tree of top-level nodes recursively.
    /// </summary>
    /// <param name="nodes">Top-level nodes to search.</param>
    /// <returns>A dictionary mapping qualified name to the corresponding <see cref="LayoutBox"/>.</returns>
    private static Dictionary<string, LayoutBox> BuildBoxPositionLookup(IReadOnlyList<LayoutNode> nodes)
    {
        var result = new Dictionary<string, LayoutBox>(StringComparer.Ordinal);
        CollectBoxes(nodes, result);
        return result;
    }

    /// <summary>
    /// Recursively collects labeled <see cref="LayoutBox"/> nodes and their positions.
    /// </summary>
    /// <param name="nodes">Nodes to walk.</param>
    /// <param name="lookup">Dictionary to populate.</param>
    private static void CollectBoxes(IEnumerable<LayoutNode> nodes, Dictionary<string, LayoutBox> lookup)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutBox box)
            {
                // Record by label (which is the simple name for child part-def boxes)
                if (box.Label != null)
                {
                    lookup.TryAdd(box.Label, box);
                }

                // Recurse into children
                CollectBoxes(box.Children, lookup);
            }
        }
    }
}
