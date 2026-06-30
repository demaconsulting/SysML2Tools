// <copyright file="NotationMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// A single vertex of an end-marker decoration, expressed in tip-relative notation units (before the
/// renderer's uniform scale is applied).
/// </summary>
/// <remarks>
/// The coordinate frame is shared by both renderers so that the SVG marker and the PNG path describe
/// the identical shape: <see cref="Along"/> is the distance measured back from the line endpoint (the
/// tip) into the line, and <see cref="Across"/> is the perpendicular offset. A negative
/// <see cref="Along"/> places the vertex ahead of the endpoint (e.g. a triangle apex that overshoots
/// the tip by <see cref="NotationMetrics.EndMarkerTipOvershoot"/>).
/// </remarks>
/// <param name="Along">Distance back from the tip along the line, in notation units.</param>
/// <param name="Across">Perpendicular offset from the line, in notation units.</param>
public readonly record struct MarkerVertex(double Along, double Across);

/// <summary>
/// The single home for all intrinsic, theme-independent notation geometry shared by the SVG and PNG
/// renderers: end-marker (arrowhead) shapes and sizes, port squares, folder-tab proportions, note
/// dog-ear folds, rounded-rectangle corner scaling, badge fractions, and the label-background inset.
/// </summary>
/// <remarks>
/// This class is the notation-geometry peer of <see cref="DemaConsulting.SysML2Tools.Layout.BoxMetrics"/>:
/// every value is either a documented primitive notation constant or a documented derivation of those
/// primitives, so a geometry literal never appears more than once in the rendering path. The canonical
/// values are the historical SVG marker values (open/triangle 10x7 with refX 9, diamond 14x8 with
/// refX 13, circle 10x10 r4, bar 4x12); the PNG renderer derives its sizes from the same constants so
/// the two renderers draw the identical shape.
/// </remarks>
public static class NotationMetrics
{
    // ── End-marker primitives (triangle family: open chevron, hollow/filled triangle) ──────────

    /// <summary>Along-line length of a triangular end marker (chevron, hollow/filled triangle).</summary>
    public const double EndMarkerLength = 10.0;

    /// <summary>Across-line width (base span) of a triangular end marker.</summary>
    public const double EndMarkerWidth = 7.0;

    /// <summary>
    /// Along-line position, measured from the marker box origin, of the vertex that lands on the line
    /// endpoint (the SVG <c>refX</c>). The triangle apex sits one
    /// <see cref="EndMarkerTipOvershoot"/> beyond it.
    /// </summary>
    public const double EndMarkerRefX = 9.0;

    /// <summary>Half of <see cref="EndMarkerWidth"/>: the perpendicular offset of each base corner.</summary>
    public const double EndMarkerHalfWidth = EndMarkerWidth / 2.0;

    /// <summary>
    /// Distance the triangle apex overshoots the line endpoint, equal to the marker box length minus
    /// its tip anchor (<see cref="EndMarkerLength"/> - <see cref="EndMarkerRefX"/>).
    /// </summary>
    public const double EndMarkerTipOvershoot = EndMarkerLength - EndMarkerRefX;

    // ── Diamond primitives (hollow/filled diamond) ─────────────────────────────────────────────

    /// <summary>Along-line length of the diamond marker box.</summary>
    public const double DiamondLength = 14.0;

    /// <summary>Across-line width of the diamond marker box.</summary>
    public const double DiamondWidth = 8.0;

    /// <summary>
    /// Along-line position, from the diamond box origin, of the far point that lands on the line
    /// endpoint (the SVG <c>refX</c>).
    /// </summary>
    public const double DiamondRefX = 13.0;

    /// <summary>Half of <see cref="DiamondWidth"/>: the perpendicular offset of each side point.</summary>
    public const double DiamondHalfWidth = DiamondWidth / 2.0;

    /// <summary>Mid-length along-line position of the diamond side points (<see cref="DiamondLength"/> / 2).</summary>
    public const double DiamondMidX = DiamondLength / 2.0;

    /// <summary>
    /// Along-line position, from the diamond box origin, of the near point, mirrored from the far
    /// point about the mid-length (<see cref="DiamondLength"/> - <see cref="DiamondRefX"/>).
    /// </summary>
    public const double DiamondNearX = DiamondLength - DiamondRefX;

    // ── Circle primitives ──────────────────────────────────────────────────────────────────────

    /// <summary>Radius of the circle end marker.</summary>
    public const double CircleRadius = 4.0;

    /// <summary>
    /// Square box that contains the circle marker; equal to the triangular marker length so circle and
    /// triangle share one marker viewport.
    /// </summary>
    public const double CircleMarkerBox = EndMarkerLength;

    /// <summary>Centre of the circle marker box (half of <see cref="CircleMarkerBox"/>).</summary>
    public const double CircleCenter = CircleMarkerBox / 2.0;

    /// <summary>
    /// Along-line tip anchor of the circle marker: the far edge of the circle
    /// (<see cref="CircleCenter"/> + <see cref="CircleRadius"/>) sits on the line endpoint.
    /// </summary>
    public const double CircleRefX = CircleCenter + CircleRadius;

    // ── Bar primitives ─────────────────────────────────────────────────────────────────────────

    /// <summary>Along-line thickness of the bar end marker (marker box length).</summary>
    public const double BarAlong = 4.0;

    /// <summary>Across-line extent of the bar end marker (marker box height).</summary>
    public const double BarAcross = 12.0;

    /// <summary>Half of <see cref="BarAlong"/>: the bar's tip anchor along the line.</summary>
    public const double BarHalfAlong = BarAlong / 2.0;

    /// <summary>Half of <see cref="BarAcross"/>: the bar's perpendicular half-length.</summary>
    public const double BarHalf = BarAcross / 2.0;

    // ── Crossbar (hollow triangle with crossbar) ───────────────────────────────────────────────

    /// <summary>
    /// Fraction of <see cref="EndMarkerLength"/> from the base at which the redefinition crossbar
    /// crosses the triangle shaft.
    /// </summary>
    public const double CrossbarFraction = 0.7;

    /// <summary>Along-line position of the crossbar from the marker box origin (<see cref="CrossbarFraction"/> x <see cref="EndMarkerLength"/>).</summary>
    public const double CrossbarX = CrossbarFraction * EndMarkerLength;

    // ── Port square ────────────────────────────────────────────────────────────────────────────

    /// <summary>Half the side length of the square drawn for a port.</summary>
    public const double PortHalfSize = 4.0;

    /// <summary>Full side length of the square drawn for a port (2 x <see cref="PortHalfSize"/>).</summary>
    public const double PortSize = PortHalfSize * 2.0;

    // ── Folder tab ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Maximum folder-tab width as a fraction of the box width.</summary>
    public const double FolderTabMaxWidthFraction = 0.45;

    /// <summary>Minimum folder-tab width in logical pixels.</summary>
    public const double FolderTabMinWidth = 60.0;

    /// <summary>Approximate character-width factor (relative to body font size) used to size the folder-tab label.</summary>
    public const double FolderLabelCharWidthFactor = 0.55;

    // ── Note dog-ear fold ──────────────────────────────────────────────────────────────────────

    /// <summary>Note dog-ear fold size as a fraction of the box's shorter side.</summary>
    public const double NoteFoldFraction = 0.25;

    /// <summary>Maximum note dog-ear fold size in logical pixels.</summary>
    public const double NoteFoldMaxSize = 16.0;

    // ── Rounded rectangle ──────────────────────────────────────────────────────────────────────

    /// <summary>Factor applied to the theme corner radius for a rounded-rectangle box corner.</summary>
    public const double RoundedRectCornerFactor = 2.0;

    // ── Badge fractions ────────────────────────────────────────────────────────────────────────

    /// <summary>Inner (white) radius of a bullseye badge as a fraction of the badge radius.</summary>
    public const double BadgeBullseyeInnerFraction = 1.0 / 3.0;

    /// <summary>Bar-badge half-length as a fraction of the badge radius.</summary>
    public const double BadgeBarLengthFraction = 0.8;

    // ── Label background filter ────────────────────────────────────────────────────────────────

    /// <summary>Negative inset (each side) of the SVG label-background filter region.</summary>
    public const double LabelBgInset = 0.05;

    /// <summary>Total label-background filter extent (1 + 2 x <see cref="LabelBgInset"/>).</summary>
    public const double LabelBgExtent = 1.0 + (2.0 * LabelBgInset);

    /// <summary>
    /// Returns the rounded-rectangle corner radius for a box: the theme corner radius scaled by
    /// <see cref="RoundedRectCornerFactor"/>.
    /// </summary>
    /// <param name="theme">Theme providing the base line corner radius.</param>
    /// <returns>The rounded-rectangle corner radius in logical pixels.</returns>
    public static double RoundedRectRadius(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.LineCornerRadius * RoundedRectCornerFactor;
    }

    /// <summary>
    /// Returns the along-line length consumed by an end-marker decoration, used by the layout
    /// strategies (to reserve a clean approach) and the renderers (to clamp the final corner radius).
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <returns>The along-line length in notation units; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    public static double AlongLineLength(EndMarkerStyle style) => style switch
    {
        EndMarkerStyle.OpenChevron => EndMarkerLength,
        EndMarkerStyle.HollowTriangle => EndMarkerLength,
        EndMarkerStyle.HollowTriangleCrossbar => EndMarkerLength,
        EndMarkerStyle.FilledArrow => EndMarkerLength,
        EndMarkerStyle.HollowDiamond => DiamondLength,
        EndMarkerStyle.FilledDiamond => DiamondLength,
        EndMarkerStyle.Circle => CircleMarkerBox,
        EndMarkerStyle.Bar => BarAlong,
        _ => 0.0,
    };

    /// <summary>
    /// Returns the triangle vertices (base corner, apex, base corner) in tip-relative notation units,
    /// shared by the open chevron, hollow triangle, and filled arrow markers.
    /// </summary>
    /// <returns>The three triangle vertices in draw order.</returns>
    public static IReadOnlyList<MarkerVertex> TriangleVertices() =>
    [
        new MarkerVertex(EndMarkerRefX, -EndMarkerHalfWidth),
        new MarkerVertex(-EndMarkerTipOvershoot, 0.0),
        new MarkerVertex(EndMarkerRefX, EndMarkerHalfWidth),
    ];

    /// <summary>
    /// Returns the diamond vertices (near, side, far, side) in tip-relative notation units, shared by
    /// the hollow and filled diamond markers. The far point lands on the line endpoint.
    /// </summary>
    /// <returns>The four diamond vertices in draw order.</returns>
    public static IReadOnlyList<MarkerVertex> DiamondVertices() =>
    [
        new MarkerVertex(DiamondRefX - DiamondNearX, 0.0),
        new MarkerVertex(DiamondRefX - DiamondMidX, -DiamondHalfWidth),
        new MarkerVertex(0.0, 0.0),
        new MarkerVertex(DiamondRefX - DiamondMidX, DiamondHalfWidth),
    ];
}
