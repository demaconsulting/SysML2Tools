// <copyright file="SequenceViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

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

        var scope = ExposeScopeResolver.ResolveExposedScope(context.Workspace, context.ViewNode);

        var root = FindRoot(context.Workspace, scope);
        if (root is null)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        var (lifelines, index) = CollectLifelines(root, scope);
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
                SourceEnd: EndMarkerStyle.None,
                TargetEnd: EndMarkerStyle.OpenChevron,
                LineStyle: LineStyle.Solid,
                MidpointLabel: msg.Label.Length > 0 ? msg.Label : null));
        }

        var width = margin + (lifelines.Count * pitch);
        var height = bottomY + margin;
        return new LayoutTree(width, height, nodes);
    }

    /// <summary>
    /// Finds the definition with the most messages to use as the diagram root. When
    /// <paramref name="scope"/> is non-null (the view's resolved <c>expose</c> containment-subtree
    /// scope), candidates are first restricted to those relevant to the scope via
    /// <see cref="ExposeScopeResolver.IsRootRelevantToScope"/>; because a nested definition and its
    /// ancestor can both be scope-relevant, ties among relevant candidates are then broken by
    /// specificity (deepest/longest qualified name wins) via
    /// <see cref="ExposeScopeResolver.IsMoreSpecificCandidate"/>, with the message-count heuristic
    /// used only to break ties between equally specific candidates. When no candidate is
    /// scope-relevant, no root is chosen (an empty canvas results). When <paramref name="scope"/> is
    /// <see langword="null"/>, selection is the plain message-count heuristic, unchanged.
    /// </summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace, ExposedScope? scope)
    {
        SysmlDefinitionNode? best = null;
        string? bestQualifiedName = null;
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

            if (scope is not null && !ExposeScopeResolver.IsRootRelevantToScope(qualifiedName, scope))
            {
                continue;
            }

            var messages = def.Children.OfType<SysmlConnectionNode>().Count(c => c.ConnectionKeyword == "message");
            var scoreBetter = messages > bestMessages;
            var isBetter = scope is not null
                ? ExposeScopeResolver.IsMoreSpecificCandidate(qualifiedName, bestQualifiedName, scoreBetter)
                : scoreBetter;

            if (isBetter)
            {
                best = def;
                bestQualifiedName = qualifiedName;
                bestMessages = messages;
            }
        }

        return best;
    }

    /// <summary>
    /// Collects the lifelines participating in the root's messages — the distinct first segments of
    /// the message from/to references — in first-appearance order. When <paramref name="scope"/> is
    /// non-null, a lifeline is skipped when its reconstructed absolute qualified name
    /// (<c>"{root.QualifiedName}::{lifelineName}"</c> — a message-endpoint lifeline name is the first
    /// dotted segment of an endpoint reference, which names a feature declared directly under
    /// <paramref name="root"/>, confirmed against real declared features in
    /// <c>client-server-sequence.sysml</c>) fails <see cref="ExposeScopeResolver.IsInSubjectScope"/>.
    /// When <paramref name="root"/> has no <c>QualifiedName</c> (defensive; every workspace
    /// declaration carries one), the reconstruction is skipped and lifeline scoping does not apply,
    /// so the strategy never filters based on an unreliable name.
    /// </summary>
    private static (IReadOnlyList<string> Lifelines, Dictionary<string, int> Index) CollectLifelines(
        SysmlDefinitionNode root,
        ExposedScope? scope)
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

            if (scope is not null && root.QualifiedName is { Length: > 0 } rootQualified)
            {
                var reconstructed = $"{rootQualified}::{name}";
                if (!ExposeScopeResolver.IsInSubjectScope(reconstructed, scope))
                {
                    return;
                }
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
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: EndMarkerStyle.OpenChevron,
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
