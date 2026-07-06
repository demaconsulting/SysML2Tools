// <copyright file="LayoutWarnings.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;

using DemaConsulting.Rendering;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Builds the non-fatal layout-quality warning messages surfaced on a <see cref="LayoutTree"/>.
/// </summary>
internal static class LayoutWarnings
{
    /// <summary>
    /// Returns a single-element warning list describing connectors that could not be routed without
    /// crossing a box, or an empty list when <paramref name="crossings"/> is zero.
    /// </summary>
    /// <param name="viewName">Name of the view being laid out.</param>
    /// <param name="crossings">Number of connectors that had to cross a box.</param>
    /// <returns>The warning messages for the view.</returns>
    public static IReadOnlyList<string> ForCrossings(string viewName, int crossings)
    {
        if (crossings <= 0)
        {
            return [];
        }

        var plural = crossings == 1 ? "connector" : "connectors";
        var count = crossings.ToString(CultureInfo.InvariantCulture);
        return
        [
            $"{count} {plural} in '{viewName}' could not be routed without crossing a box; " +
            "the diagram may be cluttered.",
        ];
    }

    /// <summary>
    /// Returns a single-element warning list stating that a view's <c>filter [&lt;expr&gt;];</c>
    /// statement was parsed but not evaluated, or an empty list when the view declares no filter
    /// expression.
    /// </summary>
    /// <param name="viewName">Name of the view being laid out.</param>
    /// <param name="filterExpressionText">
    /// The view's raw filter expression source text, or <see langword="null"/> when the view
    /// declares no <c>filter</c> member.
    /// </param>
    /// <returns>The warning messages for the view.</returns>
    public static IReadOnlyList<string> ForUnevaluatedFilter(string viewName, string? filterExpressionText)
    {
        if (filterExpressionText is null)
        {
            return [];
        }

        return
        [
            $"View '{viewName}' declares a filter expression, which is parsed but not yet " +
            "evaluated; all elements in the resolved scope are rendered unfiltered.",
        ];
    }
}
