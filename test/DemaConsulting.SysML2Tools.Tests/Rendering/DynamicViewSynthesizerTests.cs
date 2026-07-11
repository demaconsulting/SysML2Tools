// <copyright file="DynamicViewSynthesizerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Tests for <see cref="DiagramRenderer.SynthesizeDynamicView"/> (which delegates to the
///     internal <c>DynamicViewSynthesizer</c>).
/// </summary>
public sealed class DynamicViewSynthesizerTests
{
    /// <summary>A "general" dynamic view targeting any resolvable non-stdlib definition succeeds.</summary>
    [Fact]
    public void Synthesize_GeneralKind_ResolvableTarget_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::Widget", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asGeneralDiagram", viewNode!.RenderTargetName);
        Assert.Single(viewNode.ExposeMembers);
        Assert.Equal("P::Widget", viewNode.ExposeMembers[0].QualifiedName);
        var edge = Assert.Single(viewNode.ResolvedEdges);
        Assert.Equal(SysmlEdgeKind.Expose, edge.Kind);
        Assert.Equal("P::Widget", edge.TargetQualifiedName);
    }

    /// <summary>A "grid" dynamic view targeting any resolvable non-stdlib definition succeeds.</summary>
    [Fact]
    public void Synthesize_GridKind_ResolvableTarget_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "grid", "P::Widget", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asGridDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>A "browser" dynamic view targeting any resolvable non-stdlib definition succeeds.</summary>
    [Fact]
    public void Synthesize_BrowserKind_ResolvableTarget_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "browser", "P::Widget", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asTreeDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>
    /// An "interconnection" dynamic view targeting a "part def" with a nested "part" feature succeeds.
    /// </summary>
    [Fact]
    public void Synthesize_InterconnectionKind_PartDefWithNestedPart_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode
                {
                    Name = "Vehicle",
                    QualifiedName = "P::Vehicle",
                    DefinitionKeyword = "part def",
                    Children = [new SysmlFeatureNode { Name = "engine", QualifiedName = "P::Vehicle::engine", FeatureKeyword = "part" }]
                }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "interconnection", "P::Vehicle", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asInterconnectionDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>An "interconnection" dynamic view targeting a non-"part def" fails with a diagnostic.</summary>
    [Fact]
    public void Synthesize_InterconnectionKind_NotPartDef_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Mass"] = new SysmlDefinitionNode { Name = "Mass", QualifiedName = "P::Mass", DefinitionKeyword = "attribute def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "interconnection", "P::Mass", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("part def", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>An "interconnection" dynamic view targeting a part def with no nested parts fails.</summary>
    [Fact]
    public void Synthesize_InterconnectionKind_NoNestedParts_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Vehicle"] = new SysmlDefinitionNode { Name = "Vehicle", QualifiedName = "P::Vehicle", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "interconnection", "P::Vehicle", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("no nested 'part'", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>A "state" dynamic view targeting a definition with a nested transition succeeds.</summary>
    [Fact]
    public void Synthesize_StateKind_HasTransition_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Traffic"] = new SysmlDefinitionNode
                {
                    Name = "Traffic",
                    QualifiedName = "P::Traffic",
                    DefinitionKeyword = "state def",
                    Children = [new SysmlTransitionNode { Source = "Red", Target = "Green" }]
                }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "state", "P::Traffic", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asStateTransitionDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>A "state" dynamic view targeting a definition with no transitions fails.</summary>
    [Fact]
    public void Synthesize_StateKind_NoTransitions_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Traffic"] = new SysmlDefinitionNode { Name = "Traffic", QualifiedName = "P::Traffic", DefinitionKeyword = "state def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "state", "P::Traffic", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("no nested state transitions", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A "state" dynamic view targeting a definition with declared "state" features but no
    /// transitions succeeds (CollectStates populates from declared state features alone).
    /// </summary>
    [Fact]
    public void Synthesize_StateKind_HasStateFeatureNoTransitions_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Traffic"] = new SysmlDefinitionNode
                {
                    Name = "Traffic",
                    QualifiedName = "P::Traffic",
                    DefinitionKeyword = "state def",
                    Children =
                    [
                        new SysmlFeatureNode { Name = "red", QualifiedName = "P::Traffic::red", FeatureKeyword = "state" },
                        new SysmlFeatureNode { Name = "green", QualifiedName = "P::Traffic::green", FeatureKeyword = "state" }
                    ]
                }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "state", "P::Traffic", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asStateTransitionDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>An "action" dynamic view targeting a definition with an "action" feature succeeds.</summary>
    [Fact]
    public void Synthesize_ActionKind_HasActionFeature_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Order"] = new SysmlDefinitionNode
                {
                    Name = "Order",
                    QualifiedName = "P::Order",
                    DefinitionKeyword = "action def",
                    Children = [new SysmlFeatureNode { Name = "process", QualifiedName = "P::Order::process", FeatureKeyword = "action" }]
                }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "action", "P::Order", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asActionFlowDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>An "action" dynamic view targeting a definition with a succession succeeds.</summary>
    [Fact]
    public void Synthesize_ActionKind_HasSuccession_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Order"] = new SysmlDefinitionNode
                {
                    Name = "Order",
                    QualifiedName = "P::Order",
                    DefinitionKeyword = "action def",
                    Children = [new SysmlTransitionNode { Source = "Start", Target = "End" }]
                }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "action", "P::Order", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
    }

    /// <summary>An "action" dynamic view targeting a definition with neither successions nor action features fails.</summary>
    [Fact]
    public void Synthesize_ActionKind_NoActionsOrSuccessions_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Order"] = new SysmlDefinitionNode { Name = "Order", QualifiedName = "P::Order", DefinitionKeyword = "action def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "action", "P::Order", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("no successions or 'action' features", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>A "sequence" dynamic view targeting a definition with a nested message succeeds.</summary>
    [Fact]
    public void Synthesize_SequenceKind_HasMessage_Succeeds()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Protocol"] = new SysmlDefinitionNode
                {
                    Name = "Protocol",
                    QualifiedName = "P::Protocol",
                    DefinitionKeyword = "part def",
                    Children = [new SysmlConnectionNode { ConnectionKeyword = "message", EndpointA = "client.send", EndpointB = "server.recv" }]
                }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "sequence", "P::Protocol", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("asSequenceDiagram", viewNode!.RenderTargetName);
    }

    /// <summary>
    /// A "sequence" dynamic view targeting a definition with no nested messages fails — the known,
    /// documented cheap pre-check gap (a target with lifelines but zero messages is not
    /// representable here since lifelines are derived solely from message endpoints).
    /// </summary>
    [Fact]
    public void Synthesize_SequenceKind_NoMessages_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Protocol"] = new SysmlDefinitionNode { Name = "Protocol", QualifiedName = "P::Protocol", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "sequence", "P::Protocol", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("no nested messages", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>An unrecognized --view-type value fails with a diagnostic listing valid values.</summary>
    [Fact]
    public void Synthesize_UnrecognizedViewType_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "not-a-real-kind", "P::Widget", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("unrecognized --view-type", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>An unresolved --view-target fails with a diagnostic.</summary>
    [Fact]
    public void Synthesize_UnresolvedTarget_Fails()
    {
        var workspace = new SysmlWorkspace();

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::DoesNotExist", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("was not found", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>A --view-target resolving to a view node (wrong kind) fails with a diagnostic.</summary>
    [Fact]
    public void Synthesize_TargetIsView_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::SomeView"] = new SysmlViewNode { Name = "SomeView", QualifiedName = "P::SomeView" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::SomeView", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("view", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>A --view-target resolving to a transition (wrong kind) fails with a diagnostic.</summary>
    [Fact]
    public void Synthesize_TargetIsTransition_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::T1"] = new SysmlTransitionNode { Name = "T1", QualifiedName = "P::T1", Source = "A", Target = "B" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::T1", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("transition", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>A --view-target resolving to a standard-library element fails with a diagnostic.</summary>
    [Fact]
    public void Synthesize_TargetIsStdlib_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["CustomLib::Helper"] = new SysmlDefinitionNode { Name = "Helper", QualifiedName = "CustomLib::Helper", DefinitionKeyword = "part def" }
            },
            StdlibNames = new HashSet<string>(StringComparer.Ordinal) { "CustomLib::Helper" }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "CustomLib::Helper", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("standard-library", diagnostic!, StringComparison.Ordinal);
    }

    /// <summary>The --filter expression text is passed through unchanged to the synthesized node.</summary>
    [Fact]
    public void Synthesize_FilterExpression_PassedThroughUnchanged()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::Widget", "@Safety", out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Equal("@Safety", viewNode!.FilterExpressionText);
    }

    /// <summary>A null --filter results in a null FilterExpressionText on the synthesized node.</summary>
    [Fact]
    public void Synthesize_NoFilterExpression_ResultsInNullFilterExpressionText()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::Widget", null, out var diagnostic);

        Assert.Null(diagnostic);
        Assert.NotNull(viewNode);
        Assert.Null(viewNode!.FilterExpressionText);
    }

    /// <summary>
    /// A pre-existing declaration whose qualified name collides with the synthesized view's
    /// reserved <c>$</c>-prefixed name yields a diagnostic rather than silently overwriting it.
    /// </summary>
    [Fact]
    public void Synthesize_NameCollision_Fails()
    {
        var workspace = new SysmlWorkspace
        {
            Declarations = new Dictionary<string, SysmlNode>
            {
                ["P::Widget"] = new SysmlDefinitionNode { Name = "Widget", QualifiedName = "P::Widget", DefinitionKeyword = "part def" },
                ["$P::Widget"] = new SysmlDefinitionNode { Name = "Collision", QualifiedName = "$P::Widget", DefinitionKeyword = "part def" }
            }
        };

        var viewNode = DiagramRenderer.SynthesizeDynamicView(workspace, "general", "P::Widget", null, out var diagnostic);

        Assert.Null(viewNode);
        Assert.NotNull(diagnostic);
        Assert.Contains("already exists", diagnostic!, StringComparison.Ordinal);
    }
}
