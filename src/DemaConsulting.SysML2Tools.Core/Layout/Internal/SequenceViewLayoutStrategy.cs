// <copyright file="SequenceViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Sequence View diagrams. Renders the participating lifelines as vertical
/// dashed stems with header boxes and draws each message as a horizontal arrow between lifelines,
/// ordered top-to-bottom by declaration order.
/// </summary>
/// <remarks>
/// Lifelines are the distinct participants referenced by the messages' <c>from</c>/<c>to</c> events
/// (the first segment of each reference). Layout is pure arithmetic: lifeline X is the column index
/// times a pitch, and message Y is the message ordinal times a row pitch.
/// </remarks>
internal sealed class SequenceViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Minimum horizontal pitch between adjacent lifelines.</summary>
    private const double MinPitch = 140.0;

    /// <summary>A message between two lifelines with an optional label.</summary>
    private sealed record MessageItem(int From, int To, string Label);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        var root = FindRoot(context.Workspace);
        if (root is null)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var (lifelines, index) = CollectLifelines(root);
        var messages = ResolveMessages(root, index);
        if (lifelines.Count == 0 || messages.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var headerHeight = theme.FontSizeTitle + (2.0 * theme.LabelPadding);
        var pitch = ComputePitch(lifelines, theme);
        var rowPitch = theme.FontSizeTitle * 2.5;
        var margin = theme.LabelPadding * 3.0;
        var headerWidth = pitch - (theme.LabelPadding * 4.0);

        var firstMessageY = margin + headerHeight + rowPitch;
        var bottomY = firstMessageY + (messages.Count * rowPitch);

        var centreX = new double[lifelines.Count];
        for (var i = 0; i < lifelines.Count; i++)
        {
            centreX[i] = margin + (headerWidth / 2.0) + (i * pitch);
        }

        var nodes = new List<LayoutNode>();

        // Lifelines.
        for (var i = 0; i < lifelines.Count; i++)
        {
            nodes.Add(new LayoutLifeline(
                CentreX: centreX[i],
                TopY: margin,
                BottomY: bottomY,
                Label: lifelines[i],
                HeaderWidth: headerWidth,
                HeaderHeight: headerHeight));
        }

        // Messages as horizontal arrows, ordered top-to-bottom.
        for (var m = 0; m < messages.Count; m++)
        {
            var msg = messages[m];
            var y = firstMessageY + (m * rowPitch);
            if (msg.From == msg.To)
            {
                nodes.Add(BuildSelfMessage(centreX[msg.From], y, theme, msg.Label));
                continue;
            }

            nodes.Add(new LayoutLine(
                Waypoints: [new Point2D(centreX[msg.From], y), new Point2D(centreX[msg.To], y)],
                SourceArrowhead: ArrowheadStyle.None,
                TargetArrowhead: ArrowheadStyle.Filled,
                LineStyle: LineStyle.Solid,
                MidpointLabel: msg.Label.Length > 0 ? msg.Label : null));
        }

        var width = margin + (lifelines.Count * pitch);
        var height = bottomY + margin;
        return new LayoutTree(width, height, nodes);
    }

    /// <summary>Finds the definition with the most messages to use as the diagram root.</summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace)
    {
        SysmlDefinitionNode? best = null;
        var bestMessages = 0;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            if (node is not SysmlDefinitionNode def)
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            var messages = def.Children.OfType<SysmlConnectionNode>().Count(c => c.ConnectionKeyword == "message");
            if (messages > bestMessages)
            {
                best = def;
                bestMessages = messages;
            }
        }

        return best;
    }

    /// <summary>
    /// Collects the lifelines participating in the root's messages — the distinct first segments of
    /// the message from/to references — in first-appearance order.
    /// </summary>
    private static (IReadOnlyList<string> Lifelines, Dictionary<string, int> Index) CollectLifelines(SysmlDefinitionNode root)
    {
        var lifelines = new List<string>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(string? reference)
        {
            var name = FirstSegment(reference);
            if (name is null || index.ContainsKey(name))
            {
                return;
            }

            index[name] = lifelines.Count;
            lifelines.Add(name);
        }

        foreach (var message in root.Children.OfType<SysmlConnectionNode>().Where(c => c.ConnectionKeyword == "message"))
        {
            Add(message.EndpointA);
            Add(message.EndpointB);
        }

        return (lifelines, index);
    }

    /// <summary>Resolves the root's messages to lifeline indices, preserving declaration order.</summary>
    private static IReadOnlyList<MessageItem> ResolveMessages(SysmlDefinitionNode root, Dictionary<string, int> index)
    {
        var result = new List<MessageItem>();
        foreach (var message in root.Children.OfType<SysmlConnectionNode>().Where(c => c.ConnectionKeyword == "message"))
        {
            var from = FirstSegment(message.EndpointA);
            var to = FirstSegment(message.EndpointB);
            if (from is null || to is null ||
                !index.TryGetValue(from, out var fi) || !index.TryGetValue(to, out var ti))
            {
                continue;
            }

            result.Add(new MessageItem(fi, ti, message.Name ?? string.Empty));
        }

        return result;
    }

    /// <summary>Computes the horizontal pitch between lifelines from the widest label.</summary>
    private static double ComputePitch(IReadOnlyList<string> lifelines, Theme theme)
    {
        var maxLabel = 0.0;
        foreach (var lifeline in lifelines)
        {
            maxLabel = Math.Max(maxLabel, lifeline.Length * theme.FontSizeBody * CharWidthFactor);
        }

        return Math.Max(MinPitch, maxLabel + (theme.LabelPadding * 8.0));
    }

    /// <summary>Builds a small self-message loop on a single lifeline.</summary>
    private static LayoutLine BuildSelfMessage(double centreX, double y, Theme theme, string label)
    {
        var loop = theme.FontSizeTitle;
        var waypoints = new List<Point2D>
        {
            new(centreX, y),
            new(centreX + (loop * 1.5), y),
            new(centreX + (loop * 1.5), y + loop),
            new(centreX, y + loop),
        };

        return new LayoutLine(
            Waypoints: waypoints,
            SourceArrowhead: ArrowheadStyle.None,
            TargetArrowhead: ArrowheadStyle.Filled,
            LineStyle: LineStyle.Solid,
            MidpointLabel: label.Length > 0 ? label : null);
    }

    /// <summary>Returns the first dot-separated segment of a reference, or null.</summary>
    private static string? FirstSegment(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var dot = reference.IndexOf('.', StringComparison.Ordinal);
        return dot >= 0 ? reference[..dot] : reference;
    }
}
