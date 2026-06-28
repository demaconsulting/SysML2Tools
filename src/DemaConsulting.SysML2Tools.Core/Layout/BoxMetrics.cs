// <copyright file="BoxMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Shared geometry helpers that compute box title-area and folder-tab heights from a
/// <see cref="Theme"/>. Both the layout strategies and the renderers use these formulas so
/// that reserved space and drawn space stay consistent.
/// </summary>
public static class BoxMetrics
{
    /// <summary>
    /// Computes the height of the folder tab drawn at the top-left of a
    /// <see cref="BoxShape.Folder"/> box.
    /// </summary>
    /// <param name="theme">Theme providing font and padding metrics.</param>
    /// <returns>The tab height in logical pixels.</returns>
    public static double FolderTabHeight(Theme theme) =>
        theme.FontSizeBody + 2.0 * theme.LabelPadding;

    /// <summary>
    /// Computes the height of the title area of a box: the vertical space reserved at the top
    /// for the optional keyword line and the bold name line.
    /// </summary>
    /// <param name="theme">Theme providing font and padding metrics.</param>
    /// <param name="hasLabel">Whether the box has a name label.</param>
    /// <param name="hasKeyword">Whether the box has a keyword line above the name.</param>
    /// <returns>The title-area height in logical pixels.</returns>
    public static double TitleAreaHeight(Theme theme, bool hasLabel, bool hasKeyword)
    {
        if (!hasLabel && !hasKeyword)
        {
            return 0.0;
        }

        var height = theme.LabelPadding;
        if (hasKeyword)
        {
            height += theme.FontSizeBody + theme.LabelPadding;
        }

        if (hasLabel)
        {
            height += theme.FontSizeTitle + theme.LabelPadding;
        }

        return height;
    }
}
