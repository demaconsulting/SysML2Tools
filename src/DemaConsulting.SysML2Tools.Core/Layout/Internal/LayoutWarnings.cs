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
    /// statement could not be evaluated (a parse error or an unsupported Phase 1 construct), or an
    /// empty list when the view declares no filter expression.
    /// </summary>
    /// <param name="viewName">Name of the view being laid out.</param>
    /// <param name="filterExpressionText">
    /// The view's raw filter expression source text, or <see langword="null"/> when the view
    /// declares no <c>filter</c> member.
    /// </param>
    /// <param name="reason">
    /// A short human-readable explanation of why evaluation could not proceed (e.g. the first
    /// parse/evaluation diagnostic message), or <see langword="null"/> to omit the reason clause.
    /// </param>
    /// <returns>The warning messages for the view.</returns>
    public static IReadOnlyList<string> ForUnevaluatedFilter(
        string viewName, string? filterExpressionText, string? reason = null)
    {
        if (filterExpressionText is null)
        {
            return [];
        }

        var suffix = reason is { Length: > 0 } ? $" ({reason})" : string.Empty;
        return
        [
            $"View '{viewName}' declares a filter expression that could not be evaluated{suffix}; " +
            "all elements in the resolved scope are rendered unfiltered.",
        ];
    }

    /// <summary>
    /// Returns a single-element warning list stating that a view's <c>expose &lt;path&gt;::**[&lt;expr&gt;]</c>
    /// bracket-filter expression(s) were parsed but not yet evaluated (Phase 1 captures raw text
    /// only — see <c>SysmlViewNode.ExposeBracketFilterTexts</c>), or an empty list when the view
    /// declares no bracket-filter expose members.
    /// </summary>
    /// <param name="viewName">Name of the view being laid out.</param>
    /// <param name="bracketFilterTexts">The view's raw bracket-filter expression source texts.</param>
    /// <returns>The warning messages for the view.</returns>
    public static IReadOnlyList<string> ForUnevaluatedExposeBracketFilter(
        string viewName, IReadOnlyList<string> bracketFilterTexts)
    {
        if (bracketFilterTexts.Count == 0)
        {
            return [];
        }

        var plural = bracketFilterTexts.Count == 1 ? "expression" : "expressions";
        var verb = bracketFilterTexts.Count == 1 ? "is" : "are";
        return
        [
            $"View '{viewName}' declares {bracketFilterTexts.Count} expose bracket-filter {plural} " +
            $"('::**[...]'), which {verb} parsed but not yet evaluated; the bracket filter has no " +
            "effect on the rendered scope.",
        ];
    }
}
