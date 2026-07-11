// <copyright file="GridViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

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

        var defs = CollectDefinitions(context.Workspace, ExposeScopeResolver.ResolveExposedScope(context.Workspace, context.ViewNode));
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
    private sealed record DefRow(string QualifiedName, string Name, IReadOnlyList<string> SupertypeNames);

    /// <summary>
    /// Collects the non-stdlib definitions of the workspace in deterministic order, restricted to
    /// <paramref name="scope"/> when non-null (the view's resolved <c>expose</c> containment
    /// subtrees).
    /// </summary>
    /// <remarks>
    /// A definition is kept when it is directly within <paramref name="scope"/> <em>or</em> it
    /// participates in a specialization relationship with another definition that is in scope
    /// (i.e. it is a supertype of an in-scope definition, or an in-scope definition is one of its
    /// own supertypes). This "at least one dimension in scope" rule keeps both sides of a
    /// specialization relationship visible in the matrix even when only one side was directly
    /// exposed, so the relationship mark is never rendered against a missing row or column.
    /// </remarks>
    private static IReadOnlyList<DefRow> CollectDefinitions(SysmlWorkspace workspace, ExposedScope? scope)
    {
        // Phase 1: collect every non-stdlib definition, unfiltered, building a full simple-name index.
        var all = new List<DefRow>();
        foreach (var qn in workspace.Declarations.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (StdlibFilter.IsStdlibElement(qn, workspace.StdlibNames))
            {
                continue;
            }

            if (workspace.Declarations[qn] is SysmlDefinitionNode def)
            {
                all.Add(new DefRow(qn, def.Name ?? qn, def.SupertypeNames));
            }
        }

        // No expose scope: everything is kept (fast path, byte-identical to prior behavior).
        if (scope is null)
        {
            return all;
        }

        var fullIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < all.Count; i++)
        {
            fullIndexByName.TryAdd(all[i].Name, i);
        }

        // Phase 2: determine which indices are directly in the resolved scope.
        var inScope = new HashSet<int>();
        for (var i = 0; i < all.Count; i++)
        {
            if (ExposeScopeResolver.IsInSubjectScope(all[i].QualifiedName, scope))
            {
                inScope.Add(i);
            }
        }

        // Phase 3: build the specialization adjacency (each definition's resolved supertype indices).
        var adjacency = new List<HashSet<int>>(all.Count);
        foreach (var def in all)
        {
            adjacency.Add(ResolveSupertypeIndices(def, fullIndexByName));
        }

        // Phase 4: keep in-scope definitions, plus any definition sharing a specialization
        // relationship with an in-scope definition (as either the general or specific side).
        var kept = new HashSet<int>(inScope);
        for (var j = 0; j < all.Count; j++)
        {
            if (!inScope.Contains(j))
            {
                continue;
            }

            // j is in scope: its supertypes (general side) are kept too.
            foreach (var i in adjacency[j])
            {
                kept.Add(i);
            }
        }

        for (var i = 0; i < all.Count; i++)
        {
            // i's own supertypes intersect the in-scope set: i (the specific side) is kept too.
            if (adjacency[i].Overlaps(inScope))
            {
                kept.Add(i);
            }
        }

        // Phase 5: return the kept definitions in the original deterministic order.
        var result = new List<DefRow>(kept.Count);
        for (var i = 0; i < all.Count; i++)
        {
            if (kept.Contains(i))
            {
                result.Add(all[i]);
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
