// <copyright file="LayoutWarnings.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;

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
}
