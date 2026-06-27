// <copyright file="PngRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;
using SkiaSharp;

namespace DemaConsulting.SysML2Tools.Png;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to PNG format using SkiaSharp.
/// </summary>
/// <remarks>
/// The renderer is pure and stateless: each call to <see cref="Render"/> allocates a new
/// <see cref="SKBitmap"/> and <see cref="SKCanvas"/>, draws all nodes, and encodes the result
/// to the output stream before disposing all SkiaSharp resources. The output stream is not
/// closed or flushed by this renderer; the caller is responsible for its lifetime.
///
/// Node rendering:
/// - <see cref="LayoutBox"/> → filled rectangle (plain or rounded) + optional centered label
///   + compartment dividers and text rows; children rendered recursively.
/// - <see cref="LayoutLine"/> → corner-radius-aware polyline built as a single
///   <see cref="SKPath"/> with optional dashing; arrowheads at both ends; optional midpoint
///   label with white background.
/// - <see cref="LayoutLabel"/> → text element with <see cref="TextAlign"/>-derived alignment.
/// - <see cref="LayoutPort"/> → small filled square centered at the port position with
///   optional label offset away from the attached edge.
/// - <see cref="LayoutBadge"/> → icon shape (filled circle, bullseye, diamond, or bar)
///   centered at the badge position with optional label to the right.
/// - <see cref="LayoutBand"/> → swim-lane rectangle; label rendered vertically on the left
///   edge for Horizontal orientation or horizontally at the top for Vertical; children
///   rendered recursively.
/// - <see cref="LayoutLifeline"/> → header box at the top with a dashed vertical stem below.
/// - <see cref="LayoutActivation"/> → narrow white-filled rectangle with stroke border
///   centered at <c>CentreX</c>.
/// - <see cref="LayoutGrid"/> → bordered table; header rows use depth-1 fill color, body
///   rows use depth-0 fill color; per-cell text alignment respected.
/// - All other node types are silently skipped for forward compatibility.
///
/// Fill colors are derived from <see cref="Theme.DepthFillColors"/> using modulo wrapping on
/// <see cref="LayoutBox.Depth"/>. Hex color strings (e.g., <c>#RRGGBB</c>) are parsed with
/// <see cref="SKColor.Parse"/>.
///
/// A minimum bitmap size of 1×1 pixels is enforced to prevent SkiaSharp allocation errors
/// when the layout tree is empty.
/// </remarks>
public sealed class PngRenderer : IRenderer
{
    /// <summary>
    /// Lazily-loaded typeface for regular-weight, upright text. Loaded once from the embedded
    /// NotoSans-Regular.ttf resource so all renders use the same font regardless of which fonts
    /// are installed on the host system.
    /// </summary>
    private static readonly Lazy<SKTypeface> RegularTypeface = new(() => LoadTypeface("NotoSans-Regular.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for bold-weight, upright text. Loaded from NotoSans-Bold.ttf.
    /// </summary>
    private static readonly Lazy<SKTypeface> BoldTypeface = new(() => LoadTypeface("NotoSans-Bold.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for regular-weight, italic text. Loaded from NotoSans-Italic.ttf.
    /// </summary>
    private static readonly Lazy<SKTypeface> ItalicTypeface = new(() => LoadTypeface("NotoSans-Italic.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for bold-weight, italic text. Loaded from NotoSans-BoldItalic.ttf.
    /// </summary>
    private static readonly Lazy<SKTypeface> BoldItalicTypeface = new(() => LoadTypeface("NotoSans-BoldItalic.ttf"));

    /// <summary>
    /// Loads a typeface from an embedded assembly resource. The resource is matched by its
    /// filename suffix (case-insensitive). Falls back to <see cref="SKTypeface.Default"/> if
    /// the resource is not found, so the renderer remains functional even when the font is not
    /// embedded (e.g., during development without the downloaded font files).
    /// </summary>
    /// <param name="fileName">File name suffix to match in the assembly manifest resource names.</param>
    /// <returns>
    /// An <see cref="SKTypeface"/> loaded from the embedded resource, or
    /// <see cref="SKTypeface.Default"/> if the resource is not found.
    /// </returns>
    private static SKTypeface LoadTypeface(string fileName)
    {
        var asm = typeof(PngRenderer).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return SKTypeface.Default;
        }

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? SKTypeface.Default;
    }

    /// <summary>
    /// Creates an <see cref="SKPaint"/> configured for text rendering with the Noto Sans typeface
    /// matching the requested weight and style. The caller is responsible for disposing the
    /// returned paint.
    /// </summary>
    /// <param name="color">Fill color for the text glyphs.</param>
    /// <param name="fontSize">Font size in scaled pixels.</param>
    /// <param name="bold">When <see langword="true"/>, selects the bold typeface variant.</param>
    /// <param name="italic">When <see langword="true"/>, selects the italic typeface variant.</param>
    /// <returns>A new <see cref="SKPaint"/> ready for use with <c>canvas.DrawText</c>.</returns>
    private static SKPaint CreateTextPaint(SKColor color, float fontSize, bool bold, bool italic)
    {
        var typeface = (bold, italic) switch
        {
            (true, true) => BoldItalicTypeface.Value,
            (true, false) => BoldTypeface.Value,
            (false, true) => ItalicTypeface.Value,
            _ => RegularTypeface.Value,
        };
        return new SKPaint
        {
            Color = color,
            TextSize = fontSize,
            IsAntialias = true,
            Typeface = typeface,
        };
    }

    /// <summary>
    /// Computes a reduced font size that fits <paramref name="text"/> within
    /// <paramref name="availableWidth"/> scaled pixels by scaling down proportionally.
    /// Returns <paramref name="maxFontSize"/> unchanged when the text already fits or
    /// when there is no meaningful width constraint.
    /// </summary>
    /// <param name="paint">Paint whose <see cref="SKPaint.TextSize"/> is temporarily set
    /// to <paramref name="maxFontSize"/> to measure the text width.</param>
    /// <param name="text">Text whose rendered width is measured.</param>
    /// <param name="availableWidth">Maximum allowed width in scaled pixels. 0 or negative disables shrinking.</param>
    /// <param name="maxFontSize">Preferred (maximum) font size in scaled pixels.</param>
    /// <returns>Font size in scaled pixels, guaranteed to be &gt; 0.</returns>
    private static float FitFontSize(SKPaint paint, string text, float availableWidth, float maxFontSize)
    {
        paint.TextSize = maxFontSize;
        if (availableWidth <= 0 || string.IsNullOrEmpty(text))
        {
            return maxFontSize;
        }

        var measuredWidth = paint.MeasureText(text);
        if (measuredWidth <= availableWidth)
        {
            return maxFontSize;
        }

        // Scale font size proportionally so the text fits within the available width
        return maxFontSize * (availableWidth / measuredWidth);
    }

    /// <inheritdoc/>
    public string MediaType => "image/png";

    /// <inheritdoc/>
    public string DefaultExtension => ".png";

    /// <inheritdoc/>
    /// <remarks>
    /// The bitmap dimensions are derived from <see cref="LayoutTree.Width"/> and
    /// <see cref="LayoutTree.Height"/> scaled by <see cref="RenderOptions.Scale"/>,
    /// with a minimum of 1×1 pixels. The background is filled with
    /// <see cref="SKColors.White"/> before any nodes are drawn.
    /// </remarks>
    public void Render(LayoutTree layout, RenderOptions options, Stream output)
    {
        // Validate inputs — null arguments would produce silent failures
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        // Compute bitmap size, enforcing minimum 1×1 to prevent SKBitmap allocation errors
        var w = Math.Max(1, (int)Math.Ceiling(layout.Width * options.Scale));
        var h = Math.Max(1, (int)Math.Ceiling(layout.Height * options.Scale));

        // Allocate bitmap, canvas and render all nodes
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        // Fill the background with white before drawing diagram elements
        canvas.Clear(SKColors.White);

        foreach (var node in layout.Nodes)
        {
            RenderNode(canvas, node, options);
        }

        // Encode as PNG and write to the output stream
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(output);
    }

    /// <summary>
    /// Dispatches a single <see cref="LayoutNode"/> to the appropriate typed render method.
    /// Unknown concrete types are silently skipped so that future node types do not break
    /// existing callers.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="node">Node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderNode(SKCanvas canvas, LayoutNode node, RenderOptions options)
    {
        switch (node)
        {
            case LayoutBox box:
                RenderBox(canvas, box, options);
                break;

            case LayoutLine line:
                RenderLine(canvas, line, options);
                break;

            case LayoutLabel label:
                RenderLabel(canvas, label, options);
                break;

            case LayoutPort port:
                RenderPort(canvas, port, options);
                break;

            case LayoutBadge badge:
                RenderBadge(canvas, badge, options);
                break;

            case LayoutBand band:
                RenderBand(canvas, band, options);
                break;

            case LayoutLifeline lifeline:
                RenderLifeline(canvas, lifeline, options);
                break;

            case LayoutActivation activation:
                RenderActivation(canvas, activation, options);
                break;

            case LayoutGrid grid:
                RenderGrid(canvas, grid, options);
                break;

            default:
                // Skip unknown node types for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as a filled and stroked rectangle — plain or
    /// rounded depending on <see cref="BoxShape"/> — with an optional centered label,
    /// compartment dividers and rows, then recursively renders its children.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBox(SKCanvas canvas, LayoutBox box, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        var x = (float)(box.X * scale);
        var y = (float)(box.Y * scale);
        var rect = new SKRect(x, y, x + (float)(box.Width * scale), y + (float)(box.Height * scale));

        // Corner radius for RoundedRectangle: double the line corner radius for visual prominence
        var cornerR = (float)(theme.LineCornerRadius * 2.0 * scale);
        var isRounded = box.Shape == BoxShape.RoundedRectangle && cornerR > 0;

        // Fill the box with the theme color for this depth level
        var fillHex = theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count];
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColor.Parse(fillHex);
            fillPaint.Style = SKPaintStyle.Fill;
            if (isRounded)
            {
                canvas.DrawRoundRect(rect, cornerR, cornerR, fillPaint);
            }
            else
            {
                canvas.DrawRect(rect, fillPaint);
            }
        }

        // Draw the box border
        var strokeColor = SKColor.Parse(theme.StrokeColor);
        using (var strokePaint = new SKPaint())
        {
            strokePaint.Color = strokeColor;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
            if (isRounded)
            {
                canvas.DrawRoundRect(rect, cornerR, cornerR, strokePaint);
            }
            else
            {
                canvas.DrawRect(rect, strokePaint);
            }
        }

        // Draw the centered label in the title area if present
        if (box.Label != null)
        {
            using var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeTitle * scale, bold: true, italic: false);
            textPaint.TextAlign = SKTextAlign.Center;
            var textX = (float)((box.X + box.Width / 2.0) * scale);
            var availableWidth = (float)((box.Width - 2 * theme.LabelPadding) * scale);
            textPaint.TextSize = FitFontSize(textPaint, box.Label, availableWidth, textPaint.TextSize);
            var textY = (float)((box.Y + theme.LabelPadding + theme.FontSizeTitle) * scale);
            canvas.DrawText(box.Label, textX, textY, textPaint);
        }

        // Render compartments below the label area with horizontal dividers
        if (box.Compartments.Count > 0)
        {
            RenderBoxCompartments(canvas, box, options, strokeColor);
        }

        // Render children recursively
        foreach (var child in box.Children)
        {
            RenderNode(canvas, child, options);
        }
    }

    /// <summary>
    /// Renders the compartments of a <see cref="LayoutBox"/> below the title area.
    /// Each compartment begins with a full-width horizontal divider, followed by an optional
    /// bold title row and then zero or more body-font text rows, each indented by
    /// <see cref="Theme.LabelPadding"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box whose compartments are rendered.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="strokeColor">Pre-parsed stroke color reused across all compartment draws.</param>
    private static void RenderBoxCompartments(
        SKCanvas canvas,
        LayoutBox box,
        RenderOptions options,
        SKColor strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        // Compartments start below the label area (padding + font + padding when label present)
        var labelAreaHeight = box.Label != null
            ? theme.LabelPadding + theme.FontSizeTitle + theme.LabelPadding
            : 0.0;
        var compartmentY = box.Y + labelAreaHeight;

        foreach (var compartment in box.Compartments)
        {
            // Draw a full-width horizontal divider at the top of this compartment
            using (var divPaint = new SKPaint())
            {
                divPaint.Color = strokeColor;
                divPaint.Style = SKPaintStyle.Stroke;
                divPaint.StrokeWidth = (float)theme.StrokeWidth * scale;
                canvas.DrawLine(
                    (float)(box.X * scale),
                    (float)(compartmentY * scale),
                    (float)((box.X + box.Width) * scale),
                    (float)(compartmentY * scale),
                    divPaint);
            }

            // Draw the optional bold compartment title
            if (compartment.Title != null)
            {
                using var titlePaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: true, italic: true);
                titlePaint.TextAlign = SKTextAlign.Left;
                var titleX = (float)((box.X + theme.LabelPadding) * scale);
                var titleY = (float)((compartmentY + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(compartment.Title, titleX, titleY, titlePaint);
                compartmentY += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
            }

            // Draw each body row at body font size, left-aligned with LabelPadding indent
            foreach (var row in compartment.Rows)
            {
                using var rowPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: false, italic: false);
                rowPaint.TextAlign = SKTextAlign.Left;
                var rowX = (float)((box.X + theme.LabelPadding) * scale);
                var rowY = (float)((compartmentY + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(row, rowX, rowY, rowPaint);
                compartmentY += theme.LabelPadding + theme.FontSizeBody;
            }
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as a corner-radius-aware polyline, built from a
    /// single <see cref="SKPath"/> so that <c>CornerPathEffect</c> can round every bend
    /// uniformly. Arrowheads are drawn on top of the finished path. An optional midpoint
    /// label is centered over the line with a white background rectangle.
    /// </summary>
    /// <remarks>
    /// When <see cref="Theme.LineCornerRadius"/> is zero (e.g., the Print theme) the path is
    /// drawn with sharp corners. When both corner rounding and dashing are active, the two
    /// effects are composed so that the dash pattern follows the rounded path.
    /// </remarks>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="line">Line node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLine(SKCanvas canvas, LayoutLine line, RenderOptions options)
    {
        // Lines with fewer than 2 waypoints cannot be drawn
        if (line.Waypoints.Count < 2)
        {
            return;
        }

        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Build a single path through all waypoints for unified corner-effect rendering
        using var path = new SKPath();
        path.MoveTo((float)(line.Waypoints[0].X * scale), (float)(line.Waypoints[0].Y * scale));
        for (var i = 1; i < line.Waypoints.Count; i++)
        {
            path.LineTo((float)(line.Waypoints[i].X * scale), (float)(line.Waypoints[i].Y * scale));
        }

        using var paint = new SKPaint();
        paint.Color = strokeColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = (float)theme.StrokeWidth * scale;
        paint.IsAntialias = true;

        // Apply corner and/or dash path effects based on theme and line style
        var cornerRadius = (float)(theme.LineCornerRadius * scale);
        var hasDash = line.LineStyle != LineStyle.Solid;

        if (hasDash && cornerRadius > 0)
        {
            // Compose: apply corner rounding (inner effect) then dashing (outer effect)
            float[] intervals = line.LineStyle == LineStyle.Dashed
                ? [6f * scale, 3f * scale]
                : [2f * scale, 2f * scale];
            using var dash = SKPathEffect.CreateDash(intervals, 0);
            using var corner = SKPathEffect.CreateCorner(cornerRadius);
            paint.PathEffect = SKPathEffect.CreateCompose(dash, corner);
        }
        else if (hasDash)
        {
            float[] intervals = line.LineStyle == LineStyle.Dashed
                ? [6f * scale, 3f * scale]
                : [2f * scale, 2f * scale];
            paint.PathEffect = SKPathEffect.CreateDash(intervals, 0);
        }
        else if (cornerRadius > 0)
        {
            paint.PathEffect = SKPathEffect.CreateCorner(cornerRadius);
        }

        canvas.DrawPath(path, paint);

        // Draw source arrowhead at the first waypoint, direction pointing away from the line
        if (line.SourceArrowhead != ArrowheadStyle.None)
        {
            var tip = line.Waypoints[0];
            var next = line.Waypoints[1];
            var (dx, dy) = ComputeDirection(next.X, next.Y, tip.X, tip.Y);
            DrawArrowhead(
                canvas,
                (float)(tip.X * scale), (float)(tip.Y * scale),
                (float)dx, (float)dy,
                line.SourceArrowhead,
                new ArrowheadPaint(strokeColor, (float)theme.StrokeWidth * scale, scale));
        }

        // Draw target arrowhead at the last waypoint, direction pointing away from the line
        if (line.TargetArrowhead != ArrowheadStyle.None)
        {
            var n = line.Waypoints.Count;
            var tip = line.Waypoints[n - 1];
            var prev = line.Waypoints[n - 2];
            var (dx, dy) = ComputeDirection(prev.X, prev.Y, tip.X, tip.Y);
            DrawArrowhead(
                canvas,
                (float)(tip.X * scale), (float)(tip.Y * scale),
                (float)dx, (float)dy,
                line.TargetArrowhead,
                new ArrowheadPaint(strokeColor, (float)theme.StrokeWidth * scale, scale));
        }

        // Draw the optional midpoint label with a white background for readability
        if (line.MidpointLabel != null)
        {
            RenderLineMidpointLabel(canvas, line.Waypoints, line.MidpointLabel, theme, scale, strokeColor);
        }
    }

    /// <summary>
    /// Groups the visual paint parameters for arrowhead rendering, reducing the parameter
    /// count on <see cref="DrawArrowhead"/> to within the allowed limit.
    /// </summary>
    /// <param name="Color">Stroke and fill color for the arrowhead.</param>
    /// <param name="StrokeWidth">Stroke width applied to open (non-filled) arrowhead styles.</param>
    /// <param name="Scale">Uniform scale factor used to size the arrowhead relative to the diagram.</param>
    private readonly record struct ArrowheadPaint(SKColor Color, float StrokeWidth, float Scale);

    /// <summary>
    /// Draws an arrowhead of the specified style at a line endpoint.
    /// </summary>
    /// <remarks>
    /// The direction vector (<paramref name="dx"/>, <paramref name="dy"/>) must be a unit
    /// vector pointing from the line body toward the tip. A perpendicular vector is derived
    /// automatically to construct the wing points of triangle and diamond shapes.
    /// </remarks>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="tipX">Scaled X coordinate of the arrowhead tip.</param>
    /// <param name="tipY">Scaled Y coordinate of the arrowhead tip.</param>
    /// <param name="dx">X component of the normalized direction vector pointing toward the tip.</param>
    /// <param name="dy">Y component of the normalized direction vector pointing toward the tip.</param>
    /// <param name="style">Arrowhead style to draw; <see cref="ArrowheadStyle.None"/> is a no-op.</param>
    /// <param name="paint">Color, stroke width, and scale parameters for the arrowhead.</param>
    private static void DrawArrowhead(
        SKCanvas canvas,
        float tipX, float tipY,
        float dx, float dy,
        ArrowheadStyle style,
        ArrowheadPaint paint)
    {
        if (style == ArrowheadStyle.None)
        {
            return;
        }

        // Perpendicular direction (90° CCW rotation of the forward vector)
        var px = -dy;
        var py = dx;

        // Arrowhead sizing: length along the line, half-width across
        var arrowLen = 10f * paint.Scale;
        var halfW = 4f * paint.Scale;

        using var paintObj = new SKPaint();
        paintObj.Color = paint.Color;
        paintObj.IsAntialias = true;
        paintObj.StrokeWidth = paint.StrokeWidth;

        switch (style)
        {
            case ArrowheadStyle.Open:
                {
                    // Open (hollow) triangle: two stroke lines from wing points to the tip
                    paintObj.Style = SKPaintStyle.Stroke;
                    using var p = new SKPath();
                    p.MoveTo(tipX - dx * arrowLen + px * halfW, tipY - dy * arrowLen + py * halfW);
                    p.LineTo(tipX, tipY);
                    p.LineTo(tipX - dx * arrowLen - px * halfW, tipY - dy * arrowLen - py * halfW);
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case ArrowheadStyle.Filled:
                {
                    // Filled solid triangle pointing toward the tip
                    paintObj.Style = SKPaintStyle.Fill;
                    using var p = new SKPath();
                    p.MoveTo(tipX, tipY);
                    p.LineTo(tipX - dx * arrowLen + px * halfW, tipY - dy * arrowLen + py * halfW);
                    p.LineTo(tipX - dx * arrowLen - px * halfW, tipY - dy * arrowLen - py * halfW);
                    p.Close();
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case ArrowheadStyle.Diamond:
                {
                    // Open diamond: four-point polygon straddling the line end
                    paintObj.Style = SKPaintStyle.Stroke;
                    using var p = new SKPath();
                    p.MoveTo(tipX, tipY);
                    p.LineTo(tipX - dx * (arrowLen / 2f) + px * halfW, tipY - dy * (arrowLen / 2f) + py * halfW);
                    p.LineTo(tipX - dx * arrowLen, tipY - dy * arrowLen);
                    p.LineTo(tipX - dx * (arrowLen / 2f) - px * halfW, tipY - dy * (arrowLen / 2f) - py * halfW);
                    p.Close();
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case ArrowheadStyle.FilledDiamond:
                {
                    // Filled diamond
                    paintObj.Style = SKPaintStyle.Fill;
                    using var p = new SKPath();
                    p.MoveTo(tipX, tipY);
                    p.LineTo(tipX - dx * (arrowLen / 2f) + px * halfW, tipY - dy * (arrowLen / 2f) + py * halfW);
                    p.LineTo(tipX - dx * arrowLen, tipY - dy * arrowLen);
                    p.LineTo(tipX - dx * (arrowLen / 2f) - px * halfW, tipY - dy * (arrowLen / 2f) - py * halfW);
                    p.Close();
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case ArrowheadStyle.Circle:
                {
                    // Open circle whose near edge touches the tip; center is pulled back by one radius
                    paintObj.Style = SKPaintStyle.Stroke;
                    var r = 4f * paint.Scale;
                    canvas.DrawCircle(tipX - dx * r, tipY - dy * r, r, paintObj);
                    break;
                }

            case ArrowheadStyle.Bar:
                {
                    // Perpendicular bar centered on the tip
                    paintObj.Style = SKPaintStyle.Stroke;
                    var barHalf = 6f * paint.Scale;
                    canvas.DrawLine(
                        tipX + px * barHalf, tipY + py * barHalf,
                        tipX - px * barHalf, tipY - py * barHalf,
                        paintObj);
                    break;
                }

            default:
                // Unknown styles are treated as None
                break;
        }
    }

    /// <summary>
    /// Computes a normalized direction unit vector from (<paramref name="fromX"/>,
    /// <paramref name="fromY"/>) toward (<paramref name="toX"/>, <paramref name="toY"/>).
    /// Returns (1, 0) as a safe fallback when the two points coincide.
    /// </summary>
    /// <param name="fromX">X coordinate of the source point.</param>
    /// <param name="fromY">Y coordinate of the source point.</param>
    /// <param name="toX">X coordinate of the target point.</param>
    /// <param name="toY">Y coordinate of the target point.</param>
    /// <returns>Normalized (Dx, Dy) direction tuple.</returns>
    private static (double Dx, double Dy) ComputeDirection(
        double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        return len < 0.001 ? (1.0, 0.0) : (dx / len, dy / len);
    }

    /// <summary>
    /// Renders a text label centered at the midpoint of a polyline, with a white background
    /// rectangle drawn first to ensure readability over the line stroke.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="waypoints">Ordered waypoints of the line; must contain at least one entry.</param>
    /// <param name="label">Label text to render.</param>
    /// <param name="theme">Theme providing font size and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="strokeColor">Color used for the label text.</param>
    private static void RenderLineMidpointLabel(
        SKCanvas canvas,
        IReadOnlyList<Point2D> waypoints,
        string label,
        Theme theme,
        float scale,
        SKColor strokeColor)
    {
        // Compute the geometric midpoint of the waypoints list
        var (midX, midY) = ComputeLineMidpoint(waypoints);
        var scaledX = (float)(midX * scale);
        var scaledY = (float)(midY * scale);

        using var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: false, italic: false);
        textPaint.TextAlign = SKTextAlign.Center;

        // Measure the text so the background rectangle fits snugly around it
        var textWidth = textPaint.MeasureText(label);
        var textHeight = (float)theme.FontSizeBody * scale;
        var padding = (float)theme.LabelPadding * scale * 0.5f;
        var bgRect = new SKRect(
            scaledX - textWidth / 2f - padding,
            scaledY - textHeight - padding,
            scaledX + textWidth / 2f + padding,
            scaledY + padding);

        using (var bgPaint = new SKPaint())
        {
            bgPaint.Color = SKColors.White;
            bgPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(bgRect, bgPaint);
        }

        canvas.DrawText(label, scaledX, scaledY, textPaint);
    }

    /// <summary>
    /// Computes the geometric midpoint of an ordered waypoint list. For an odd number of
    /// waypoints the center element is returned; for an even count the average of the two
    /// center elements is returned.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints; must contain at least one entry.</param>
    /// <returns>The (X, Y) coordinates of the midpoint in logical pixels.</returns>
    private static (double X, double Y) ComputeLineMidpoint(IReadOnlyList<Point2D> waypoints)
    {
        var n = waypoints.Count;
        if (n % 2 == 1)
        {
            // Odd: middle element is the exact midpoint
            return (waypoints[n / 2].X, waypoints[n / 2].Y);
        }

        // Even: average the two center elements
        var lo = waypoints[n / 2 - 1];
        var hi = waypoints[n / 2];
        return ((lo.X + hi.X) / 2.0, (lo.Y + hi.Y) / 2.0);
    }

    /// <summary>
    /// Renders a <see cref="LayoutLabel"/> as a text element at its absolute position.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="label">Label node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLabel(SKCanvas canvas, LayoutLabel label, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        using var paint = CreateTextPaint(
            SKColor.Parse(theme.StrokeColor),
            (float)label.FontSize * scale,
            bold: label.Weight == FontWeight.Bold,
            italic: label.Style == FontStyle.Italic);
        paint.TextAlign = label.Align switch
        {
            TextAlign.Center => SKTextAlign.Center,
            TextAlign.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };

        var availableWidth = (float)(label.MaxWidth * scale);
        paint.TextSize = FitFontSize(paint, label.Text, availableWidth, paint.TextSize);

        canvas.DrawText(label.Text, (float)(label.X * scale), (float)(label.Y * scale), paint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutPort"/> as a small (8×8 logical pixels) filled square
    /// centered at the port position. When a label is present it is offset away from the
    /// edge the port is attached to, ensuring it does not overlap with the host box.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="port">Port node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderPort(SKCanvas canvas, LayoutPort port, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Port square: 8×8 logical pixels, centered at (CentreX, CentreY)
        const double PortHalfSize = 4.0;
        var portRect = new SKRect(
            (float)((port.CentreX - PortHalfSize) * scale),
            (float)((port.CentreY - PortHalfSize) * scale),
            (float)((port.CentreX + PortHalfSize) * scale),
            (float)((port.CentreY + PortHalfSize) * scale));

        // Ports are conventionally drawn as filled squares using the stroke color
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = strokeColor;
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(portRect, fillPaint);
        }

        // Draw the optional label offset away from the attached edge
        if (port.Label != null)
        {
            // Offset the label far enough from the port square so it does not overlap
            var offset = PortHalfSize + theme.LabelPadding;
            var (labelX, labelY, align) = port.Side switch
            {
                PortSide.Top => (port.CentreX, port.CentreY - offset, SKTextAlign.Center),
                PortSide.Bottom => (port.CentreX, port.CentreY + offset + theme.FontSizeBody, SKTextAlign.Center),
                PortSide.Left => (port.CentreX - offset, port.CentreY + theme.FontSizeBody / 2.0, SKTextAlign.Right),
                _ => (port.CentreX + offset, port.CentreY + theme.FontSizeBody / 2.0, SKTextAlign.Left)
            };

            using var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: false, italic: false);
            textPaint.TextAlign = align;
            canvas.DrawText(port.Label, (float)(labelX * scale), (float)(labelY * scale), textPaint);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBadge"/> as the specified icon shape centered at the badge
    /// position. An optional label is drawn to the right of the bounding circle.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="badge">Badge node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBadge(SKCanvas canvas, LayoutBadge badge, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        var cx = (float)(badge.CentreX * scale);
        var cy = (float)(badge.CentreY * scale);
        var r = (float)(badge.Size / 2.0 * scale);

        using var strokePaint = new SKPaint();
        strokePaint.Color = strokeColor;
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
        strokePaint.IsAntialias = true;

        using var fillPaint = new SKPaint();
        fillPaint.Color = strokeColor;
        fillPaint.Style = SKPaintStyle.Fill;
        fillPaint.IsAntialias = true;

        // Draw the badge shape centered at (cx, cy) within a bounding circle of radius r
        switch (badge.Shape)
        {
            case BadgeShape.FilledCircle:
                canvas.DrawCircle(cx, cy, r, fillPaint);
                break;

            case BadgeShape.Bullseye:
                {
                    // Outer filled circle + white inner circle to create a visible ring
                    canvas.DrawCircle(cx, cy, r, fillPaint);
                    using var innerWhite = new SKPaint();
                    innerWhite.Color = SKColors.White;
                    innerWhite.Style = SKPaintStyle.Fill;
                    innerWhite.IsAntialias = true;
                    canvas.DrawCircle(cx, cy, r / 3f, innerWhite);
                    canvas.DrawCircle(cx, cy, r / 3f, strokePaint);
                    break;
                }

            case BadgeShape.Diamond:
                {
                    // Open rotated-square diamond with vertices at the compass cardinal points
                    using var p = new SKPath();
                    p.MoveTo(cx, cy - r);       // top
                    p.LineTo(cx + r, cy);       // right
                    p.LineTo(cx, cy + r);       // bottom
                    p.LineTo(cx - r, cy);       // left
                    p.Close();
                    canvas.DrawPath(p, strokePaint);
                    break;
                }

            case BadgeShape.HorizontalBar:
                canvas.DrawLine(cx - r * 0.8f, cy, cx + r * 0.8f, cy, strokePaint);
                break;

            case BadgeShape.VerticalBar:
                canvas.DrawLine(cx, cy - r * 0.8f, cx, cy + r * 0.8f, strokePaint);
                break;

            default:
                // Unknown badge shapes are skipped for forward compatibility
                break;
        }

        // Draw the optional label to the right of the bounding circle
        if (badge.Label != null)
        {
            using var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: false, italic: false);
            textPaint.TextAlign = SKTextAlign.Left;
            var labelX = (float)((badge.CentreX + badge.Size / 2.0 + theme.LabelPadding) * scale);
            var labelY = (float)((badge.CentreY + theme.FontSizeBody / 2.0) * scale);
            canvas.DrawText(badge.Label, labelX, labelY, textPaint);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBand"/> as a swim-lane rectangle with an optional label.
    /// For Horizontal bands the label is rendered vertically (rotated 90° CCW) along the
    /// left edge; for Vertical bands it is rendered horizontally at the top. Children are
    /// rendered recursively.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="band">Band node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBand(SKCanvas canvas, LayoutBand band, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        var rect = new SKRect(
            (float)(band.X * scale),
            (float)(band.Y * scale),
            (float)((band.X + band.Width) * scale),
            (float)((band.Y + band.Height) * scale));

        // Fill band with the primary (depth-0) background color
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColor.Parse(theme.DepthFillColors[0]);
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, fillPaint);
        }

        // Draw the band border
        using (var strokePaint = new SKPaint())
        {
            strokePaint.Color = strokeColor;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
            canvas.DrawRect(rect, strokePaint);
        }

        // Draw the optional label; position and rotation depends on band orientation
        if (band.Label != null)
        {
            using var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: false, italic: false);
            textPaint.TextAlign = SKTextAlign.Center;

            if (band.Orientation == BandOrientation.Horizontal)
            {
                // Vertical text on the left edge: translate to label center, rotate CCW
                var labelCx = (float)((band.X + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale);
                var labelCy = (float)((band.Y + band.Height / 2.0) * scale);
                canvas.Save();
                canvas.Translate(labelCx, labelCy);
                canvas.RotateDegrees(-90);
                canvas.DrawText(band.Label, 0, 0, textPaint);
                canvas.Restore();
            }
            else
            {
                // Horizontal text at the top of the band
                var textX = (float)((band.X + band.Width / 2.0) * scale);
                var textY = (float)((band.Y + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(band.Label, textX, textY, textPaint);
            }
        }

        // Render children recursively
        foreach (var child in band.Children)
        {
            RenderNode(canvas, child, options);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLifeline"/> as a header box centered at
    /// <see cref="LayoutLifeline.CentreX"/> containing the lifeline label, followed by a
    /// dashed vertical stem running from the bottom of the header to
    /// <see cref="LayoutLifeline.BottomY"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="lifeline">Lifeline node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLifeline(SKCanvas canvas, LayoutLifeline lifeline, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Header box: centered at CentreX, top edge at TopY
        var headerLeft = lifeline.CentreX - lifeline.HeaderWidth / 2.0;
        var headerRect = new SKRect(
            (float)(headerLeft * scale),
            (float)(lifeline.TopY * scale),
            (float)((headerLeft + lifeline.HeaderWidth) * scale),
            (float)((lifeline.TopY + lifeline.HeaderHeight) * scale));

        // Fill header with the primary background color
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColor.Parse(theme.DepthFillColors[0]);
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(headerRect, fillPaint);
        }

        // Draw header border
        using (var strokePaint = new SKPaint())
        {
            strokePaint.Color = strokeColor;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
            canvas.DrawRect(headerRect, strokePaint);
        }

        // Draw the header label centered within the header box
        using (var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: true, italic: false))
        {
            textPaint.TextAlign = SKTextAlign.Center;
            var textX = (float)(lifeline.CentreX * scale);
            var textY = (float)((lifeline.TopY + (lifeline.HeaderHeight + theme.FontSizeBody) / 2.0) * scale);
            canvas.DrawText(lifeline.Label, textX, textY, textPaint);
        }

        // Dashed vertical stem from the bottom of the header box to BottomY
        using var stemPaint = new SKPaint();
        stemPaint.Color = strokeColor;
        stemPaint.Style = SKPaintStyle.Stroke;
        stemPaint.StrokeWidth = (float)theme.StrokeWidth * scale;
        stemPaint.IsAntialias = true;
        stemPaint.PathEffect = SKPathEffect.CreateDash([6f * scale, 3f * scale], 0);

        var stemX = (float)(lifeline.CentreX * scale);
        canvas.DrawLine(
            stemX, (float)((lifeline.TopY + lifeline.HeaderHeight) * scale),
            stemX, (float)(lifeline.BottomY * scale),
            stemPaint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutActivation"/> as a narrow white-filled rectangle with a
    /// stroke border, centered horizontally at <see cref="LayoutActivation.CentreX"/>.
    /// </summary>
    /// <remarks>
    /// The activation bar width is <c>Theme.LabelPadding * 2</c>, giving it a size that
    /// scales proportionally with the diagram's text padding setting.
    /// </remarks>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="activation">Activation node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderActivation(SKCanvas canvas, LayoutActivation activation, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Bar width = LabelPadding * 2, centered at CentreX
        var halfWidth = theme.LabelPadding;
        var rect = new SKRect(
            (float)((activation.CentreX - halfWidth) * scale),
            (float)(activation.TopY * scale),
            (float)((activation.CentreX + halfWidth) * scale),
            (float)(activation.BottomY * scale));

        // White fill indicates the lifeline is active during this time interval
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColors.White;
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, fillPaint);
        }

        // Stroke border delineates the bar from surrounding elements
        using var strokePaint = new SKPaint();
        strokePaint.Color = strokeColor;
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
        canvas.DrawRect(rect, strokePaint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutGrid"/> as a bordered table. Header rows are filled with
    /// the depth-1 theme color; body rows use the depth-0 color. Each cell's text is
    /// aligned according to <see cref="LayoutGridCell.Align"/> and vertically centered
    /// within the row height.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="grid">Grid node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderGrid(SKCanvas canvas, LayoutGrid grid, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Header rows use depth-1 color; body rows use depth-0 color
        var headerFill = SKColor.Parse(theme.DepthFillColors[1 % theme.DepthFillColors.Count]);
        var bodyFill = SKColor.Parse(theme.DepthFillColors[0]);

        // Accumulate Y position across rows; X resets at the start of each row
        var currentY = grid.Y;
        foreach (var row in grid.Rows)
        {
            // Row height = maximum cell height in this row
            var rowHeight = 0.0;
            foreach (var cell in row.Cells)
            {
                rowHeight = Math.Max(rowHeight, cell.Height);
            }

            var currentX = grid.X;
            foreach (var cell in row.Cells)
            {
                var cellRect = new SKRect(
                    (float)(currentX * scale),
                    (float)(currentY * scale),
                    (float)((currentX + cell.Width) * scale),
                    (float)((currentY + rowHeight) * scale));

                // Fill cell with header or body background
                using (var fillPaint = new SKPaint())
                {
                    fillPaint.Color = row.IsHeader ? headerFill : bodyFill;
                    fillPaint.Style = SKPaintStyle.Fill;
                    canvas.DrawRect(cellRect, fillPaint);
                }

                // Draw the cell border
                using (var borderPaint = new SKPaint())
                {
                    borderPaint.Color = strokeColor;
                    borderPaint.Style = SKPaintStyle.Stroke;
                    borderPaint.StrokeWidth = (float)theme.StrokeWidth * scale;
                    canvas.DrawRect(cellRect, borderPaint);
                }

                // Draw cell text, horizontally aligned per cell spec and vertically centered
                using (var textPaint = CreateTextPaint(strokeColor, (float)theme.FontSizeBody * scale, bold: row.IsHeader, italic: false))
                {
                    textPaint.TextAlign = cell.Align switch
                    {
                        TextAlign.Center => SKTextAlign.Center,
                        TextAlign.Right => SKTextAlign.Right,
                        _ => SKTextAlign.Left
                    };

                    var textX = cell.Align switch
                    {
                        TextAlign.Center => currentX + cell.Width / 2.0,
                        TextAlign.Right => currentX + cell.Width - theme.LabelPadding,
                        _ => currentX + theme.LabelPadding
                    };

                    // Vertically center the baseline within the row
                    var textY = currentY + (rowHeight + theme.FontSizeBody) / 2.0;
                    canvas.DrawText(cell.Text, (float)(textX * scale), (float)(textY * scale), textPaint);
                }

                currentX += cell.Width;
            }

            currentY += rowHeight;
        }
    }
}

