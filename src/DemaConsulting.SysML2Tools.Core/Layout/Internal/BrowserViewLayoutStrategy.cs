// <copyright file="BrowserViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Browser View diagrams. Presents the membership hierarchy of the workspace's
/// user-defined elements as an indented tree of rows, with connector lines from each parent to its
/// children.
/// </summary>
/// <remarks>
/// The tree is derived from the qualified-name hierarchy: an element <c>A::B::C</c> is a child of
/// <c>A::B</c>. Each row is a small box indented by its depth; layout is pure arithmetic.
/// </remarks>
internal sealed class BrowserViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Horizontal indentation per depth level.</summary>
    private const double Indent = 28.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>A node in the membership tree.</summary>
    private sealed record TreeNode(string QualifiedName, string Label, string? Keyword, List<TreeNode> Children);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        var roots = BuildForest(context.Workspace);
        if (roots.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var nodes = new List<LayoutNode>();
        var cursorY = theme.LabelPadding * 2.0;
        var maxRight = 0.0;
        var rowHeight = theme.FontSizeTitle + (2.0 * theme.LabelPadding);

        foreach (var root in roots)
        {
            EmitNode(root, depth: 0, theme, rowHeight, nodes, ref cursorY, ref maxRight, parentCentreY: null, parentX: 0);
        }

        var width = maxRight + (theme.LabelPadding * 2.0);
        var height = cursorY + theme.LabelPadding;
        return new LayoutTree(width, height, nodes);
    }

    /// <summary>
    /// Builds the membership forest from the non-stdlib declarations using their qualified-name
    /// nesting (parent = prefix before the last <c>::</c>).
    /// </summary>
    private static IReadOnlyList<TreeNode> BuildForest(SysmlWorkspace workspace)
    {
        var byName = new Dictionary<string, TreeNode>(StringComparer.Ordinal);
        var roots = new List<TreeNode>();

        // Deterministic order: sort qualified names so parents precede children.
        var names = workspace.Declarations.Keys
            .Where(qn => !StdlibFilter.IsStdlibElement(qn, workspace.StdlibNames))
            .OrderBy(qn => qn, StringComparer.Ordinal)
            .ToList();

        foreach (var qn in names)
        {
            var node = workspace.Declarations[qn];
            var label = LastSegment(qn);
            var keyword = KeywordOf(node);
            var tree = new TreeNode(qn, label, keyword, []);
            byName[qn] = tree;

            var sep = qn.LastIndexOf("::", StringComparison.Ordinal);
            if (sep >= 0 && byName.TryGetValue(qn[..sep], out var parent))
            {
                parent.Children.Add(tree);
            }
            else
            {
                roots.Add(tree);
            }
        }

        return roots;
    }

    /// <summary>Recursively emits a tree node row and its descendants, advancing the Y cursor.</summary>
    private static void EmitNode(
        TreeNode node,
        int depth,
        Theme theme,
        double rowHeight,
        List<LayoutNode> nodes,
        ref double cursorY,
        ref double maxRight,
        double? parentCentreY,
        double parentX)
    {
        var x = (theme.LabelPadding * 2.0) + (depth * Indent);
        var y = cursorY;
        var label = node.Keyword is { Length: > 0 } k ? $"{k} {node.Label}" : node.Label;
        var boxWidth = (label.Length * theme.FontSizeBody * CharWidthFactor) + (4.0 * theme.LabelPadding);
        var centreY = y + (rowHeight / 2.0);

        // Connector line from the parent's bottom-left stem down to this row, then across to the box.
        if (parentCentreY is { } pcy)
        {
            nodes.Add(new LayoutLine(
                Waypoints: [new Point2D(parentX, pcy), new Point2D(parentX, centreY), new Point2D(x, centreY)],
                SourceEnd: EndMarkerStyle.None,
                TargetEnd: EndMarkerStyle.None,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }

        nodes.Add(new LayoutBox(
            X: x,
            Y: y,
            Width: boxWidth,
            Height: rowHeight,
            Label: label,
            Depth: Math.Min(depth, 3),
            Shape: BoxShape.Rectangle,
            Compartments: [],
            Children: []));

        maxRight = Math.Max(maxRight, x + boxWidth);
        cursorY += rowHeight + (theme.LabelPadding / 2.0);

        // Children hang from a vertical stem that drops from this row's bottom-left, so it never
        // crosses over this node's own box or text.
        var stemX = x + (Indent / 2.0);
        var stemTopY = y + rowHeight;
        foreach (var child in node.Children)
        {
            EmitNode(child, depth + 1, theme, rowHeight, nodes, ref cursorY, ref maxRight, stemTopY, stemX);
        }
    }

    /// <summary>Returns a short keyword for a declaration node, or null when none applies.</summary>
    private static string? KeywordOf(SysmlNode node) => node switch
    {
        SysmlPackageNode => "package",
        SysmlDefinitionNode def => string.IsNullOrEmpty(def.DefinitionKeyword) ? "def" : def.DefinitionKeyword,
        SysmlFeatureNode feature => string.IsNullOrEmpty(feature.FeatureKeyword) ? null : feature.FeatureKeyword,
        SysmlViewNode => "view def",
        _ => null,
    };

    /// <summary>Returns the last <c>::</c>-separated segment of a qualified name.</summary>
    private static string LastSegment(string qualifiedName)
    {
        var sep = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? qualifiedName[(sep + 2)..] : qualifiedName;
    }
}
