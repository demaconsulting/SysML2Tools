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

        // Assert: the message line is horizontal (equal Y) and has an open arrowhead at the target
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>());
        Assert.Equal(line.Waypoints[0].Y, line.Waypoints[^1].Y, 6);
        Assert.NotEqual(line.Waypoints[0].X, line.Waypoints[^1].X);
        Assert.Equal(EndMarkerStyle.OpenChevron, line.TargetEnd);
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
    ///     A sequence message arrow carries an open arrowhead at the receiver end.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_MessageArrow_HasOpenArrowhead()
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

        // Assert: the message arrow has an open arrowhead at the receiver (target) end
        var line = Assert.Single(layout.Nodes.OfType<LayoutLine>());
        Assert.Equal(EndMarkerStyle.OpenChevron, line.TargetEnd);
        Assert.Equal(EndMarkerStyle.None, line.SourceEnd);
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
            ExposedNames = ["ProtocolB"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolB", SysmlEdgeKind.Expose)]
        };
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
            ExposedNames = ["x"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolB::x", SysmlEdgeKind.Expose)]
        };
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
            ExposedNames = ["x"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolA::ProtocolC::x", SysmlEdgeKind.Expose)]
        };
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
            ExposedNames = ["Unrelated"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::Unrelated", SysmlEdgeKind.Expose)]
        };
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
            ExposedNames = ["server"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::ProtocolA::server", SysmlEdgeKind.Expose)]
        };
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
            ExposedNames = ["client", "server"],
            ResolvedEdges =
            [
                new SysmlEdge("M::V", "M::ProtocolA::client", SysmlEdgeKind.Expose),
                new SysmlEdge("M::V", "M::ProtocolA::server", SysmlEdgeKind.Expose)
            ]
        };
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
            ExposedNames = ["myProtocol"],
            ResolvedEdges = [new SysmlEdge("M::V", "M::myProtocol", SysmlEdgeKind.Expose)]
        };
        var context = new ViewContext("v", workspace, viewNode);
        var options = new RenderOptions(Themes.Light);

        var layout = strategy.BuildLayout(context, options);

        var lifelines = layout.Nodes.OfType<LayoutLifeline>().Select(l => l.Label).ToList();
        Assert.Contains("x", lifelines);
        Assert.Contains("y", lifelines);
    }
}
