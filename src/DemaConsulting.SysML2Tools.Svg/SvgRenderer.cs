// <copyright file="SvgRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Svg;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to SVG format using zero external dependencies.
/// </summary>
/// <remarks>
/// The renderer is pure and stateless: each call to <see cref="Render"/> builds a complete
/// SVG document from the supplied <see cref="LayoutTree"/> and writes it to the output stream.
/// No state is shared between calls. The produced SVG targets SVG 1.1 and is encoded in UTF-8.
///
/// Node rendering:
/// - <see cref="LayoutBox"/> → <c>&lt;rect&gt;</c> (with <c>rx</c>/<c>ry</c> for rounded
///   corners) + optional <c>&lt;text&gt;</c> for the label + <c>&lt;line&gt;</c> dividers
///   and <c>&lt;text&gt;</c> rows for each compartment; children rendered recursively.
/// - <see cref="LayoutLine"/> → <c>&lt;path&gt;</c> with M/L/A commands for corner-radius-
///   aware bends; arrowheads as marker references defined in <c>&lt;defs&gt;</c>; optional
///   midpoint label as <c>&lt;text&gt;</c>.
/// - <see cref="LayoutLabel"/> → <c>&lt;text&gt;</c>.
/// - <see cref="LayoutPort"/> → <c>&lt;rect&gt;</c> filled square with optional
///   <c>&lt;text&gt;</c> label offset away from the attached edge.
/// - <see cref="LayoutBadge"/> → shape-specific SVG elements centered at the badge position
///   with optional <c>&lt;text&gt;</c> label.
/// - <see cref="LayoutBand"/> → <c>&lt;rect&gt;</c> with optional rotated or horizontal
///   <c>&lt;text&gt;</c> label; children rendered recursively.
/// - <see cref="LayoutLifeline"/> → <c>&lt;rect&gt;</c> header + dashed
///   <c>&lt;line&gt;</c> stem + <c>&lt;text&gt;</c> label.
/// - <see cref="LayoutActivation"/> → <c>&lt;rect&gt;</c> with white fill.
/// - <see cref="LayoutGrid"/> → bordered <c>&lt;rect&gt;</c> cells with per-cell
///   <c>&lt;text&gt;</c> elements.
/// - All other node types are silently skipped for forward compatibility.
///
/// Fill colors are derived from <see cref="Theme.DepthFillColors"/> using modulo wrapping on
/// <see cref="LayoutBox.Depth"/>.
/// </remarks>
public sealed class SvgRenderer : IRenderer
{
    /// <summary>Closing tag for an SVG marker element, indented to match the defs block.</summary>
    private const string MarkerClose = "    </marker>";

    /// <summary>SVG text-anchor value for centered alignment.</summary>
    private const string TextAnchorMiddle = "middle";

    /// <inheritdoc/>
    public string MediaType => "image/svg+xml";

    /// <inheritdoc/>
    public string DefaultExtension => ".svg";

    /// <inheritdoc/>
    /// <remarks>
    /// Writes a complete SVG 1.1 document to <paramref name="output"/>. The canvas size is
    /// taken from <see cref="LayoutTree.Width"/> and <see cref="LayoutTree.Height"/>.
    /// All coordinates are expressed as doubles formatted to two decimal places.
    /// </remarks>
    public void Render(LayoutTree layout, RenderOptions options, Stream output)
    {
        // Validate inputs — null arguments would produce silent failures
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        // Build the SVG document in memory then flush once to avoid partial writes
        var sb = new StringBuilder();
        var theme = options.Theme;

        // Compute canvas dimensions, ensuring a minimum 1×1 canvas
        var width = Math.Max(1.0, layout.Width * options.Scale);
        var height = Math.Max(1.0, layout.Height * options.Scale);

        // Write SVG root element with explicit namespace and viewBox
        sb.Append(CultureInfo.InvariantCulture,
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}">""");
        sb.AppendLine();

        // Write defs section with all arrowhead markers
        WriteArrowheadDefs(sb, theme);

        // Render all top-level nodes recursively
        foreach (var node in layout.Nodes)
        {
            RenderNode(sb, node, theme, options.Scale);
        }

        // Close SVG root
        sb.AppendLine("</svg>");

        // Encode as UTF-8 and write to output stream
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        output.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Writes the <c>&lt;defs&gt;</c> block containing all arrowhead marker definitions used
    /// by <see cref="RenderLine"/>.
    /// </summary>
    /// <remarks>
    /// Markers defined: <c>arrowhead-open</c> (hollow triangle),
    /// <c>arrowhead-filled</c> (filled triangle), <c>arrowhead-diamond</c> (hollow diamond),
    /// <c>arrowhead-filled-diamond</c> (filled diamond), <c>arrowhead-circle</c> (hollow
    /// circle), and <c>arrowhead-bar</c> (perpendicular bar).
    /// </remarks>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="theme">Theme providing stroke color and width.</param>
    private static void WriteArrowheadDefs(StringBuilder sb, Theme theme)
    {
        sb.AppendLine("  <defs>");

        // Open arrowhead marker — hollow triangle pointing along the line direction
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-open" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <polygon points="0 0, 10 3.5, 0 7" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine(MarkerClose);

        // Filled arrowhead marker — solid triangle pointing along the line direction
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-filled" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <polygon points="0 0, 10 3.5, 0 7" fill="{theme.StrokeColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine(MarkerClose);

        // Diamond marker — open four-point diamond straddling the line end
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-diamond" markerWidth="14" markerHeight="8" refX="13" refY="4" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <polygon points="1 4, 7 0, 13 4, 7 8" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine(MarkerClose);

        // Filled diamond marker — solid four-point diamond straddling the line end
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-filled-diamond" markerWidth="14" markerHeight="8" refX="13" refY="4" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <polygon points="1 4, 7 0, 13 4, 7 8" fill="{theme.StrokeColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine(MarkerClose);

        // Circle marker — open circle whose near edge sits at the line endpoint
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-circle" markerWidth="10" markerHeight="10" refX="9" refY="5" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <circle cx="5" cy="5" r="4" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine(MarkerClose);

        // Bar marker — perpendicular line centered on the line endpoint
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-bar" markerWidth="4" markerHeight="12" refX="2" refY="6" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <line x1="2" y1="0" x2="2" y2="12" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine(MarkerClose);

        sb.AppendLine("  </defs>");
    }

    /// <summary>
    /// Dispatches a single <see cref="LayoutNode"/> to the appropriate typed render method.
    /// Unknown concrete types are silently skipped so that future node types do not break
    /// existing callers.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="node">The node to render.</param>
    /// <param name="theme">Visual theme providing colors and dimensions.</param>
    /// <param name="scale">Uniform scale factor applied to all coordinates.</param>
    private static void RenderNode(StringBuilder sb, LayoutNode node, Theme theme, double scale)
    {
        switch (node)
        {
            case LayoutBox box:
                RenderBox(sb, box, theme, scale);
                break;

            case LayoutLine line:
                RenderLine(sb, line, theme, scale);
                break;

            case LayoutLabel label:
                RenderLabel(sb, label, theme, scale);
                break;

            case LayoutPort port:
                RenderPort(sb, port, theme, scale);
                break;

            case LayoutBadge badge:
                RenderBadge(sb, badge, theme, scale);
                break;

            case LayoutBand band:
                RenderBand(sb, band, theme, scale);
                break;

            case LayoutLifeline lifeline:
                RenderLifeline(sb, lifeline, theme, scale);
                break;

            case LayoutActivation activation:
                RenderActivation(sb, activation, theme, scale);
                break;

            case LayoutGrid grid:
                RenderGrid(sb, grid, theme, scale);
                break;

            default:
                // Skip unknown node types for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as a <c>&lt;rect&gt;</c> with <c>rx</c>/<c>ry</c>
    /// attributes for rounded corners, an optional centered <c>&lt;text&gt;</c> label,
    /// horizontal divider <c>&lt;line&gt;</c> elements and text rows for each compartment,
    /// then recursively renders its children.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">The box node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font size.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        // Derive fill color from theme using depth modulo wrapping
        var fillColor = theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count];

        // Draw the box outline (shape-specific)
        RenderBoxOutline(sb, box, theme, fillColor, scale);

        // Draw the keyword and label in the title area
        RenderBoxTitle(sb, box, theme, scale);

        // Render compartments below the label area
        if (box.Compartments.Count > 0)
        {
            RenderBoxCompartments(sb, box, theme, scale);
        }

        // Render children recursively
        foreach (var child in box.Children)
        {
            RenderNode(sb, child, theme, scale);
        }
    }

    /// <summary>
    /// Renders the outline (border and fill) of a <see cref="LayoutBox"/>, selecting the path
    /// geometry based on <see cref="LayoutBox.Shape"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">The box whose outline is drawn.</param>
    /// <param name="theme">Visual theme providing stroke settings and corner radius.</param>
    /// <param name="fillColor">Resolved fill color for the box interior.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxOutline(StringBuilder sb, LayoutBox box, Theme theme, string fillColor, double scale)
    {
        var x = box.X * scale;
        var y = box.Y * scale;
        var w = box.Width * scale;
        var h = box.Height * scale;

        switch (box.Shape)
        {
            case BoxShape.Folder:
                RenderFolderOutline(sb, box, theme, fillColor, scale);
                break;

            case BoxShape.Note:
                RenderNoteOutline(sb, box, theme, fillColor, scale);
                break;

            case BoxShape.RoundedRectangle:
                var cornerStr = theme.LineCornerRadius > 0
                    ? $" rx=\"{F(theme.LineCornerRadius * 2.0 * scale)}\" ry=\"{F(theme.LineCornerRadius * 2.0 * scale)}\""
                    : string.Empty;
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{cornerStr}/>""");
                sb.AppendLine();
                break;

            default:
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                sb.AppendLine();
                break;
        }
    }

    /// <summary>
    /// Renders a folder-shaped outline (a tab at the top-left above a full-width body),
    /// used for package nodes.
    /// </summary>
    private static void RenderFolderOutline(StringBuilder sb, LayoutBox box, Theme theme, string fillColor, double scale)
    {
        var tabHeight = BoxMetrics.FolderTabHeight(theme);
        var tabWidth = Math.Min(box.Width * 0.45, Math.Max(60.0, (box.Label?.Length ?? 4) * theme.FontSizeBody * 0.55 + 2.0 * theme.LabelPadding));

        var x = box.X * scale;
        var yTab = box.Y * scale;
        var yBody = (box.Y + tabHeight) * scale;
        var xTabRight = (box.X + tabWidth) * scale;
        var xRight = (box.X + box.Width) * scale;
        var yBottom = (box.Y + box.Height) * scale;

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <path d="M {F(x)} {F(yBody)} L {F(x)} {F(yTab)} L {F(xTabRight)} {F(yTab)} L {F(xTabRight)} {F(yBody)} L {F(xRight)} {F(yBody)} L {F(xRight)} {F(yBottom)} L {F(x)} {F(yBottom)} Z" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a note-shaped outline (a rectangle with a folded-down top-right corner),
    /// used for documentation and comment nodes.
    /// </summary>
    private static void RenderNoteOutline(StringBuilder sb, LayoutBox box, Theme theme, string fillColor, double scale)
    {
        var fold = Math.Min(box.Width, box.Height) * 0.25;
        fold = Math.Min(fold, 16.0);

        var x = box.X * scale;
        var y = box.Y * scale;
        var xRight = (box.X + box.Width) * scale;
        var xFold = (box.X + box.Width - fold) * scale;
        var yFold = (box.Y + fold) * scale;
        var yBottom = (box.Y + box.Height) * scale;

        // Main body with the top-right corner cut
        sb.Append(CultureInfo.InvariantCulture,
            $"""  <path d="M {F(x)} {F(y)} L {F(xFold)} {F(y)} L {F(xRight)} {F(yFold)} L {F(xRight)} {F(yBottom)} L {F(x)} {F(yBottom)} Z" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();

        // The folded corner triangle
        sb.Append(CultureInfo.InvariantCulture,
            $"""  <path d="M {F(xFold)} {F(y)} L {F(xFold)} {F(yFold)} L {F(xRight)} {F(yFold)}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders the optional keyword line and bold name label in the title area of a box.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">Box whose title is rendered.</param>
    /// <param name="theme">Visual theme providing font sizes and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxTitle(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        var centerX = (box.X + box.Width / 2.0) * scale;
        var cursorY = box.Y + theme.LabelPadding;

        // Keyword line (smaller, italic, guillemet-wrapped) above the name
        if (box.Keyword != null)
        {
            var kwY = (cursorY + theme.FontSizeBody / 2.0) * scale;
            sb.Append(CultureInfo.InvariantCulture,
                $"""  <text x="{F(centerX)}" y="{F(kwY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" font-style="italic" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml("\u00AB" + box.Keyword + "\u00BB")}</text>""");
            sb.AppendLine();
            cursorY += theme.FontSizeBody + theme.LabelPadding;
        }

        // Bold name label
        if (box.Label != null)
        {
            var textY = (cursorY + theme.FontSizeTitle / 2.0) * scale;
            var availableWidth = (box.Width - 2 * theme.LabelPadding) * scale;
            sb.Append(CultureInfo.InvariantCulture,
                $"""  <text x="{F(centerX)}" y="{F(textY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeTitle * scale)}" font-weight="bold" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle" textLength="{F(availableWidth)}" lengthAdjust="spacingAndGlyphs">{EscapeXml(box.Label)}</text>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Renders the compartments of a <see cref="LayoutBox"/> below the title area as SVG
    /// <c>&lt;line&gt;</c> dividers and <c>&lt;text&gt;</c> elements.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">Box whose compartments are rendered.</param>
    /// <param name="theme">Visual theme providing font sizes, padding, and stroke settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxCompartments(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        // Compartments start below the title area (keyword + label), computed via shared metrics
        var labelAreaHeight = BoxMetrics.TitleAreaHeight(theme, box.Label != null, box.Keyword != null);
        var compartmentY = box.Y + labelAreaHeight;

        foreach (var compartment in box.Compartments)
        {
            // Full-width horizontal divider at the top of this compartment
            sb.Append(CultureInfo.InvariantCulture,
                $"""  <line x1="{F(box.X * scale)}" y1="{F(compartmentY * scale)}" x2="{F((box.X + box.Width) * scale)}" y2="{F(compartmentY * scale)}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
            sb.AppendLine();

            // Draw optional bold compartment title
            if (compartment.Title != null)
            {
                var titleX = (box.X + theme.LabelPadding) * scale;
                var titleY = (compartmentY + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <text x="{F(titleX)}" y="{F(titleY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" font-weight="bold" font-style="italic" fill="{theme.StrokeColor}" text-anchor="start" dominant-baseline="middle">{EscapeXml(compartment.Title)}</text>""");
                sb.AppendLine();
                compartmentY += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
            }

            // Draw each body row with body font size and left-aligned indent
            foreach (var row in compartment.Rows)
            {
                var rowX = (box.X + theme.LabelPadding) * scale;
                var rowY = (compartmentY + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <text x="{F(rowX)}" y="{F(rowY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="start" dominant-baseline="middle">{EscapeXml(row)}</text>""");
                sb.AppendLine();
                compartmentY += theme.LabelPadding + theme.FontSizeBody;
            }

            // Bottom gap so the last row clears the next compartment divider.
            compartmentY += theme.LabelPadding;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as an SVG <c>&lt;path&gt;</c> element with
    /// arc-at-bend corner rounding, optional arrowhead marker references, optional dashing,
    /// and an optional midpoint label.
    /// </summary>
    /// <remarks>
    /// When <see cref="Theme.LineCornerRadius"/> is zero, each interior waypoint is
    /// connected with a plain <c>L</c> command. When the radius is positive, each interior
    /// waypoint is replaced with a shortened incoming <c>L</c> command and an <c>A</c>
    /// (arc) command whose sweep direction is derived from the cross product of the incoming
    /// and outgoing direction vectors.
    /// </remarks>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="line">The line node to render.</param>
    /// <param name="theme">Visual theme providing stroke color and width.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)
    {
        // Lines with fewer than 2 waypoints cannot be drawn
        if (line.Waypoints.Count < 2)
        {
            return;
        }

        // Build SVG path data with optional arc-at-bend corner rounding
        var pathData = BuildLinePath(line.Waypoints, theme.LineCornerRadius, scale);

        // Resolve arrowhead marker attribute strings
        var markerStart = line.SourceArrowhead switch
        {
            ArrowheadStyle.Open => " marker-start=\"url(#arrowhead-open)\"",
            ArrowheadStyle.Filled => " marker-start=\"url(#arrowhead-filled)\"",
            ArrowheadStyle.Diamond => " marker-start=\"url(#arrowhead-diamond)\"",
            ArrowheadStyle.FilledDiamond => " marker-start=\"url(#arrowhead-filled-diamond)\"",
            ArrowheadStyle.Circle => " marker-start=\"url(#arrowhead-circle)\"",
            ArrowheadStyle.Bar => " marker-start=\"url(#arrowhead-bar)\"",
            _ => string.Empty
        };
        var markerEnd = line.TargetArrowhead switch
        {
            ArrowheadStyle.Open => " marker-end=\"url(#arrowhead-open)\"",
            ArrowheadStyle.Filled => " marker-end=\"url(#arrowhead-filled)\"",
            ArrowheadStyle.Diamond => " marker-end=\"url(#arrowhead-diamond)\"",
            ArrowheadStyle.FilledDiamond => " marker-end=\"url(#arrowhead-filled-diamond)\"",
            ArrowheadStyle.Circle => " marker-end=\"url(#arrowhead-circle)\"",
            ArrowheadStyle.Bar => " marker-end=\"url(#arrowhead-bar)\"",
            _ => string.Empty
        };

        // Determine stroke dash pattern for non-solid lines
        var dashArray = line.LineStyle switch
        {
            LineStyle.Dashed => " stroke-dasharray=\"6 3\"",
            LineStyle.Dotted => " stroke-dasharray=\"2 2\"",
            _ => string.Empty
        };

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <path d="{pathData}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{markerStart}{markerEnd}{dashArray}/>""");
        sb.AppendLine();

        // Draw the optional midpoint label as a centered text element
        if (line.MidpointLabel != null)
        {
            var (midX, midY) = ComputeLineMidpoint(line.Waypoints);
            sb.Append(CultureInfo.InvariantCulture,
                $"""  <text x="{F(midX * scale)}" y="{F(midY * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(line.MidpointLabel)}</text>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Builds the SVG path <c>d</c> attribute string for a polyline, applying arc-at-bend
    /// corner rounding for each interior waypoint when <paramref name="cornerRadius"/> is
    /// greater than zero.
    /// </summary>
    /// <remarks>
    /// For each interior waypoint the incoming segment is shortened by
    /// <paramref name="cornerRadius"/> and an SVG arc (<c>A</c> command) bridges the gap.
    /// The arc sweep direction is determined by the cross product of the incoming and outgoing
    /// direction vectors: positive cross product (clockwise turn in SVG screen space) uses
    /// sweep-flag 1; negative uses sweep-flag 0. The radius is clamped to half the shorter
    /// adjacent segment so the arc never overshoots.
    /// </remarks>
    /// <param name="waypoints">Ordered waypoints; must contain at least 2 entries.</param>
    /// <param name="cornerRadius">Corner rounding radius in logical pixels; 0 disables arcs.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>SVG path data string starting with <c>M</c>.</returns>
    private static string BuildLinePath(
        IReadOnlyList<Point2D> waypoints,
        double cornerRadius,
        double scale)
    {
        var sb = new StringBuilder();
        var first = waypoints[0];
        sb.Append(CultureInfo.InvariantCulture, $"M {F(first.X * scale)} {F(first.Y * scale)}");

        if (cornerRadius <= 0)
        {
            // No corner rounding: plain M/L path
            for (var i = 1; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                sb.Append(CultureInfo.InvariantCulture, $" L {F(wp.X * scale)} {F(wp.Y * scale)}");
            }

            return sb.ToString();
        }

        // Arc-at-bends: replace each interior waypoint with a shortened L + arc A command
        for (var i = 1; i < waypoints.Count; i++)
        {
            var cur = waypoints[i];

            var isInterior = i < waypoints.Count - 1;
            if (!isInterior)
            {
                // Last waypoint: plain line to the endpoint
                sb.Append(CultureInfo.InvariantCulture, $" L {F(cur.X * scale)} {F(cur.Y * scale)}");
                continue;
            }

            var prev = waypoints[i - 1];
            var next = waypoints[i + 1];

            // Incoming direction from prev to cur
            var inDx = cur.X - prev.X;
            var inDy = cur.Y - prev.Y;
            var inLen = Math.Sqrt(inDx * inDx + inDy * inDy);

            // Outgoing direction from cur to next
            var outDx = next.X - cur.X;
            var outDy = next.Y - cur.Y;
            var outLen = Math.Sqrt(outDx * outDx + outDy * outDy);

            if (inLen < 0.001 || outLen < 0.001)
            {
                // Degenerate segment: fall back to a plain line command
                sb.Append(CultureInfo.InvariantCulture, $" L {F(cur.X * scale)} {F(cur.Y * scale)}");
                continue;
            }

            // Normalize both direction vectors
            var inNx = inDx / inLen;
            var inNy = inDy / inLen;
            var outNx = outDx / outLen;
            var outNy = outDy / outLen;

            // Clamp radius so the arc never overshoots either adjacent segment
            var r = Math.Min(cornerRadius, Math.Min(inLen / 2.0, outLen / 2.0));

            // Endpoint of the shortened incoming segment (just before the corner)
            var shortEndX = cur.X - inNx * r;
            var shortEndY = cur.Y - inNy * r;

            // Start of the outgoing segment after the arc (just past the corner)
            var shortStartX = cur.X + outNx * r;
            var shortStartY = cur.Y + outNy * r;

            // Cross product z-component determines clockwise vs counter-clockwise arc in SVG
            // (positive cross = clockwise screen turn = sweep-flag 1)
            var cross = inNx * outNy - inNy * outNx;
            var sweep = cross > 0 ? 1 : 0;

            sb.Append(CultureInfo.InvariantCulture,
                $" L {F(shortEndX * scale)} {F(shortEndY * scale)}");
            sb.Append(CultureInfo.InvariantCulture,
                $" A {F(r * scale)} {F(r * scale)} 0 0 {sweep} {F(shortStartX * scale)} {F(shortStartY * scale)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Computes the geometric midpoint of an ordered waypoint list. For an odd count the
    /// center element is returned; for an even count the average of the two center elements
    /// is returned.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints; must contain at least one entry.</param>
    /// <returns>The (X, Y) coordinates of the midpoint in logical pixels.</returns>
    private static (double X, double Y) ComputeLineMidpoint(IReadOnlyList<Point2D> waypoints)
    {
        var n = waypoints.Count;
        if (n % 2 == 1)
        {
            return (waypoints[n / 2].X, waypoints[n / 2].Y);
        }

        var lo = waypoints[n / 2 - 1];
        var hi = waypoints[n / 2];
        return ((lo.X + hi.X) / 2.0, (lo.Y + hi.Y) / 2.0);
    }

    /// <summary>
    /// Renders a <see cref="LayoutLabel"/> as an SVG <c>&lt;text&gt;</c> element with
    /// <c>text-anchor</c> derived from <see cref="TextAlign"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="label">The label node to render.</param>
    /// <param name="theme">Visual theme providing font and color settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderLabel(StringBuilder sb, LayoutLabel label, Theme theme, double scale)
    {
        var x = label.X * scale;
        var y = label.Y * scale;
        var anchor = label.Align switch
        {
            TextAlign.Center => TextAnchorMiddle,
            TextAlign.Right => "end",
            _ => "start"
        };
        var fontWeight = label.Weight == FontWeight.Bold ? "bold" : "normal";
        var fontStyle = label.Style == FontStyle.Italic ? "italic" : "normal";
        var textLengthAttr = label.MaxWidth > 0
            ? $""" textLength="{F(label.MaxWidth * scale)}" lengthAdjust="spacingAndGlyphs" """
            : " ";

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <text x="{F(x)}" y="{F(y)}" font-family="Noto Sans, sans-serif" font-size="{F(label.FontSize * scale)}" font-weight="{fontWeight}" font-style="{fontStyle}" fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle"{textLengthAttr.TrimEnd()}>{EscapeXml(label.Text)}</text>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutPort"/> as a filled 8×8 <c>&lt;rect&gt;</c> centered at
    /// the port position, with an optional <c>&lt;text&gt;</c> label offset away from the
    /// attached edge.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="port">The port node to render.</param>
    /// <param name="theme">Visual theme providing stroke color and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderPort(StringBuilder sb, LayoutPort port, Theme theme, double scale)
    {
        // Port square: 8×8 logical pixels, filled with the stroke color
        const double PortHalfSize = 4.0;
        var rx = (port.CentreX - PortHalfSize) * scale;
        var ry = (port.CentreY - PortHalfSize) * scale;
        var rs = PortHalfSize * 2.0 * scale;

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <rect x="{F(rx)}" y="{F(ry)}" width="{F(rs)}" height="{F(rs)}" fill="{theme.StrokeColor}"/>""");
        sb.AppendLine();

        // Optional label offset away from the attached edge
        if (port.Label != null)
        {
            var offset = PortHalfSize + theme.LabelPadding;
            var (labelX, labelY, anchor) = port.Side switch
            {
                PortSide.Top => (port.CentreX, port.CentreY - offset, TextAnchorMiddle),
                PortSide.Bottom => (port.CentreX, port.CentreY + offset + theme.FontSizeBody, TextAnchorMiddle),
                PortSide.Left => (port.CentreX - offset, port.CentreY + theme.FontSizeBody / 2.0, "end"),
                _ => (port.CentreX + offset, port.CentreY + theme.FontSizeBody / 2.0, "start")
            };

            sb.Append(CultureInfo.InvariantCulture,
                $"""  <text x="{F(labelX * scale)}" y="{F(labelY * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle">{EscapeXml(port.Label)}</text>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBadge"/> as shape-specific SVG elements centered at the
    /// badge position, with an optional <c>&lt;text&gt;</c> label to the right.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="badge">The badge node to render.</param>
    /// <param name="theme">Visual theme providing stroke color, width, and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBadge(StringBuilder sb, LayoutBadge badge, Theme theme, double scale)
    {
        var cx = badge.CentreX * scale;
        var cy = badge.CentreY * scale;
        var r = badge.Size / 2.0 * scale;
        var sw = F(theme.StrokeWidth);

        // Draw the badge shape centered at (cx, cy) within bounding radius r
        switch (badge.Shape)
        {
            case BadgeShape.FilledCircle:
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{theme.StrokeColor}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.Bullseye:
                // Outer filled circle + white inner circle for the ring effect
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{theme.StrokeColor}"/>""");
                sb.AppendLine();
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r / 3.0)}" fill="white" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.Diamond:
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <polygon points="{F(cx)},{F(cy - r)} {F(cx + r)},{F(cy)} {F(cx)},{F(cy + r)} {F(cx - r)},{F(cy)}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.HorizontalBar:
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <line x1="{F(cx - r * 0.8)}" y1="{F(cy)}" x2="{F(cx + r * 0.8)}" y2="{F(cy)}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.VerticalBar:
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <line x1="{F(cx)}" y1="{F(cy - r * 0.8)}" x2="{F(cx)}" y2="{F(cy + r * 0.8)}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            default:
                // Unknown badge shapes are skipped for forward compatibility
                break;
        }

        // Optional label to the right of the bounding circle
        if (badge.Label != null)
        {
            var labelX = (badge.CentreX + badge.Size / 2.0 + theme.LabelPadding) * scale;
            var labelY = (badge.CentreY + theme.FontSizeBody / 2.0) * scale;
            sb.Append(CultureInfo.InvariantCulture,
                $"""  <text x="{F(labelX)}" y="{F(labelY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="start" dominant-baseline="middle">{EscapeXml(badge.Label)}</text>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBand"/> as a <c>&lt;rect&gt;</c> with a depth-0 fill,
    /// an optional label (rotated for Horizontal orientation, horizontal for Vertical),
    /// then recursively renders its children.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="band">The band node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBand(StringBuilder sb, LayoutBand band, Theme theme, double scale)
    {
        var x = band.X * scale;
        var y = band.Y * scale;
        var w = band.Width * scale;
        var h = band.Height * scale;
        var fillColor = theme.DepthFillColors[0];

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();

        // Draw the optional label; Horizontal → rotated 90° CCW on left edge, Vertical → top
        if (band.Label != null)
        {
            if (band.Orientation == BandOrientation.Horizontal)
            {
                // Label center on the left edge strip, rotated 90° CCW
                var labelCx = (band.X + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                var labelCy = (band.Y + band.Height / 2.0) * scale;
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <text x="0" y="0" transform="translate({F(labelCx)},{F(labelCy)}) rotate(-90)" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(band.Label)}</text>""");
                sb.AppendLine();
            }
            else
            {
                // Horizontal label at the top of the band
                var textX = (band.X + band.Width / 2.0) * scale;
                var textY = (band.Y + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <text x="{F(textX)}" y="{F(textY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(band.Label)}</text>""");
                sb.AppendLine();
            }
        }

        // Render children recursively
        foreach (var child in band.Children)
        {
            RenderNode(sb, child, theme, scale);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLifeline"/> as a header <c>&lt;rect&gt;</c> with a
    /// centered label, followed by a dashed <c>&lt;line&gt;</c> stem running from the
    /// bottom of the header to <see cref="LayoutLifeline.BottomY"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="lifeline">The lifeline node to render.</param>
    /// <param name="theme">Visual theme providing colors, font, and stroke settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderLifeline(StringBuilder sb, LayoutLifeline lifeline, Theme theme, double scale)
    {
        // Header box: centered at CentreX, top edge at TopY
        var headerLeft = lifeline.CentreX - lifeline.HeaderWidth / 2.0;
        var hx = headerLeft * scale;
        var hy = lifeline.TopY * scale;
        var hw = lifeline.HeaderWidth * scale;
        var hh = lifeline.HeaderHeight * scale;
        var fillColor = theme.DepthFillColors[0];

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <rect x="{F(hx)}" y="{F(hy)}" width="{F(hw)}" height="{F(hh)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();

        // Centered label within the header box
        var textX = lifeline.CentreX * scale;
        var textY = (lifeline.TopY + lifeline.HeaderHeight / 2.0) * scale;
        sb.Append(CultureInfo.InvariantCulture,
            $"""  <text x="{F(textX)}" y="{F(textY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" font-weight="bold" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(lifeline.Label)}</text>""");
        sb.AppendLine();

        // Dashed vertical stem from the bottom of the header to BottomY
        var stemX = lifeline.CentreX * scale;
        var stemTopY = (lifeline.TopY + lifeline.HeaderHeight) * scale;
        var stemBottomY = lifeline.BottomY * scale;
        sb.Append(CultureInfo.InvariantCulture,
            $"""  <line x1="{F(stemX)}" y1="{F(stemTopY)}" x2="{F(stemX)}" y2="{F(stemBottomY)}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}" stroke-dasharray="6 3"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutActivation"/> as a narrow white-filled
    /// <c>&lt;rect&gt;</c> with a stroke border, centered at
    /// <see cref="LayoutActivation.CentreX"/>.
    /// </summary>
    /// <remarks>
    /// The bar width is <c>Theme.LabelPadding * 2</c> so it scales proportionally with
    /// the diagram's text padding.
    /// </remarks>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="activation">The activation node to render.</param>
    /// <param name="theme">Visual theme providing stroke settings and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderActivation(StringBuilder sb, LayoutActivation activation, Theme theme, double scale)
    {
        // Bar width = LabelPadding * 2, centered at CentreX
        var halfWidth = theme.LabelPadding;
        var ax = (activation.CentreX - halfWidth) * scale;
        var ay = activation.TopY * scale;
        var aw = halfWidth * 2.0 * scale;
        var ah = (activation.BottomY - activation.TopY) * scale;

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <rect x="{F(ax)}" y="{F(ay)}" width="{F(aw)}" height="{F(ah)}" fill="white" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutGrid"/> as a bordered table. Header rows are filled with
    /// the depth-1 theme color; body rows use the depth-0 color. Each cell contains a
    /// <c>&lt;text&gt;</c> element aligned per <see cref="LayoutGridCell.Align"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="grid">The grid node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderGrid(StringBuilder sb, LayoutGrid grid, Theme theme, double scale)
    {
        // Header rows use depth-1 color; body rows use depth-0 color
        var headerFill = theme.DepthFillColors[1 % theme.DepthFillColors.Count];
        var bodyFill = theme.DepthFillColors[0];

        var currentY = grid.Y;
        foreach (var row in grid.Rows)
        {
            // Row height = maximum cell height in this row
            var rowHeight = 0.0;
            foreach (var cell in row.Cells)
            {
                rowHeight = Math.Max(rowHeight, cell.Height);
            }

            var fillColor = row.IsHeader ? headerFill : bodyFill;
            var currentX = grid.X;

            foreach (var cell in row.Cells)
            {
                var cx = currentX * scale;
                var cy = currentY * scale;
                var cw = cell.Width * scale;
                var ch = rowHeight * scale;

                // Cell background and border
                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <rect x="{F(cx)}" y="{F(cy)}" width="{F(cw)}" height="{F(ch)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                sb.AppendLine();

                // Cell text properties are computed in a helper to stay within complexity limits
                var (anchor, textX, fontWeightAttr) = GetCellTextProperties(cell, currentX, theme, row.IsHeader);
                var textY = currentY + rowHeight / 2.0;

                sb.Append(CultureInfo.InvariantCulture,
                    $"""  <text x="{F(textX * scale)}" y="{F(textY * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{fontWeightAttr} fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle">{EscapeXml(cell.Text)}</text>""");
                sb.AppendLine();

                currentX += cell.Width;
            }

            currentY += rowHeight;
        }
    }

    /// <summary>
    /// Computes the SVG text-anchor, horizontal text position, and font-weight attribute for a
    /// single grid cell. Extracted from <see cref="RenderGrid"/> to reduce its cognitive complexity.
    /// </summary>
    /// <param name="cell">The grid cell whose alignment drives the computed values.</param>
    /// <param name="cellLeft">Logical X origin of the cell within the grid.</param>
    /// <param name="theme">Visual theme providing label padding.</param>
    /// <param name="isHeader">
    /// <see langword="true"/> when the cell belongs to a header row; controls font weight.
    /// </param>
    /// <returns>
    /// A tuple of (<c>Anchor</c>, <c>TextX</c>, <c>FontWeightAttr</c>) ready for SVG output.
    /// </returns>
    private static (string Anchor, double TextX, string FontWeightAttr) GetCellTextProperties(
        LayoutGridCell cell,
        double cellLeft,
        Theme theme,
        bool isHeader)
    {
        var anchor = cell.Align switch
        {
            TextAlign.Center => TextAnchorMiddle,
            TextAlign.Right => "end",
            _ => "start"
        };
        var textX = cell.Align switch
        {
            TextAlign.Center => cellLeft + cell.Width / 2.0,
            TextAlign.Right => cellLeft + cell.Width - theme.LabelPadding,
            _ => cellLeft + theme.LabelPadding
        };
        var fontWeightAttr = isHeader ? " font-weight=\"bold\"" : string.Empty;
        return (anchor, textX, fontWeightAttr);
    }

    /// <summary>
    /// Formats a double to two decimal places using invariant culture.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <returns>String representation with exactly two decimal digits.</returns>
    private static string F(double value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes XML special characters in a string for safe embedding in SVG text content.
    /// </summary>
    /// <param name="text">Raw text to escape.</param>
    /// <returns>XML-safe text with <c>&lt;</c>, <c>&gt;</c>, and <c>&amp;</c> replaced by
    /// their entity equivalents.</returns>
    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}

