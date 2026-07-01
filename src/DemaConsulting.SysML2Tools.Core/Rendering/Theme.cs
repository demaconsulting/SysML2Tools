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
/// <param name="ConnectorStub">Perpendicular step-off distance from a box face before a connector bends.</param>
/// <param name="BendRadius">
/// Corner bend radius reserved for a connector's approach zone by <see cref="ConnectorApproachZone"/>
/// (layout approach reservation). It is not read by any renderer; renderers round connector elbows
/// using <see cref="LineCornerRadius"/> instead.
/// </param>
/// <param name="CleanLegMargin">
/// Safety margin (in logical pixels) added to a decorated connector end's required clean straight
/// approach, beyond the end-marker length plus one corner radius, so the rounded corner never intrudes
/// into the end decoration.
/// </param>
public sealed record Theme(
    IReadOnlyList<string> DepthFillColors,
    string StrokeColor,
    double StrokeWidth,
    double LineCornerRadius,
    double FontSizeTitle,
    double FontSizeBody,
    double LabelPadding,
    double ConnectorStub,
    double BendRadius,
    double CleanLegMargin)
{
    /// <summary>
    /// Computes the connector approach zone: the clear distance a connector needs off a box face
    /// before it can bend, combining the perpendicular stub, the corner bend radius, and the
    /// caller-supplied connector clearance.
    /// </summary>
    /// <param name="connectorClearance">Clearance kept between routed connectors and part boxes.</param>
    /// <returns>The required approach-zone distance in logical pixels.</returns>
    public double ConnectorApproachZone(double connectorClearance) =>
        ConnectorStub + BendRadius + connectorClearance;

    /// <summary>
    /// Gets the canvas/base background fill color (the depth-0 fill). Used to occlude connector lines
    /// behind hollow (unfilled) enclosing end markers so the line does not show through the decoration.
    /// </summary>
    public string BackgroundColor => DepthFillColors[0];
}

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
        LabelPadding: 6.0,
        ConnectorStub: 8.0,
        BendRadius: 4.0,
        CleanLegMargin: 1.0);

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
        LabelPadding: 6.0,
        ConnectorStub: 8.0,
        BendRadius: 4.0,
        CleanLegMargin: 1.0);

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
        LabelPadding: 4.0,
        ConnectorStub: 6.0,
        BendRadius: 0.0,
        CleanLegMargin: 1.0);
}
