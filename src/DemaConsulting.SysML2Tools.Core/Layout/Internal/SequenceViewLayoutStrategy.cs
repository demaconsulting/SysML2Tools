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
/// Layout strategy for Sequence View diagrams (SysML v2.0 clause 8.2.3.12). Renders each lifeline as a
/// vertical dashed stem under a rounded usage header (<c>«part»</c> keyword over <c>name : Type</c>) and
/// draws each message as a horizontal arrow between lifelines, ordered top-to-bottom by the order of the
/// events it connects.
/// </summary>
/// <remarks>
/// Lifelines are the <c>part</c>, <c>item</c>, and <c>occurrence</c> usages the root declares, in
/// declaration order (including a part that exchanges no message), followed by any message endpoint whose
/// first segment names no declared usage, in first-appearance order. A message is labeled
/// <c>name of Payload</c>. Messages are ordered by the declaration order of the <c>event occurrence</c>
/// usages they reference on each lifeline (the <c>then</c> chain), and by declaration order when the model
/// gives no event data. Layout is pure arithmetic: lifeline X is the column index times a pitch, and
/// message Y is the message ordinal times a row pitch.
/// </remarks>
internal sealed class SequenceViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>Minimum horizontal pitch between adjacent lifelines.</summary>
    private const double MinPitch = 140.0;

    /// <summary>The keyword of the usages whose declaration order gives the event order of a lifeline.</summary>
    private const string EventKeyword = "event occurrence";

    /// <summary>The usage keywords that declare a lifeline.</summary>
    private static readonly IReadOnlySet<string> LifelineKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "part",
        "item",
        "occurrence",
    };

    /// <summary>
    /// A lifeline: its name, the header it is drawn with (keyword and name line, null keyword for a
    /// message endpoint that names no declared usage), and the events it declares in order.
    /// </summary>
    private sealed record LifelineItem(string Name, string? Keyword, string Label, bool IsReference, List<string> Events);

    /// <summary>
    /// A message between two lifelines with an optional label, and the position of each of its two events
    /// in the declaration order of its lifeline's events (-1 when the event is not declared there).
    /// </summary>
    private sealed record MessageItem(int From, int To, string Label, int FromEvent, int ToEvent);

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
        var messages = OrderMessages(ResolveMessages(root, lifelines, index));
        if (lifelines.Count == 0 || messages.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Every header is as tall as one that carries a keyword when any does, so the headers line up.
        var hasKeyword = lifelines.Any(l => l.Keyword is not null);
        var headerHeight = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword);
        var pitch = ComputePitch(lifelines, messages, theme);
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

        // Lifelines: a rounded usage header (keyword over name line), dashed when the usage is a reference.
        for (var i = 0; i < lifelines.Count; i++)
        {
            nodes.Add(new LayoutLifeline(
                CentreX: centreX[i],
                TopY: margin,
                BottomY: bottomY,
                Label: lifelines[i].Label,
                HeaderWidth: headerWidth,
                HeaderHeight: headerHeight,
                Keyword: lifelines[i].Keyword,
                HeaderShape: BoxShape.RoundedRectangle,
                HeaderAppearance: lifelines[i].IsReference ? new BoxAppearance(LineStyle.Dashed) : null));
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
                TargetEnd: EndMarkerStyle.HollowTriangle,
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
            // Members modeled for name resolution only are never drawn, nor chosen as a root.
            if (node.IsExcludedFromDiagrams)
            {
                continue;
            }

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

            var messages = def.DiagramChildren.OfType<SysmlConnectionNode>().Count(c => c.ConnectionKeyword == "message");
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
    /// Collects the lifelines of the root: first every <c>part</c>, <c>item</c>, and <c>occurrence</c> usage it
    /// declares, in declaration order (so a part that exchanges no message still has a lifeline), then every
    /// first segment of a message from/to reference that names no declared usage, in first-appearance order,
    /// drawn with its plain name. When <paramref name="scope"/> is
    /// non-null, a lifeline is skipped when its reconstructed absolute qualified name
    /// (<c>"{root.QualifiedName}::{lifelineName}"</c> — a message-endpoint lifeline name is the first
    /// dotted segment of an endpoint reference, which names a feature declared directly under
    /// <paramref name="root"/>, confirmed against real declared features in
    /// <c>client-server-sequence.sysml</c>) fails <see cref="ExposeScopeResolver.IsInSubjectScope"/>.
    /// When <paramref name="root"/> has no <c>QualifiedName</c> (defensive; every workspace
    /// declaration carries one), the reconstruction is skipped and lifeline scoping does not apply,
    /// so the strategy never filters based on an unreliable name.
    /// </summary>
    private static (IReadOnlyList<LifelineItem> Lifelines, Dictionary<string, int> Index) CollectLifelines(
        SysmlDefinitionNode root,
        ExposedScope? scope)
    {
        var lifelines = new List<LifelineItem>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(string? name, SysmlFeatureNode? declaration)
        {
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
            lifelines.Add(declaration is null
                ? new LifelineItem(name, null, name, false, [])
                : new LifelineItem(
                    name,
                    GeneralViewLayoutStrategy.UsageKeyword(declaration),
                    GeneralViewLayoutStrategy.FormatUsageNameLine(declaration, name),
                    declaration.IsReference,
                    [.. declaration.DiagramChildren
                        .OfType<SysmlFeatureNode>()
                        .Where(e => e.FeatureKeyword == EventKeyword && e.Name is not null)
                        .Select(e => e.Name!)]));
        }

        foreach (var feature in root.DiagramChildren.OfType<SysmlFeatureNode>()
                     .Where(f => f.Name is not null && f.FeatureKeyword is not null && LifelineKeywords.Contains(f.FeatureKeyword)))
        {
            Add(feature.Name, feature);
        }

        foreach (var message in root.DiagramChildren.OfType<SysmlConnectionNode>().Where(c => c.ConnectionKeyword == "message"))
        {
            Add(FirstSegment(message.EndpointA), null);
            Add(FirstSegment(message.EndpointB), null);
        }

        return (lifelines, index);
    }

    /// <summary>
    /// Resolves the root's messages to lifeline indices and event positions, preserving declaration order.
    /// A message with an endpoint on no lifeline (excluded by scope) is dropped.
    /// </summary>
    private static List<MessageItem> ResolveMessages(
        SysmlDefinitionNode root,
        IReadOnlyList<LifelineItem> lifelines,
        Dictionary<string, int> index)
    {
        var result = new List<MessageItem>();
        foreach (var message in root.DiagramChildren.OfType<SysmlConnectionNode>().Where(c => c.ConnectionKeyword == "message"))
        {
            var from = FirstSegment(message.EndpointA);
            var to = FirstSegment(message.EndpointB);
            if (from is null || to is null ||
                !index.TryGetValue(from, out var fi) || !index.TryGetValue(to, out var ti))
            {
                continue;
            }

            result.Add(new MessageItem(
                fi,
                ti,
                FormatMessageLabel(message.Name, message.PayloadText),
                lifelines[fi].Events.IndexOf(SecondSegment(message.EndpointA) ?? string.Empty),
                lifelines[ti].Events.IndexOf(SecondSegment(message.EndpointB) ?? string.Empty)));
        }

        return result;
    }

    /// <summary>
    /// Formats a message label as <c>name of Payload</c> (SysML v2.0 clause 8.2.3.12), leaving out the name
    /// and the payload when the model declares none.
    /// </summary>
    private static string FormatMessageLabel(string? name, string? payload)
    {
        var hasName = !string.IsNullOrEmpty(name);
        if (string.IsNullOrEmpty(payload))
        {
            return hasName ? name! : string.Empty;
        }

        return hasName ? $"{name} of {payload}" : $"of {payload}";
    }

    /// <summary>
    /// Orders messages by the events they connect: a message is drawn above another when one of its events
    /// precedes one of the other's events on the same lifeline (the declaration order of that lifeline's
    /// <c>event occurrence</c> usages). Among messages the events do not order, and when two lifelines
    /// disagree about two messages, declaration order decides, so a model with no event data keeps
    /// declaration order.
    /// </summary>
    private static IReadOnlyList<MessageItem> OrderMessages(List<MessageItem> messages)
    {
        var count = messages.Count;
        var before = new bool[count, count];
        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                before[i, j] = i != j && Precedes(messages[i], messages[j]);
            }
        }

        // Two messages the lifelines order both ways carry no usable constraint.
        var successors = new List<int>[count];
        var incoming = new int[count];
        for (var i = 0; i < count; i++)
        {
            successors[i] = [];
        }

        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                if (before[i, j] && !before[j, i])
                {
                    successors[i].Add(j);
                    incoming[j]++;
                }
            }
        }

        // Stable topological sort: always draw the earliest-declared message that has nothing left above
        // it; a cycle among the remaining messages falls back to the earliest-declared one.
        var ordered = new List<MessageItem>(count);
        var placed = new bool[count];
        for (var step = 0; step < count; step++)
        {
            var next = -1;
            for (var i = 0; i < count && next < 0; i++)
            {
                if (!placed[i] && incoming[i] == 0)
                {
                    next = i;
                }
            }

            if (next < 0)
            {
                next = Array.IndexOf(placed, false);
            }

            placed[next] = true;
            ordered.Add(messages[next]);
            foreach (var successor in successors[next])
            {
                incoming[successor]--;
            }
        }

        return ordered;
    }

    /// <summary>Returns whether an event of <paramref name="a"/> precedes an event of <paramref name="b"/> on one lifeline.</summary>
    private static bool Precedes(MessageItem a, MessageItem b)
    {
        (int Lifeline, int Event)[] endsA = [(a.From, a.FromEvent), (a.To, a.ToEvent)];
        (int Lifeline, int Event)[] endsB = [(b.From, b.FromEvent), (b.To, b.ToEvent)];
        return endsA.Any(x => x.Event >= 0 && endsB.Any(y => y.Event >= 0 && y.Lifeline == x.Lifeline && x.Event < y.Event));
    }

    /// <summary>
    /// Computes the horizontal pitch between lifelines: wide enough for the widest header (keyword and name
    /// line) and for the widest message label across the lifelines its arrow spans.
    /// </summary>
    private static double ComputePitch(IReadOnlyList<LifelineItem> lifelines, IReadOnlyList<MessageItem> messages, Theme theme)
    {
        var maxLabel = 0.0;
        foreach (var lifeline in lifelines)
        {
            maxLabel = Math.Max(maxLabel, lifeline.Label.Length * theme.FontSizeBody * CharWidthFactor);
        }

        var pitch = Math.Max(MinPitch, maxLabel + (theme.LabelPadding * 8.0));
        foreach (var message in messages.Where(m => m.From != m.To))
        {
            var span = Math.Abs(message.From - message.To);
            var labelWidth = (message.Label.Length * theme.FontSizeBody * CharWidthFactor) + (theme.LabelPadding * 4.0);
            pitch = Math.Max(pitch, labelWidth / span);
        }

        return pitch;
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
            TargetEnd: EndMarkerStyle.HollowTriangle,
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

    /// <summary>Returns the second dot-separated segment of a reference (the event on the lifeline), or null.</summary>
    private static string? SecondSegment(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var dot = reference.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return null;
        }

        var rest = reference[(dot + 1)..];
        var next = rest.IndexOf('.', StringComparison.Ordinal);
        return next >= 0 ? rest[..next] : rest;
    }
}
