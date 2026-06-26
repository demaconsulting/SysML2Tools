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
/// - <see cref="LayoutBox"/> → <c>&lt;rect&gt;</c> + optional <c>&lt;text&gt;</c> for the label;
///   children are rendered recursively.
/// - <see cref="LayoutLine"/> → <c>&lt;path&gt;</c> with M/L commands; arrowheads are rendered
///   as marker references defined in <c>&lt;defs&gt;</c>.
/// - <see cref="LayoutLabel"/> → <c>&lt;text&gt;</c>.
/// - All other node types are skipped for forward compatibility.
///
/// Fill colors are derived from <see cref="Theme.DepthFillColors"/> using modulo wrapping on
/// <see cref="LayoutBox.Depth"/>.
/// </remarks>
public sealed class SvgRenderer : IRenderer
{
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

        // Write defs section with arrowhead markers
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
    /// Writes the <c>&lt;defs&gt;</c> block containing all arrowhead marker definitions.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="theme">Theme providing stroke color and width.</param>
    private static void WriteArrowheadDefs(StringBuilder sb, Theme theme)
    {
        sb.AppendLine("  <defs>");

        // Open arrowhead marker — used for specialization / generalization lines
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-open" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <polygon points="0 0, 10 3.5, 0 7" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine("    </marker>");

        // Filled arrowhead marker — used for dependency / usage lines
        sb.Append(CultureInfo.InvariantCulture,
            $"""    <marker id="arrowhead-filled" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"""      <polygon points="0 0, 10 3.5, 0 7" fill="{theme.StrokeColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
        sb.AppendLine("    </marker>");

        sb.AppendLine("  </defs>");
    }

    /// <summary>
    /// Renders a single <see cref="LayoutNode"/> to the SVG string builder.
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

            default:
                // Skip unknown node types for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as an SVG <c>&lt;rect&gt;</c> with an optional
    /// label and recursively renders its children.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">The box node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font size.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        // Derive fill color from theme using depth modulo wrapping
        var fillColor = theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count];

        var x = box.X * scale;
        var y = box.Y * scale;
        var w = box.Width * scale;
        var h = box.Height * scale;

        // Draw the rectangle
        sb.Append(CultureInfo.InvariantCulture,
            $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();

        // Draw the label if present
        if (box.Label != null)
        {
            var textX = (box.X + box.Width / 2.0) * scale;
            var textY = (box.Y + theme.LabelPadding + theme.FontSizeTitle / 2.0) * scale;
            sb.Append(CultureInfo.InvariantCulture,
                $"""  <text x="{F(textX)}" y="{F(textY)}" font-family="Segoe UI, sans-serif" font-size="{F(theme.FontSizeTitle * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(box.Label)}</text>""");
            sb.AppendLine();
        }

        // Render children recursively
        foreach (var child in box.Children)
        {
            RenderNode(sb, child, theme, scale);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as an SVG <c>&lt;path&gt;</c> element with optional
    /// arrowhead markers.
    /// </summary>
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

        // Build SVG path data from waypoints
        var pathData = new StringBuilder();
        var first = line.Waypoints[0];
        pathData.Append(CultureInfo.InvariantCulture, $"M {F(first.X * scale)} {F(first.Y * scale)}");
        for (var i = 1; i < line.Waypoints.Count; i++)
        {
            var wp = line.Waypoints[i];
            pathData.Append(CultureInfo.InvariantCulture, $" L {F(wp.X * scale)} {F(wp.Y * scale)}");
        }

        // Determine arrowhead markers
        var markerStart = line.SourceArrowhead switch
        {
            ArrowheadStyle.Open => " marker-start=\"url(#arrowhead-open)\"",
            ArrowheadStyle.Filled => " marker-start=\"url(#arrowhead-filled)\"",
            _ => string.Empty
        };
        var markerEnd = line.TargetArrowhead switch
        {
            ArrowheadStyle.Open => " marker-end=\"url(#arrowhead-open)\"",
            ArrowheadStyle.Filled => " marker-end=\"url(#arrowhead-filled)\"",
            _ => string.Empty
        };

        // Determine stroke style
        var dashArray = line.LineStyle switch
        {
            LineStyle.Dashed => " stroke-dasharray=\"6 3\"",
            LineStyle.Dotted => " stroke-dasharray=\"2 2\"",
            _ => string.Empty
        };

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <path d="{pathData}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{markerStart}{markerEnd}{dashArray}/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutLabel"/> as an SVG <c>&lt;text&gt;</c> element.
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
            TextAlign.Center => "middle",
            TextAlign.Right => "end",
            _ => "start"
        };

        sb.Append(CultureInfo.InvariantCulture,
            $"""  <text x="{F(x)}" y="{F(y)}" font-family="Segoe UI, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle">{EscapeXml(label.Text)}</text>""");
        sb.AppendLine();
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

