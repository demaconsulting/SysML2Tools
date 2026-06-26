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
/// - <see cref="LayoutBox"/> → filled rectangle + optional centered label; children rendered
///   recursively.
/// - <see cref="LayoutLine"/> → polyline stroke.
/// - <see cref="LayoutLabel"/> → text element.
/// - All other node types are skipped for forward compatibility.
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
    /// Renders a single <see cref="LayoutNode"/> to the SkiaSharp canvas.
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

            default:
                // Skip unknown node types for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as a filled and stroked rectangle with an optional
    /// centered label, then recursively renders its children.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBox(SKCanvas canvas, LayoutBox box, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        var rect = new SKRect(
            (float)(box.X * options.Scale),
            (float)(box.Y * options.Scale),
            (float)((box.X + box.Width) * options.Scale),
            (float)((box.Y + box.Height) * options.Scale));

        // Fill the box with the theme color for this depth level
        var fillHex = theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count];
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColor.Parse(fillHex);
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, fillPaint);
        }

        // Draw the box border
        var strokeColor = SKColor.Parse(theme.StrokeColor);
        using (var strokePaint = new SKPaint())
        {
            strokePaint.Color = strokeColor;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
            canvas.DrawRect(rect, strokePaint);
        }

        // Draw the centered label if present
        if (box.Label != null)
        {
            using var textPaint = new SKPaint();
            textPaint.Color = strokeColor;
            textPaint.TextSize = (float)theme.FontSizeTitle * scale;
            textPaint.IsAntialias = true;
            textPaint.TextAlign = SKTextAlign.Center;
            var textX = (float)((box.X + box.Width / 2.0) * options.Scale);
            var textY = (float)((box.Y + theme.LabelPadding + theme.FontSizeTitle) * options.Scale);
            canvas.DrawText(box.Label, textX, textY, textPaint);
        }

        // Render children recursively
        foreach (var child in box.Children)
        {
            RenderNode(canvas, child, options);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as a series of connected line segments.
    /// </summary>
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

        using var paint = new SKPaint();
        paint.Color = strokeColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = (float)theme.StrokeWidth * scale;
        paint.IsAntialias = true;

        // Configure dashing for non-solid line styles
        if (line.LineStyle == LineStyle.Dashed)
        {
            paint.PathEffect = SKPathEffect.CreateDash([6f * scale, 3f * scale], 0);
        }
        else if (line.LineStyle == LineStyle.Dotted)
        {
            paint.PathEffect = SKPathEffect.CreateDash([2f * scale, 2f * scale], 0);
        }

        // Draw each segment between consecutive waypoints
        for (var i = 0; i < line.Waypoints.Count - 1; i++)
        {
            var from = line.Waypoints[i];
            var to = line.Waypoints[i + 1];
            canvas.DrawLine(
                (float)(from.X * options.Scale),
                (float)(from.Y * options.Scale),
                (float)(to.X * options.Scale),
                (float)(to.Y * options.Scale),
                paint);
        }
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

        using var paint = new SKPaint();
        paint.Color = SKColor.Parse(theme.StrokeColor);
        paint.TextSize = (float)theme.FontSizeBody * scale;
        paint.IsAntialias = true;
        paint.TextAlign = label.Align switch
        {
            TextAlign.Center => SKTextAlign.Center,
            TextAlign.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };

        canvas.DrawText(label.Text, (float)(label.X * options.Scale), (float)(label.Y * options.Scale), paint);
    }
}

