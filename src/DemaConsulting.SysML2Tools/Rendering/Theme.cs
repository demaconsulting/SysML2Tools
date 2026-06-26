// <copyright file="Theme.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// Visual theme for rendering a diagram.
/// </summary>
/// <remarks>
/// Font choice is not part of the theme; each renderer hardcodes its own typeface internally
/// to ensure consistent output across all platforms.
/// </remarks>
/// <param name="DepthFillColors">
/// Hex colour strings indexed by nesting depth. Wraps if depth exceeds count.
/// Example: <c>["#FFFFFF", "#EEF4FF", "#D6E8FF"]</c>.
/// </param>
/// <param name="StrokeColor">Hex colour used for all borders and lines.</param>
/// <param name="StrokeWidth">Width of borders and lines in logical pixels.</param>
/// <param name="LineCornerRadius">Corner radius for orthogonal-line elbows. 0 = sharp; &gt;0 = rounded.</param>
/// <param name="FontSizeTitle">Font size for title / heading text.</param>
/// <param name="FontSizeBody">Font size for body / row text.</param>
/// <param name="LabelPadding">Internal padding between text and its bounding box.</param>
public sealed record Theme(
    IReadOnlyList<string> DepthFillColors,
    string StrokeColor,
    double StrokeWidth,
    double LineCornerRadius,
    double FontSizeTitle,
    double FontSizeBody,
    double LabelPadding);

/// <summary>
/// Built-in themes for common rendering scenarios.
/// </summary>
public static class Themes
{
    /// <summary>
    /// Gets a light theme suitable for screen display.
    /// </summary>
    public static Theme Light { get; } = new(
        DepthFillColors: ["#FFFFFF", "#EEF4FF", "#D6E8FF", "#C0D8F8"],
        StrokeColor: "#1A1A2E",
        StrokeWidth: 1.5,
        LineCornerRadius: 4.0,
        FontSizeTitle: 14.0,
        FontSizeBody: 12.0,
        LabelPadding: 6.0);

    /// <summary>
    /// Gets a dark theme suitable for dark-mode screen display.
    /// </summary>
    public static Theme Dark { get; } = new(
        DepthFillColors: ["#1E1E2E", "#2A2A40", "#363650", "#424260"],
        StrokeColor: "#C0C0D8",
        StrokeWidth: 1.5,
        LineCornerRadius: 4.0,
        FontSizeTitle: 14.0,
        FontSizeBody: 12.0,
        LabelPadding: 6.0);

    /// <summary>
    /// Gets a print theme optimized for black-and-white output.
    /// </summary>
    public static Theme Print { get; } = new(
        DepthFillColors: ["#FFFFFF", "#F0F0F0", "#E0E0E0", "#D0D0D0"],
        StrokeColor: "#000000",
        StrokeWidth: 1.0,
        LineCornerRadius: 0.0,
        FontSizeTitle: 12.0,
        FontSizeBody: 10.0,
        LabelPadding: 4.0);
}
