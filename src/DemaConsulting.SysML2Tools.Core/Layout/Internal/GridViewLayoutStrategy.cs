// <copyright file="GridViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Grid View diagrams. Presents the workspace's user-defined definitions as a
/// specialization relationship matrix: rows and columns are the definitions, and a cell is marked
/// where the row definition specializes the column definition.
/// </summary>
/// <remarks>
/// Layout is pure arithmetic via <see cref="LayoutGrid"/>: column widths fit the widest cell and a
/// header row/column are styled distinctly.
/// </remarks>
internal sealed class GridViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Mark placed in a cell where the row specializes the column.</summary>
    private const string Mark = "X";

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        var defs = CollectDefinitions(context.Workspace);
        if (defs.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Resolve each definition's supertypes to column indices by simple name.
        var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < defs.Count; i++)
        {
            indexByName.TryAdd(defs[i].Name, i);
        }

        var rowHeight = theme.FontSizeBody + (2.0 * theme.LabelPadding);
        var headerWidth = MaxLabelWidth(defs.Select(d => d.Name), theme.FontSizeBody) + (2.0 * theme.LabelPadding);
        var dataWidth = Math.Max(rowHeight, MaxLabelWidth(defs.Select(d => d.Name), theme.FontSizeBody) + (2.0 * theme.LabelPadding));

        var rows = new List<LayoutGridRow>();

        // Header row: empty corner cell then each definition name as a column header.
        var headerCells = new List<LayoutGridCell> { new(headerWidth, rowHeight, string.Empty, TextAlign.Center, 1) };
        foreach (var def in defs)
        {
            headerCells.Add(new LayoutGridCell(dataWidth, rowHeight, def.Name, TextAlign.Center, 1));
        }

        rows.Add(new LayoutGridRow(IsHeader: true, headerCells));

        // Data rows: header column with the row definition, then a mark where it specializes the column.
        foreach (var rowDef in defs)
        {
            var cells = new List<LayoutGridCell> { new(headerWidth, rowHeight, rowDef.Name, TextAlign.Left, 1) };
            var supertypeIndices = ResolveSupertypeIndices(rowDef, indexByName);
            for (var col = 0; col < defs.Count; col++)
            {
                var text = supertypeIndices.Contains(col) ? Mark : string.Empty;
                cells.Add(new LayoutGridCell(dataWidth, rowHeight, text, TextAlign.Center, 1));
            }

            rows.Add(new LayoutGridRow(IsHeader: false, cells));
        }

        var grid = new LayoutGrid(theme.LabelPadding * 2.0, theme.LabelPadding * 2.0, rows);

        var width = (theme.LabelPadding * 4.0) + headerWidth + (defs.Count * dataWidth);
        var height = (theme.LabelPadding * 4.0) + ((defs.Count + 1) * rowHeight);
        return new LayoutTree(width, height, [grid]);
    }

    /// <summary>A user-defined definition with its supertype references.</summary>
    private sealed record DefRow(string Name, IReadOnlyList<string> SupertypeNames);

    /// <summary>Collects the non-stdlib definitions of the workspace in deterministic order.</summary>
    private static IReadOnlyList<DefRow> CollectDefinitions(SysmlWorkspace workspace)
    {
        var result = new List<DefRow>();
        foreach (var qn in workspace.Declarations.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (StdlibFilter.IsStdlibElement(qn, workspace.StdlibNames))
            {
                continue;
            }

            if (workspace.Declarations[qn] is SysmlDefinitionNode def)
            {
                result.Add(new DefRow(def.Name ?? qn, def.SupertypeNames));
            }
        }

        return result;
    }

    /// <summary>Resolves a definition's supertype references to column indices by simple name.</summary>
    private static HashSet<int> ResolveSupertypeIndices(DefRow def, Dictionary<string, int> indexByName)
    {
        var result = new HashSet<int>();
        foreach (var supertype in def.SupertypeNames)
        {
            var sep = supertype.LastIndexOf("::", StringComparison.Ordinal);
            var simple = sep >= 0 ? supertype[(sep + 2)..] : supertype;
            if (indexByName.TryGetValue(simple, out var i))
            {
                result.Add(i);
            }
        }

        return result;
    }

    /// <summary>Computes the maximum rendered width of a set of labels at the given font size.</summary>
    private static double MaxLabelWidth(IEnumerable<string> labels, double fontSize)
    {
        var max = 0.0;
        foreach (var label in labels)
        {
            max = Math.Max(max, label.Length * fontSize * CharWidthFactor);
        }

        return Math.Max(40.0, max);
    }
}
