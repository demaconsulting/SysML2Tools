// <copyright file="SequenceViewLayoutStrategyTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="SequenceViewLayoutStrategy"/>.
/// </summary>
public sealed class SequenceViewLayoutStrategyTests
{
    /// <summary>
    ///     A definition with messages produces a lifeline per participant and a message line per
    ///     message, ordered top-to-bottom by declaration order.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_Messages_ProducesLifelinesAndOrderedLines()
    {
        // Arrange: client/server with two messages
        var strategy = new SequenceViewLayoutStrategy();
        var protocol = new SysmlDefinitionNode
        {
            Name = "Protocol",
            QualifiedName = "P::Protocol",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlConnectionNode { Name = "request", ConnectionKeyword = "message", EndpointA = "client.a", EndpointB = "server.b" },
                new SysmlConnectionNode { Name = "response", ConnectionKeyword = "message", EndpointA = "server.c", EndpointB = "client.d" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["P::Protocol"] = protocol }
        };
        var context = new ViewContext("ProtocolSequenceView", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: two lifelines (client, server) and two message lines
        var lifelines = layout.Nodes.OfType<LayoutLifeline>().ToList();
        Assert.Equal(2, lifelines.Count);
        Assert.Contains(lifelines, l => l.Label == "client");
        Assert.Contains(lifelines, l => l.Label == "server");

        var lines = layout.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(2, lines.Count);

        // The "request" line is above the "response" line (declaration order).
        var request = lines.First(l => l.MidpointLabel == "request");
        var response = lines.First(l => l.MidpointLabel == "response");
        Assert.True(request.Waypoints[0].Y < response.Waypoints[0].Y);
    }

    /// <summary>A message arrow runs horizontally from the sender lifeline to the receiver lifeline.</summary>
    [Fact]
    public void SequenceView_BuildLayout_Message_IsHorizontalBetweenLifelines()
    {
        // Arrange: a single message client -> server
        var strategy = new SequenceViewLayoutStrategy();
        var protocol = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlConnectionNode { Name = "m", ConnectionKeyword = "message", EndpointA = "client.s", EndpointB = "server.r" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = protocol }
        };
        var context = new ViewContext("Sequence", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the message line is horizontal (equal Y) and has a hollow triangle head at the target
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>());
        Assert.Equal(line.Waypoints[0].Y, line.Waypoints[^1].Y, 6);
        Assert.NotEqual(line.Waypoints[0].X, line.Waypoints[^1].X);
        Assert.Equal(EndMarkerStyle.HollowTriangle, line.TargetEnd);
    }

    /// <summary>
    ///     A workspace with no messages yields a minimal canvas.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_NoMessages_ReturnsMinimalCanvas()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = new SysmlWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     A sequence message arrow carries a hollow triangle head at the receiver end.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_MessageArrow_HasHollowTriangleHead()
    {
        // Arrange: a single message sender -> receiver
        var strategy = new SequenceViewLayoutStrategy();
        var protocol = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlConnectionNode { Name = "call", ConnectionKeyword = "message", EndpointA = "sender.s", EndpointB = "receiver.r" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = protocol }
        };
        var context = new ViewContext("Sequence", workspace);
        var options = new RenderOptions(Themes.Light);

        // Act
        var layout = strategy.BuildLayout(context, options);

        // Assert: the message arrow has a hollow triangle head at the receiver (target) end
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>());
        Assert.Equal(EndMarkerStyle.HollowTriangle, line.TargetEnd);
        Assert.Equal(EndMarkerStyle.None, line.SourceEnd);
    }

    /// <summary>
    ///     A self-message (both endpoints on the same lifeline) is drawn as a loop that likewise ends
    ///     in a hollow triangle head.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_SelfMessage_HasHollowTriangleHead()
    {
        // Arrange: a single message whose endpoints are both on the "worker" lifeline
        var strategy = new SequenceViewLayoutStrategy();
        var protocol = new SysmlDefinitionNode
        {
            Name = "P",
            QualifiedName = "M::P",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlConnectionNode { Name = "tick", ConnectionKeyword = "message", EndpointA = "worker.a", EndpointB = "worker.b" }
            ]
        };
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode> { ["M::P"] = protocol }
        };

        // Act
        var layout = strategy.BuildLayout(new ViewContext("Sequence", workspace), new RenderOptions(Themes.Light));

        // Assert: the loop (more than two waypoints) ends in a hollow triangle
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>());
        Assert.True(line.Waypoints.Count > 2, "a self-message is drawn as a multi-segment loop.");
        Assert.Equal(EndMarkerStyle.HollowTriangle, line.TargetEnd);
    }

    /// <summary>
    ///     Confirms Assumption 4 of the expose-scoping plan for Sequence View: reconstructing a
    ///     lifeline's absolute qualified name as <c>"{root.QualifiedName}::{lifelineName}"</c>
    ///     matches a genuinely declared feature's own <c>QualifiedName</c> in a realistic model —
    ///     <c>part client { ... }</c> declared directly under the root part def, referenced by a
    ///     message endpoint's first dotted segment (<c>client.sendRequest</c>) — mirroring
    ///     <c>test/SysMLModels/Custom/client-server-sequence.sysml</c>. This validates that
    ///     <see cref="SequenceViewLayoutStrategy"/> may safely use the reconstructed name with
    ///     <c>ExposeScopeResolver.IsInSubjectScope</c> for lifeline-level scope filtering.
    /// </summary>
    [Fact]
    public void SequenceView_LifelineQualifiedNameReconstruction_MatchesDeclaredFeature()
    {
        // Arrange: a Protocol part def declaring client/server parts and a message between them,
        // mirroring the real client-server-sequence.sysml fixture.
        var root = new SysmlDefinitionNode
        {
            Name = "Protocol",
            QualifiedName = "ClientServerProtocol::Protocol",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "client", QualifiedName = "ClientServerProtocol::Protocol::client", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "server", QualifiedName = "ClientServerProtocol::Protocol::server", FeatureKeyword = "part" },
                new SysmlConnectionNode
                {
                    Name = "request",
                    ConnectionKeyword = "message",
                    EndpointA = "client.sendRequest",
                    EndpointB = "server.getRequest"
                }
            ]
        };

        // Act: reconstruct the lifeline qualified name the strategy uses ("client", the message
        // endpoint's first dotted segment) and confirm it equals the actually-declared feature's
        // own QualifiedName.
        var reconstructed = $"{root.QualifiedName}::client";
        var declaredClient = root.Children.OfType<SysmlFeatureNode>().Single(f => f.Name == "client");

        // Assert
        Assert.Equal(declaredClient.QualifiedName, reconstructed);
    }

    /// <summary>
    ///     Builds a workspace with two candidate roots: <c>M::ProtocolA</c> (two messages — the
    ///     heuristic's default pick, with lifelines <c>client</c>/<c>server</c> declared directly
    ///     under it) and <c>M::ProtocolB</c> (one message, lifelines <c>x</c>/<c>y</c>), plus an
    ///     unrelated sibling definition <c>M::Unrelated</c> with no messages.
    /// </summary>
    private static SysmlWorkspace BuildTwoRootWorkspace()
    {
        var protocolA = new SysmlDefinitionNode
        {
            Name = "ProtocolA",
            QualifiedName = "M::ProtocolA",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "client", QualifiedName = "M::ProtocolA::client", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "server", QualifiedName = "M::ProtocolA::server", FeatureKeyword = "part" },
                new SysmlConnectionNode { Name = "req", ConnectionKeyword = "message", EndpointA = "client.a", EndpointB = "server.b" },
                new SysmlConnectionNode { Name = "resp", ConnectionKeyword = "message", EndpointA = "server.c", EndpointB = "client.d" },
                new SysmlConnectionNode { Name = "self", ConnectionKeyword = "message", EndpointA = "server.e", EndpointB = "server.f" }
            ]
        };
        var protocolB = new SysmlDefinitionNode
        {
            Name = "ProtocolB",
            QualifiedName = "M::ProtocolB",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "x", QualifiedName = "M::ProtocolB::x", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "y", QualifiedName = "M::ProtocolB::y", FeatureKeyword = "part" },
                new SysmlConnectionNode { Name = "m", ConnectionKeyword = "message", EndpointA = "x.p", EndpointB = "y.q" },
                new SysmlConnectionNode { Name = "self", ConnectionKeyword = "message", EndpointA = "x.s", EndpointB = "x.t" }
            ]
        };
        var unrelated = new SysmlDefinitionNode { Name = "Unrelated", QualifiedName = "M::Unrelated", DefinitionKeyword = "part def" };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::ProtocolA"] = protocolA,
                ["M::ProtocolB"] = protocolB,
                ["M::Unrelated"] = unrelated
            }
        };
    }

    /// <summary>
    ///     With no <c>expose</c> statement (null <c>ViewNode</c>), the heuristic picks the
    ///     definition with the most messages (<c>ProtocolA</c>), unchanged from pre-scoping
    ///     behavior — the critical --auto/no-expose regression guard.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var context = new ViewContext("v", workspace);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("client", lifelines);
        Assert.Contains("server", lifelines);
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition other than the heuristic's default root
    ///     (<c>ProtocolB</c>, which has fewer messages than <c>ProtocolA</c>) selects
    ///     <c>ProtocolB</c> as the root instead.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("ProtocolB", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolB", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("x", lifelines);
        Assert.Contains("y", lifelines);
        Assert.DoesNotContain("client", lifelines);
        Assert.DoesNotContain("server", lifelines);
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at an inner child of a non-heuristic root
    ///     (<c>M::ProtocolB::x</c>) still selects <c>ProtocolB</c> as the root, since the exposed
    ///     subject lies within the candidate root's own containment subtree.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("x", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolB::x", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("x", lifelines);
    }

    /// <summary>
    ///     Builds a workspace where <c>M::ProtocolA</c> (more messages) genuinely nests
    ///     <c>M::ProtocolA::ProtocolC</c> (fewer messages) as one of its own <c>Children</c>, while
    ///     both are also independently registered in <c>Declarations</c> under their own qualified
    ///     names — the shape needed to make both candidates scope-relevant for a subject exposed only
    ///     inside <c>ProtocolC</c>.
    /// </summary>
    private static SysmlWorkspace BuildNestedCandidateWorkspace()
    {
        var protocolC = new SysmlDefinitionNode
        {
            Name = "ProtocolC",
            QualifiedName = "M::ProtocolA::ProtocolC",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "x", QualifiedName = "M::ProtocolA::ProtocolC::x", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "y", QualifiedName = "M::ProtocolA::ProtocolC::y", FeatureKeyword = "part" },
                new SysmlConnectionNode { Name = "m", ConnectionKeyword = "message", EndpointA = "x.p", EndpointB = "y.q" },
                new SysmlConnectionNode { Name = "self", ConnectionKeyword = "message", EndpointA = "x.s", EndpointB = "x.t" }
            ]
        };
        var protocolA = new SysmlDefinitionNode
        {
            Name = "ProtocolA",
            QualifiedName = "M::ProtocolA",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "client", QualifiedName = "M::ProtocolA::client", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "server", QualifiedName = "M::ProtocolA::server", FeatureKeyword = "part" },
                new SysmlConnectionNode { Name = "req", ConnectionKeyword = "message", EndpointA = "client.a", EndpointB = "server.b" },
                new SysmlConnectionNode { Name = "resp", ConnectionKeyword = "message", EndpointA = "server.c", EndpointB = "client.d" },
                new SysmlConnectionNode { Name = "self", ConnectionKeyword = "message", EndpointA = "server.e", EndpointB = "server.f" },
                protocolC
            ]
        };
        return new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["M::ProtocolA"] = protocolA,
                ["M::ProtocolA::ProtocolC"] = protocolC
            }
        };
    }

    /// <summary>
    ///     Exposing an inner lifeline participant of the nested definition <c>ProtocolC</c> selects
    ///     <c>ProtocolC</c> as the root even though its ancestor <c>ProtocolA</c> has more messages
    ///     and would win the old pure-score tie-break, proving <c>FindRoot</c> now prefers the most
    ///     specific (deepest-qualified-name) scope-relevant candidate over a less specific ancestor.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposeInnerLifelineOfNestedDefinition_SelectsNestedDefinitionNotAncestor()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildNestedCandidateWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("x", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolA::ProtocolC::x", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("x", lifelines);
        Assert.DoesNotContain("client", lifelines);
        Assert.DoesNotContain("server", lifelines);
    }

    /// <summary>
    ///     An <c>expose</c> edge pointing at a definition unrelated to every candidate root makes no
    ///     root scope-relevant, so no root is chosen and an empty canvas results.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("Unrelated", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::Unrelated", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        Assert.Empty(layout.Nodes);
    }

    /// <summary>
    ///     Exposing lifeline <c>server</c> of <c>ProtocolA</c> narrows the diagram to lifelines whose
    ///     reconstructed qualified name is in scope (<c>client</c> is dropped), which in turn drops
    ///     the <c>req</c>/<c>resp</c> messages that reference the excluded <c>client</c> lifeline —
    ///     proving the existing <c>ResolveMessages</c> endpoint-drop mechanism, not new edge-side
    ///     logic, governs which messages survive filtering. The <c>self</c> message (both endpoints
    ///     on <c>server</c>) remains since neither of its endpoints was excluded, producing strictly
    ///     fewer lifelines than the unscoped rendering.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposeSingleLifeline_NarrowsLifelines()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("server", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolA::server", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var scoped = strategy.BuildLayout(context, options);
        var full = strategy.BuildLayout(new ViewContext("full", workspace), options);

        var scopedLifelines = scoped.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        var fullLifelines = full.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();

        Assert.Contains("server", scopedLifelines);
        Assert.DoesNotContain("client", scopedLifelines);
        Assert.True(scopedLifelines.Count < fullLifelines.Count,
            $"expected scoped ({scopedLifelines.Count}) < full ({fullLifelines.Count})");

        // Messages referencing the excluded client lifeline are dropped; the server-only self
        // message survives.
        var scopedLines = scoped.Nodes.OfType<LayoutLine>().Select(l => l.MidpointLabel).ToList();
        Assert.DoesNotContain("req", scopedLines);
        Assert.DoesNotContain("resp", scopedLines);
        Assert.Contains("self", scopedLines);
    }

    /// <summary>
    ///     An <c>expose</c> edge naming both lifelines of the same root includes both (the union of
    ///     every exposed target's containment subtree) and retains every message between them.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposeBothLifelines_UnionsSubtreesKeepsMessages()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("client", null, ExposeRecursionKind.MembershipRecursive), new ExposeMember("server", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "M::ProtocolA::client", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "M::ProtocolA::server", SysmlEdgeKind.Expose)
            ]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("client", lifelines);
        Assert.Contains("server", lifelines);
        Assert.Equal(3, layout.Nodes.OfType<LayoutLine>().Count());
    }

    /// <summary>
    ///     An <c>expose</c> edge that resolves to a feature usage (not a definition) still selects
    ///     the definition it types via the shared usage-to-type fallback in
    ///     <c>ExposeScopeResolver.ResolveExposedScope</c>: exposing a usage <c>myProtocol</c> typed
    ///     by <c>ProtocolB</c> selects <c>ProtocolB</c> as the root.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot()
    {
        var strategy = new SequenceViewLayoutStrategy();
        var workspace = BuildTwoRootWorkspace();
        workspace.AddDeclaration("M::myProtocol", new SysmlFeatureNode
        {
            Name = "myProtocol",
            QualifiedName = "M::myProtocol",
            FeatureTyping = "ProtocolB",
            ResolvedEdges = [new SysmlEdge("M::myProtocol", "M::ProtocolB", SysmlEdgeKind.Typing)]
        });
        var viewNode = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "M::V",
            ExposeMembers = [new ExposeMember("myProtocol", null, ExposeRecursionKind.MembershipRecursive)],
            ResolvedEdges = [new SysmlEdge("M::V", "M::myProtocol", SysmlEdgeKind.Expose)]
        }.WithResolvedExposeMembers();
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("x", lifelines);
        Assert.Contains("y", lifelines);
    }

    /// <summary>Builds a root part def that declares the given features and messages.</summary>
    private static SysmlWorkspace BuildWorkspace(params SysmlNode[] children)
    {
        var root = new SysmlDefinitionNode
        {
            Name = "Flow",
            QualifiedName = "M::Flow",
            DefinitionKeyword = "part def",
            Children = children
        };
        return new SysmlWorkspace { Declarations = new Dictionary<string, SysmlNode> { ["M::Flow"] = root } };
    }

    /// <summary>Builds a declared lifeline usage with the given events, in declaration order.</summary>
    private static SysmlFeatureNode Participant(
        string name,
        string? typing = null,
        bool isReference = false,
        string keyword = "part",
        IReadOnlyList<string>? events = null) =>
        new()
        {
            Name = name,
            QualifiedName = $"M::Flow::{name}",
            FeatureKeyword = keyword,
            FeatureTyping = typing,
            IsReference = isReference,
            Children = [.. (events ?? []).Select(e => new SysmlFeatureNode { Name = e, QualifiedName = $"M::Flow::{name}::{e}", FeatureKeyword = "event occurrence" })]
        };

    /// <summary>Builds a message between two event references.</summary>
    private static SysmlConnectionNode Message(string name, string from, string to, string? payload = null) =>
        new() { Name = name, ConnectionKeyword = "message", EndpointA = from, EndpointB = to, PayloadText = payload };

    /// <summary>Lays out a workspace built by <see cref="BuildWorkspace"/> with the Light theme.</summary>
    private static LayoutTree Layout(SysmlWorkspace workspace) =>
        new SequenceViewLayoutStrategy().BuildLayout(new ViewContext("v", workspace), new RenderOptions(Themes.Light));

    /// <summary>
    ///     A declared part is drawn as a lifeline with a rounded header, the <c>part</c> keyword, and the
    ///     name line <c>name : Type</c>, as the specification draws a part usage.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_Lifeline_HasRoundedHeaderKeywordAndTypedLabel()
    {
        // Arrange
        var workspace = BuildWorkspace(
            Participant("client", "Client"),
            Participant("server", "Server"),
            Message("request", "client.a", "server.b"));

        // Act
        var layout = Layout(workspace);

        // Assert
        var client = layout.Nodes.OfType<LayoutLifeline>().Single(l => l.Label.StartsWith("client", StringComparison.Ordinal));
        Assert.Equal("client : Client", client.Label);
        Assert.Equal("part", client.Keyword);
        Assert.Equal(BoxShape.RoundedRectangle, client.HeaderShape);
        Assert.Equal(BoxMetrics.LifelineHeaderHeight(Themes.Light, hasKeyword: true), client.HeaderHeight, 6);
        Assert.True(client.HeaderAppearance is null || client.HeaderAppearance.OutlineStyle == LineStyle.Solid);
    }

    /// <summary>A reference part is drawn with the <c>ref part</c> keyword and a dashed header outline.</summary>
    [Fact]
    public void SequenceView_BuildLayout_ReferencePartLifeline_HasDashedHeader()
    {
        // Arrange
        var workspace = BuildWorkspace(
            Participant("client"),
            Participant("server", "Server", isReference: true),
            Message("request", "client.a", "server.b"));

        // Act
        var layout = Layout(workspace);

        // Assert
        var lifelines = layout.Nodes.OfType<LayoutLifeline>().ToList();
        var server = lifelines.Single(l => l.Label.StartsWith("server", StringComparison.Ordinal));
        Assert.Equal("ref part", server.Keyword);
        Assert.NotNull(server.HeaderAppearance);
        Assert.Equal(LineStyle.Dashed, server.HeaderAppearance.OutlineStyle);
        var client = lifelines.Single(l => l.Label == "client");
        Assert.True(client.HeaderAppearance is null || client.HeaderAppearance.OutlineStyle == LineStyle.Solid);
    }

    /// <summary>
    ///     An item usage and an occurrence usage are lifelines as well, keyed by their own keyword.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ItemAndOccurrenceUsages_AreLifelines()
    {
        // Arrange
        var workspace = BuildWorkspace(
            Participant("token", "Token", keyword: "item"),
            Participant("session", keyword: "occurrence"),
            Message("use", "token.a", "session.b"));

        // Act
        var layout = Layout(workspace);

        // Assert
        var lifelines = layout.Nodes.OfType<LayoutLifeline>().ToList();
        Assert.Equal(["item", "occurrence"], lifelines.Select(l => l.Keyword));
    }

    /// <summary>
    ///     A declared part that exchanges no message still has a lifeline, placed in declaration order
    ///     after the parts declared before it.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_DeclaredPartWithoutMessages_GetsLifeline()
    {
        // Arrange: audit never appears in a message
        var workspace = BuildWorkspace(
            Participant("client"),
            Participant("server"),
            Participant("audit", "Audit"),
            Message("request", "client.a", "server.b"));

        // Act
        var layout = Layout(workspace);

        // Assert
        var lifelines = layout.Nodes.OfType<LayoutLifeline>().OrderBy(l => l.CentreX).ToList();
        Assert.Equal(["client", "server", "audit : Audit"], lifelines.Select(l => l.Label));
    }

    /// <summary>
    ///     A message endpoint whose first segment names no declared usage still gets a lifeline, after the
    ///     declared ones, drawn with its plain name and no keyword.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_UndeclaredEndpoint_FallsBackToFirstAppearance()
    {
        // Arrange: ghost and spirit are not declared; client is
        var workspace = BuildWorkspace(
            Participant("client", "Client"),
            Message("m1", "ghost.a", "client.b"),
            Message("m2", "spirit.a", "ghost.b"));

        // Act
        var layout = Layout(workspace);

        // Assert: declared first, then undeclared in first-appearance order, with no keyword
        var lifelines = layout.Nodes.OfType<LayoutLifeline>().OrderBy(l => l.CentreX).ToList();
        Assert.Equal(["client : Client", "ghost", "spirit"], lifelines.Select(l => l.Label));
        Assert.Equal(["part", null, null], lifelines.Select(l => l.Keyword));
        Assert.All(lifelines, l => Assert.Equal(BoxShape.RoundedRectangle, l.HeaderShape));
        Assert.All(lifelines, l => Assert.Equal(lifelines[0].HeaderHeight, l.HeaderHeight, 6));
    }

    /// <summary>
    ///     A message with a payload is labeled <c>name of Payload</c>; without a name the label is
    ///     <c>of Payload</c>; without a payload it is the name alone (and nothing when unnamed).
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_MessageWithPayload_LabelIsNameOfPayload()
    {
        // Arrange
        var workspace = BuildWorkspace(
            Participant("a"),
            Participant("b"),
            Message("grant", "a.x", "b.y", "Token"),
            Message(string.Empty, "a.x", "b.y", "Token"),
            Message("ping", "b.y", "a.x"),
            Message(string.Empty, "b.y", "a.x"));

        // Act
        var lines = Layout(workspace).Nodes.OfType<LayoutLine>().OrderBy(l => l.Waypoints[0].Y).ToList();

        // Assert
        Assert.Equal(["grant of Token", "of Token", "ping", null], lines.Select(l => l.MidpointLabel));
    }

    /// <summary>
    ///     Messages declared out of order are drawn in the order of the events they connect: the
    ///     declaration order of each lifeline's <c>event occurrence</c> usages decides.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_MessagesDeclaredOutOfEventOrder_AreOrderedByEvents()
    {
        // Arrange: reply is declared first but its events follow those of request
        var workspace = BuildWorkspace(
            Participant("client", events: ["send", "receive"]),
            Participant("server", events: ["get", "answer"]),
            Message("reply", "server.answer", "client.receive"),
            Message("request", "client.send", "server.get"));

        // Act
        var lines = Layout(workspace).Nodes.OfType<LayoutLine>().OrderBy(l => l.Waypoints[0].Y).ToList();

        // Assert
        Assert.Equal(["request", "reply"], lines.Select(l => l.MidpointLabel));
    }

    /// <summary>
    ///     Two messages that share an event on a lifeline are ordered through the other events of their
    ///     lifelines, and a message the events do not order stays where it was declared.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_MessagesChained_AreOrderedTransitivelyAndStably()
    {
        // Arrange: m3 < m2 < m1 on the server's events; extra has no event data and is declared last
        var workspace = BuildWorkspace(
            Participant("client", events: ["c1", "c2", "c3"]),
            Participant("server", events: ["s1", "s2", "s3"]),
            Message("m1", "client.c3", "server.s3"),
            Message("m2", "client.c2", "server.s2"),
            Message("m3", "client.c1", "server.s1"),
            Message("extra", "client", "server"));

        // Act
        var lines = Layout(workspace).Nodes.OfType<LayoutLine>().OrderBy(l => l.Waypoints[0].Y).ToList();

        // Assert
        Assert.Equal(["m3", "m2", "m1", "extra"], lines.Select(l => l.MidpointLabel));
    }

    /// <summary>A model without event data keeps the declaration order of its messages.</summary>
    [Fact]
    public void SequenceView_BuildLayout_NoEventData_KeepsDeclarationOrder()
    {
        // Arrange: endpoints name no declared event
        var workspace = BuildWorkspace(
            Participant("a"),
            Participant("b"),
            Message("third", "a.x", "b.y"),
            Message("first", "b.y", "a.x"),
            Message("second", "a.x", "b.y"));

        // Act
        var lines = Layout(workspace).Nodes.OfType<LayoutLine>().OrderBy(l => l.Waypoints[0].Y).ToList();

        // Assert
        Assert.Equal(["third", "first", "second"], lines.Select(l => l.MidpointLabel));
    }

    /// <summary>
    ///     When two lifelines disagree about the order of two messages, neither constraint is usable and
    ///     the messages keep their declaration order.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ContradictoryEventOrders_FallBackToDeclarationOrder()
    {
        // Arrange: the client puts m1 first, the server puts m2 first
        var workspace = BuildWorkspace(
            Participant("client", events: ["c1", "c2"]),
            Participant("server", events: ["s1", "s2"]),
            Message("m1", "client.c1", "server.s2"),
            Message("m2", "client.c2", "server.s1"));

        // Act
        var lines = Layout(workspace).Nodes.OfType<LayoutLine>().OrderBy(l => l.Waypoints[0].Y).ToList();

        // Assert
        Assert.Equal(["m1", "m2"], lines.Select(l => l.MidpointLabel));
    }

    /// <summary>
    ///     A message label wider than the default lifeline pitch widens the pitch so the label fits
    ///     between the two lifelines its arrow connects.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_LongMessageLabel_WidensLifelinePitch()
    {
        // Arrange: the same two participants with a short and a very long message label
        var shortLayout = Layout(BuildWorkspace(Participant("a"), Participant("b"), Message("m", "a.x", "b.y")));
        var longLayout = Layout(BuildWorkspace(
            Participant("a"),
            Participant("b"),
            Message("deliverTheEntireAuthorizationPayload", "a.x", "b.y", "AuthorizationCodeResponse")));

        // Act
        double Pitch(LayoutTree tree)
        {
            var xs = tree.Nodes.OfType<LayoutLifeline>().Select(l => l.CentreX).OrderBy(x => x).ToList();
            return xs[1] - xs[0];
        }

        // Assert: the label (about 61 characters of body text) needs more than the default pitch
        var labelWidth = "deliverTheEntireAuthorizationPayload of AuthorizationCodeResponse".Length * Themes.Light.FontSizeBody * 0.62;
        Assert.True(Pitch(longLayout) > Pitch(shortLayout));
        Assert.True(Pitch(longLayout) >= labelWidth);
    }
}
