// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Svg;
using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests that members flagged <see cref="SysmlNode.IsExcludedFromDiagrams"/> — modeled for name
///     resolution only (bare parameters, the bodies of behavior usages, ...) — never change a
///     diagram: for each strategy, a workspace with such members renders exactly as the same
///     workspace without them.
/// </summary>
public sealed class ExcludedMembersLayoutTests
{
    /// <summary>Lays out <paramref name="workspace"/> with <paramref name="strategy"/> and renders it to SVG text.</summary>
    private static string Svg(ILayoutStrategy strategy, SysmlWorkspace workspace)
    {
        var options = new RenderOptions(Themes.Light);
        var layout = strategy.BuildLayout(new ViewContext("v", workspace), options);
        using var stream = new MemoryStream();
        new SvgRenderer().Render(layout, options, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Builds a workspace whose declarations are <paramref name="roots"/> and everything nested in them.</summary>
    private static SysmlWorkspace WorkspaceOf(params SysmlNode[] roots)
    {
        var declarations = new Dictionary<string, SysmlNode>(StringComparer.Ordinal);
        void Add(SysmlNode node)
        {
            if (node.QualifiedName is { Length: > 0 } name)
            {
                declarations[name] = node;
            }

            foreach (var child in node.Children)
            {
                Add(child);
            }
        }

        foreach (var root in roots)
        {
            Add(root);
        }

        return new SysmlWorkspace { Declarations = declarations };
    }

    /// <summary>Returns a hidden feature (a bare parameter), as <c>AstBuilder</c> builds it.</summary>
    private static SysmlFeatureNode Hidden(string owner, string name, string keyword = "ref") => new()
    {
        Name = name,
        QualifiedName = $"{owner}::{name}",
        FeatureKeyword = keyword,
        Direction = SysmlFeatureDirection.In,
        FeatureTyping = "Real",
        IsExcludedFromDiagrams = true,
    };

    /// <summary>Hidden members of a part definition add no compartment row and no box to the General view.</summary>
    [Fact]
    public void GeneralView_BuildLayout_ExcludedMembers_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var children = new List<SysmlNode>
            {
                new SysmlFeatureNode { Name = "mass", QualifiedName = "P::Engine::mass", FeatureKeyword = "attribute", FeatureTyping = "Real" },
                new SysmlFeatureNode { Name = "shaft", QualifiedName = "P::Engine::shaft", FeatureKeyword = "port" },
            };
            if (withHidden)
            {
                children.Add(Hidden("P::Engine", "x"));
                children.Add(Hidden("P::Engine", "doIt", "perform"));
                children.Add(Hidden("P::Engine", "slice", "timeslice"));
                children.Add(new SysmlFeatureNode
                {
                    Name = "hiddenPart",
                    QualifiedName = "P::Engine::hiddenPart",
                    FeatureKeyword = "part",
                    IsExcludedFromDiagrams = true,
                });
            }

            // The port's only member is hidden, so the port stays a compartment row, not a box.
            var port = (SysmlFeatureNode)children[1];
            children[1] = new SysmlFeatureNode
            {
                Name = port.Name,
                QualifiedName = port.QualifiedName,
                FeatureKeyword = "port",
                Children = withHidden ? [Hidden("P::Engine::shaft", "signal")] : [],
            };

            var engine = new SysmlDefinitionNode
            {
                Name = "Engine",
                QualifiedName = "P::Engine",
                DefinitionKeyword = "part def",
                Children = children,
            };
            var hiddenDef = new SysmlDefinitionNode
            {
                Name = "Hidden",
                QualifiedName = "P::Engine::Hidden",
                DefinitionKeyword = "part def",
                IsExcludedFromDiagrams = true,
            };
            return withHidden ? WorkspaceOf(engine, hiddenDef) : WorkspaceOf(engine);
        }

        var strategy = new GeneralViewLayoutStrategy();

        // Act
        var without = Svg(strategy, Build(withHidden: false));
        var with = Svg(strategy, Build(withHidden: true));

        // Assert
        Assert.Equal(without, with);
    }

    /// <summary>A hidden declaration is not listed by the Browser view.</summary>
    [Fact]
    public void BrowserView_BuildLayout_ExcludedMembers_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var children = new List<SysmlNode> { new SysmlFeatureNode { Name = "a", QualifiedName = "P::Root::a", FeatureKeyword = "part" } };
            if (withHidden)
            {
                children.Add(Hidden("P::Root", "x"));
            }

            var root = new SysmlDefinitionNode { Name = "Root", QualifiedName = "P::Root", DefinitionKeyword = "part def", Children = children };
            return WorkspaceOf(new SysmlPackageNode { Name = "P", QualifiedName = "P", Children = [root] });
        }

        var strategy = new BrowserViewLayoutStrategy();

        // Act / Assert
        Assert.Equal(Svg(strategy, Build(false)), Svg(strategy, Build(true)));
    }

    /// <summary>A hidden definition is not a row or column of the Grid view.</summary>
    [Fact]
    public void GridView_BuildLayout_ExcludedDefinition_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var nodes = new List<SysmlNode>
            {
                new SysmlDefinitionNode { Name = "A", QualifiedName = "P::A", DefinitionKeyword = "part def" },
                new SysmlDefinitionNode { Name = "B", QualifiedName = "P::B", DefinitionKeyword = "part def", SupertypeNames = ["A"] },
            };
            if (withHidden)
            {
                nodes.Add(new SysmlDefinitionNode
                {
                    Name = "C",
                    QualifiedName = "P::host::C",
                    DefinitionKeyword = "part def",
                    SupertypeNames = ["A"],
                    IsExcludedFromDiagrams = true,
                });
            }

            return WorkspaceOf([.. nodes]);
        }

        var strategy = new GridViewLayoutStrategy();

        // Act / Assert
        Assert.Equal(Svg(strategy, Build(false)), Svg(strategy, Build(true)));
    }

    /// <summary>
    ///     A calculation definition whose (hidden) body holds more actions and successions than the
    ///     real action definition is not chosen as the Action Flow root, and adds nothing to it.
    /// </summary>
    [Fact]
    public void ActionFlowView_BuildLayout_ExcludedBodies_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var process = new SysmlDefinitionNode
            {
                Name = "Process",
                QualifiedName = "P::Process",
                DefinitionKeyword = "action def",
                Children =
                [
                    new SysmlFeatureNode { Name = "a", QualifiedName = "P::Process::a", FeatureKeyword = "action" },
                    new SysmlFeatureNode { Name = "b", QualifiedName = "P::Process::b", FeatureKeyword = "action" },
                    new SysmlTransitionNode { Source = "a", Target = "b" },
                ],
            };
            if (!withHidden)
            {
                return WorkspaceOf(process);
            }

            var law = new SysmlDefinitionNode
            {
                Name = "Law",
                QualifiedName = "P::Law",
                DefinitionKeyword = "calc def",
                IsExcludedFromDiagrams = true,
                Children =
                [
                    new SysmlFeatureNode { Name = "x", QualifiedName = "P::Law::x", FeatureKeyword = "action", IsExcludedFromDiagrams = true },
                    new SysmlFeatureNode { Name = "y", QualifiedName = "P::Law::y", FeatureKeyword = "action", IsExcludedFromDiagrams = true },
                    new SysmlFeatureNode { Name = "z", QualifiedName = "P::Law::z", FeatureKeyword = "action", IsExcludedFromDiagrams = true },
                    new SysmlTransitionNode { Source = "x", Target = "y", IsExcludedFromDiagrams = true },
                    new SysmlTransitionNode { Source = "y", Target = "z", IsExcludedFromDiagrams = true },
                ],
            };
            var carrier = new SysmlDefinitionNode
            {
                Name = "Carrier",
                QualifiedName = "P::Carrier",
                DefinitionKeyword = "action def",
                Children = [Hidden("P::Carrier", "p"), Hidden("P::Carrier", "step", "action")],
            };
            return WorkspaceOf(process, law, carrier);
        }

        var strategy = new ActionFlowViewLayoutStrategy();

        // Act / Assert
        Assert.Equal(Svg(strategy, Build(false)), Svg(strategy, Build(true)));
    }

    /// <summary>A state definition whose (hidden) transitions outnumber the real ones is not chosen as the State Transition root.</summary>
    [Fact]
    public void StateTransitionView_BuildLayout_ExcludedTransitions_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var light = new SysmlDefinitionNode
            {
                Name = "Light",
                QualifiedName = "SM::Light",
                DefinitionKeyword = "state def",
                Children =
                [
                    new SysmlFeatureNode { Name = "off", QualifiedName = "SM::Light::off", FeatureKeyword = "state" },
                    new SysmlFeatureNode { Name = "on", QualifiedName = "SM::Light::on", FeatureKeyword = "state" },
                    new SysmlTransitionNode { Source = "off", Target = "on" },
                ],
            };
            if (!withHidden)
            {
                return WorkspaceOf(light);
            }

            var other = new SysmlDefinitionNode
            {
                Name = "Other",
                QualifiedName = "SM::Other",
                DefinitionKeyword = "case def",
                IsExcludedFromDiagrams = true,
                Children =
                [
                    new SysmlTransitionNode { Source = "p", Target = "q", IsExcludedFromDiagrams = true },
                    new SysmlTransitionNode { Source = "q", Target = "r", IsExcludedFromDiagrams = true },
                ],
            };
            return WorkspaceOf(light, other);
        }

        var strategy = new StateTransitionViewLayoutStrategy();

        // Act / Assert
        Assert.Equal(Svg(strategy, Build(false)), Svg(strategy, Build(true)));
    }

    /// <summary>
    ///     A definition whose (hidden) body holds more messages than the real protocol is not chosen as
    ///     the Sequence root, and hidden lifelines and messages of the protocol are not drawn.
    /// </summary>
    [Fact]
    public void SequenceView_BuildLayout_ExcludedMessagesAndLifelines_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var children = new List<SysmlNode>
            {
                new SysmlFeatureNode { Name = "client", QualifiedName = "P::Protocol::client", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "server", QualifiedName = "P::Protocol::server", FeatureKeyword = "part" },
                new SysmlConnectionNode { Name = "request", ConnectionKeyword = "message", EndpointA = "client.a", EndpointB = "server.b" },
            };
            if (withHidden)
            {
                children.Add(new SysmlFeatureNode
                {
                    Name = "observer",
                    QualifiedName = "P::Protocol::observer",
                    FeatureKeyword = "part",
                    IsExcludedFromDiagrams = true,
                });
                children.Add(new SysmlConnectionNode
                {
                    Name = "note",
                    ConnectionKeyword = "message",
                    EndpointA = "observer.x",
                    EndpointB = "client.y",
                    IsExcludedFromDiagrams = true,
                });
            }

            var protocol = new SysmlDefinitionNode
            {
                Name = "Protocol",
                QualifiedName = "P::Protocol",
                DefinitionKeyword = "part def",
                Children = children,
            };
            if (!withHidden)
            {
                return WorkspaceOf(protocol);
            }

            var chatty = new SysmlDefinitionNode
            {
                Name = "Chatty",
                QualifiedName = "P::Chatty",
                DefinitionKeyword = "case def",
                IsExcludedFromDiagrams = true,
                Children =
                [
                    new SysmlConnectionNode { Name = "m1", ConnectionKeyword = "message", EndpointA = "a.x", EndpointB = "b.y", IsExcludedFromDiagrams = true },
                    new SysmlConnectionNode { Name = "m2", ConnectionKeyword = "message", EndpointA = "b.x", EndpointB = "a.y", IsExcludedFromDiagrams = true },
                ],
            };
            return WorkspaceOf(protocol, chatty);
        }

        var strategy = new SequenceViewLayoutStrategy();

        // Act / Assert
        Assert.Equal(Svg(strategy, Build(false)), Svg(strategy, Build(true)));
    }

    /// <summary>A hidden part usage is not drawn by the Interconnection view.</summary>
    [Fact]
    public void InterconnectionView_BuildLayout_ExcludedPartUsage_RenderIdenticallyToWorkspaceWithout()
    {
        // Arrange
        static SysmlWorkspace Build(bool withHidden)
        {
            var children = new List<SysmlNode>
            {
                new SysmlFeatureNode { Name = "a", QualifiedName = "P::Sys::a", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "b", QualifiedName = "P::Sys::b", FeatureKeyword = "part" },
                new SysmlConnectionNode
                {
                    Name = "link",
                    QualifiedName = "P::Sys::link",
                    ConnectionKeyword = "connection",
                    EndpointA = "a",
                    EndpointB = "b",
                },
            };
            if (withHidden)
            {
                children.Add(new SysmlFeatureNode
                {
                    Name = "c",
                    QualifiedName = "P::Sys::c",
                    FeatureKeyword = "part",
                    IsExcludedFromDiagrams = true,
                });
            }

            return WorkspaceOf(new SysmlDefinitionNode
            {
                Name = "Sys",
                QualifiedName = "P::Sys",
                DefinitionKeyword = "part def",
                Children = children,
            });
        }

        var strategy = new InterconnectionViewLayoutStrategy();

        // Act / Assert
        Assert.Equal(Svg(strategy, Build(false)), Svg(strategy, Build(true)));
    }

    /// <summary>
    ///     The auto view targets the part definition with the most <em>drawn</em> children: hidden
    ///     members do not make a definition the target, and a hidden definition is never one.
    /// </summary>
    [Fact]
    public void DiagramRenderer_SynthesizeAutoView_ExcludedMembers_DoNotChangeTarget()
    {
        // Arrange
        var crowded = new SysmlDefinitionNode
        {
            Name = "Crowded",
            QualifiedName = "P::Crowded",
            DefinitionKeyword = "part def",
            Children =
            [
                Hidden("P::Crowded", "a"),
                Hidden("P::Crowded", "b"),
                Hidden("P::Crowded", "c"),
                new SysmlFeatureNode { Name = "own", QualifiedName = "P::Crowded::own", FeatureKeyword = "part" },
            ],
        };
        var drawn = new SysmlDefinitionNode
        {
            Name = "Drawn",
            QualifiedName = "P::Drawn",
            DefinitionKeyword = "part def",
            Children =
            [
                new SysmlFeatureNode { Name = "p1", QualifiedName = "P::Drawn::p1", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "p2", QualifiedName = "P::Drawn::p2", FeatureKeyword = "part" },
            ],
        };
        var inside = new SysmlDefinitionNode
        {
            Name = "Inside",
            QualifiedName = "P::run::Inside",
            DefinitionKeyword = "part def",
            IsExcludedFromDiagrams = true,
            Children =
            [
                new SysmlFeatureNode { Name = "q1", QualifiedName = "P::run::Inside::q1", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "q2", QualifiedName = "P::run::Inside::q2", FeatureKeyword = "part" },
                new SysmlFeatureNode { Name = "q3", QualifiedName = "P::run::Inside::q3", FeatureKeyword = "part" },
            ],
        };
        var workspace = WorkspaceOf(crowded, drawn, inside);

        // Act
        var view = DiagramRenderer.SynthesizeAutoView(workspace);

        // Assert
        Assert.NotNull(view);
        Assert.Equal("P::DrawnView", view.QualifiedName);
    }

    /// <summary>The dynamic-view compatibility pre-check ignores hidden members.</summary>
    [Theory]
    [InlineData("interconnection", "part def", "part")]
    [InlineData("state", "state def", "state")]
    [InlineData("action", "action def", "action")]
    public void DynamicViewSynthesizer_Synthesize_OnlyExcludedChildren_FailsCompatibilityCheck(
        string viewType, string definitionKeyword, string childKeyword)
    {
        // Arrange
        var target = new SysmlDefinitionNode
        {
            Name = "T",
            QualifiedName = "P::T",
            DefinitionKeyword = definitionKeyword,
            Children = [Hidden("P::T", "h", childKeyword)],
        };
        var workspace = WorkspaceOf(target);

        // Act
        var view = DiagramRenderer.SynthesizeDynamicView(workspace, viewType, "P::T", null, out var diagnostic);

        // Assert
        Assert.Null(view);
        Assert.NotNull(diagnostic);
    }
}
