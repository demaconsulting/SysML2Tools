// <copyright file="DiagramTypeRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Tests for <see cref="DiagramTypeRouter"/> view-kind dispatch.
/// </summary>
public sealed class DiagramTypeRouterTests
{
    /// <summary>A view whose name contains "Interconnection" routes to the interconnection strategy.</summary>
    [Fact]
    public void GetStrategy_InterconnectionNamedView_ReturnsInterconnectionStrategy()
    {
        var view = new SysmlViewNode { Name = "VehicleInterconnectionView", QualifiedName = "M::VehicleInterconnectionView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<InterconnectionViewLayoutStrategy>(strategy);
    }

    /// <summary>A view specializing an interconnection view definition routes to that strategy.</summary>
    [Fact]
    public void GetStrategy_ViewSpecializingInterconnection_ReturnsInterconnectionStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            SupertypeNames = ["InterconnectionView"]
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<InterconnectionViewLayoutStrategy>(strategy);
    }

    /// <summary>An ordinary view routes to the general view strategy.</summary>
    [Fact]
    public void GetStrategy_PlainView_ReturnsGeneralViewStrategy()
    {
        var view = new SysmlViewNode { Name = "GeneralView", QualifiedName = "M::GeneralView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>A view whose name contains "StateTransition" routes to the state-transition strategy.</summary>
    [Fact]
    public void GetStrategy_StateTransitionNamedView_ReturnsStateStrategy()
    {
        var view = new SysmlViewNode { Name = "TrafficStateTransitionView", QualifiedName = "M::TrafficStateTransitionView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<StateTransitionViewLayoutStrategy>(strategy);
    }

    /// <summary>A view whose name contains "ActionFlow" routes to the action-flow strategy.</summary>
    [Fact]
    public void GetStrategy_ActionFlowNamedView_ReturnsActionFlowStrategy()
    {
        var view = new SysmlViewNode { Name = "OrderActionFlowView", QualifiedName = "M::OrderActionFlowView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<ActionFlowViewLayoutStrategy>(strategy);
    }

    /// <summary>A view whose name contains "Matrix" routes to the grid strategy.</summary>
    [Fact]
    public void GetStrategy_MatrixNamedView_ReturnsGridStrategy()
    {
        var view = new SysmlViewNode { Name = "SpecializationMatrixView", QualifiedName = "M::SpecializationMatrixView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<GridViewLayoutStrategy>(strategy);
    }

    /// <summary>A view whose name contains "Browser" routes to the browser strategy.</summary>
    [Fact]
    public void GetStrategy_BrowserNamedView_ReturnsBrowserStrategy()
    {
        var view = new SysmlViewNode { Name = "CatalogBrowserView", QualifiedName = "M::CatalogBrowserView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<BrowserViewLayoutStrategy>(strategy);
    }

    /// <summary>A view whose name contains "Sequence" routes to the sequence strategy.</summary>
    [Fact]
    public void GetStrategy_SequenceNamedView_ReturnsSequenceStrategy()
    {
        var view = new SysmlViewNode { Name = "ProtocolSequenceView", QualifiedName = "M::ProtocolSequenceView" };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<SequenceViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asTreeDiagram;</c> routes to the browser strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsTreeDiagram_ReturnsBrowserStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asTreeDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<BrowserViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asInterconnectionDiagram;</c> routes to the interconnection strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsInterconnectionDiagram_ReturnsInterconnectionStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asInterconnectionDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<InterconnectionViewLayoutStrategy>(strategy);
    }

    /// <summary>
    /// A declared <c>render</c> target takes precedence over a conflicting name/supertype heuristic match.
    /// </summary>
    [Fact]
    public void GetStrategy_RenderTargetPrecedenceOverridesNameHeuristic()
    {
        var view = new SysmlViewNode
        {
            Name = "TrafficStateTransitionView",
            QualifiedName = "M::TrafficStateTransitionView",
            RenderTargetName = "asTreeDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<BrowserViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asElementTable;</c> falls through unchanged to the existing heuristic.</summary>
    [Fact]
    public void GetStrategy_RenderAsElementTable_FallsThroughUnchanged()
    {
        var view = new SysmlViewNode
        {
            Name = "GeneralView",
            QualifiedName = "M::GeneralView",
            RenderTargetName = "asElementTable"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asTextualNotation;</c> falls through unchanged to the existing heuristic.</summary>
    [Fact]
    public void GetStrategy_RenderAsTextualNotation_FallsThroughUnchanged()
    {
        var view = new SysmlViewNode
        {
            Name = "GeneralView",
            QualifiedName = "M::GeneralView",
            RenderTargetName = "asTextualNotation"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>An unrecognized <c>render</c> target falls through unchanged to the existing heuristic.</summary>
    [Fact]
    public void GetStrategy_UnrecognizedRenderTarget_FallsThroughUnchanged()
    {
        var view = new SysmlViewNode
        {
            Name = "GeneralView",
            QualifiedName = "M::GeneralView",
            RenderTargetName = "asSomethingUnrecognized"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>
    /// A render target that is a near-miss of <c>asTreeDiagram</c> (wrong case) does not match: the
    /// comparison is exact and case-sensitive, so the view falls through unchanged to the existing
    /// heuristic instead of routing to the browser strategy.
    /// </summary>
    [Fact]
    public void GetStrategy_RenderTargetWrongCase_DoesNotMatchAndFallsThroughUnchanged()
    {
        var view = new SysmlViewNode
        {
            Name = "GeneralView",
            QualifiedName = "M::GeneralView",
            RenderTargetName = "ASTreeDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>
    /// A render target with trailing whitespace after <c>asTreeDiagram</c> does not match: the
    /// comparison is exact, so the view falls through unchanged to the existing heuristic instead
    /// of routing to the browser strategy.
    /// </summary>
    [Fact]
    public void GetStrategy_RenderTargetTrailingWhitespace_DoesNotMatchAndFallsThroughUnchanged()
    {
        var view = new SysmlViewNode
        {
            Name = "GeneralView",
            QualifiedName = "M::GeneralView",
            RenderTargetName = "asTreeDiagram "
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asGeneralDiagram;</c> routes to the general strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsGeneralDiagram_ReturnsGeneralViewStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asGeneralDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asStateTransitionDiagram;</c> routes to the state-transition strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsStateTransitionDiagram_ReturnsStateTransitionStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asStateTransitionDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<StateTransitionViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asActionFlowDiagram;</c> routes to the action-flow strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsActionFlowDiagram_ReturnsActionFlowStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asActionFlowDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<ActionFlowViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asSequenceDiagram;</c> routes to the sequence strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsSequenceDiagram_ReturnsSequenceStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asSequenceDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<SequenceViewLayoutStrategy>(strategy);
    }

    /// <summary>A view declaring <c>render asGridDiagram;</c> routes to the grid strategy.</summary>
    [Fact]
    public void GetStrategy_RenderAsGridDiagram_ReturnsGridStrategy()
    {
        var view = new SysmlViewNode
        {
            Name = "MyView",
            QualifiedName = "M::MyView",
            RenderTargetName = "asGridDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType<GridViewLayoutStrategy>(strategy);
    }

    /// <summary>
    /// A view declaring <c>render asGeneralDiagram;</c> takes precedence over a conflicting
    /// name/supertype heuristic match.
    /// </summary>
    [Fact]
    public void GetStrategy_RenderAsGeneralDiagram_PrecedenceOverridesNameHeuristic()
    {
        var view = new SysmlViewNode
        {
            Name = "TrafficStateTransitionView",
            QualifiedName = "M::TrafficStateTransitionView",
            RenderTargetName = "asGeneralDiagram"
        };
        var workspace = new SysmlWorkspace();

        var strategy = DiagramTypeRouter.GetStrategy(view, workspace, out _);

        Assert.IsType<GeneralViewLayoutStrategy>(strategy);
    }

    /// <summary>
    /// A <c>Views::</c>-qualified render target selects the same strategy as its bare name. Only
    /// <c>asTreeDiagram</c> and <c>asInterconnectionDiagram</c> are members of the standard <c>Views</c>
    /// package (the form SysML v2.0 requires, since <c>Views</c> is not implicitly imported); the other
    /// cases verify the router's tolerance of the tool-extension tokens, which are not valid SysML when
    /// qualified.
    /// </summary>
    /// <param name="renderTarget">The qualified render target text.</param>
    /// <param name="expected">The expected strategy type.</param>
    [Theory]
    [InlineData("Views::asTreeDiagram", typeof(BrowserViewLayoutStrategy))]
    [InlineData("Views::asInterconnectionDiagram", typeof(InterconnectionViewLayoutStrategy))]
    [InlineData("Views::asGeneralDiagram", typeof(GeneralViewLayoutStrategy))]
    [InlineData("Views::asStateTransitionDiagram", typeof(StateTransitionViewLayoutStrategy))]
    [InlineData("Views::asActionFlowDiagram", typeof(ActionFlowViewLayoutStrategy))]
    [InlineData("Views::asSequenceDiagram", typeof(SequenceViewLayoutStrategy))]
    [InlineData("Views::asGridDiagram", typeof(GridViewLayoutStrategy))]
    public void GetStrategy_QualifiedRenderTarget_RoutesLikeBareName(string renderTarget, Type expected)
    {
        var view = new SysmlViewNode
        {
            Name = "PlainView",
            QualifiedName = "M::PlainView",
            RenderTargetName = renderTarget
        };

        var strategy = DiagramTypeRouter.GetStrategy(view, new SysmlWorkspace(), out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType(expected, strategy);
    }

    /// <summary>
    /// Parsed from source, a view's <c>render Views::asXDiagram;</c> (qualified) and
    /// <c>private import Views::*; render asXDiagram;</c> (imported, then bare) both select the
    /// browser/interconnection strategies; the bare form without an import is still tolerated.
    /// </summary>
    [Fact]
    public async Task GetStrategy_RenderQualifiedOrImported_RoutesToBrowserAndInterconnection()
    {
        var workspace = await LoadAsync("""
            package Qualified {
                view Tree { render Views::asTreeDiagram; }
                view Wired { render Views::asInterconnectionDiagram; }
            }
            package Imported {
                private import Views::*;
                view Tree { render asTreeDiagram; }
                view Wired { render asInterconnectionDiagram; }
            }
            package Bare {
                view Tree { render asTreeDiagram; }
            }
            """);

        foreach (var package in new[] { "Qualified", "Imported" })
        {
            Assert.IsType<BrowserViewLayoutStrategy>(DiagramTypeRouter.GetStrategy(workspace.Declarations[$"{package}::Tree"], workspace, out _));
            Assert.IsType<InterconnectionViewLayoutStrategy>(DiagramTypeRouter.GetStrategy(workspace.Declarations[$"{package}::Wired"], workspace, out _));
        }

        Assert.IsType<BrowserViewLayoutStrategy>(DiagramTypeRouter.GetStrategy(workspace.Declarations["Bare::Tree"], workspace, out _));
    }

    /// <summary>
    /// A view usage typed by a standard view definition (SysML v2 §9.2.20) routes to that
    /// definition's notation, regardless of a conflicting name heuristic.
    /// </summary>
    /// <param name="definition">The standard view definition's simple name.</param>
    /// <param name="expected">The expected strategy type.</param>
    [Theory]
    [InlineData("GeneralView", typeof(GeneralViewLayoutStrategy))]
    [InlineData("InterconnectionView", typeof(InterconnectionViewLayoutStrategy))]
    [InlineData("ActionFlowView", typeof(ActionFlowViewLayoutStrategy))]
    [InlineData("StateTransitionView", typeof(StateTransitionViewLayoutStrategy))]
    [InlineData("SequenceView", typeof(SequenceViewLayoutStrategy))]
    [InlineData("GridView", typeof(GridViewLayoutStrategy))]
    [InlineData("BrowserView", typeof(BrowserViewLayoutStrategy))]
    public async Task GetStrategy_ViewTypedByStandardViewDefinition_RoutesToItsNotation(string definition, Type expected)
    {
        var workspace = await LoadAsync($$"""
            package P {
                private import StandardViewDefinitions::*;
                view MatrixTreeView : {{definition}};
            }
            """);

        var strategy = DiagramTypeRouter.GetStrategy(workspace.Declarations["P::MatrixTreeView"], workspace, out var unsupported);

        Assert.Null(unsupported);
        Assert.IsType(expected, strategy);
    }

    /// <summary>
    /// A view usage typed by a user view definition inherits the notation of the standard view
    /// definition it specializes, and a definition's own <c>render</c> member takes precedence.
    /// </summary>
    [Fact]
    public async Task GetStrategy_ViewTypedByUserViewDefinition_InheritsNotationAndRendering()
    {
        var workspace = await LoadAsync("""
            package P {
                view def SystemsView :> StandardViewDefinitions::InterconnectionView;
                view def OutlineView :> SystemsView { render Views::asTreeDiagram; }
                view A : SystemsView;
                view B : OutlineView;
            }
            """);

        Assert.IsType<InterconnectionViewLayoutStrategy>(DiagramTypeRouter.GetStrategy(workspace.Declarations["P::A"], workspace, out _));
        Assert.IsType<BrowserViewLayoutStrategy>(DiagramTypeRouter.GetStrategy(workspace.Declarations["P::B"], workspace, out _));
    }

    /// <summary>A view usage's own <c>render</c> member overrides its standard view definition.</summary>
    [Fact]
    public async Task GetStrategy_RenderOnTypedView_OverridesViewDefinition()
    {
        var workspace = await LoadAsync("""
            package P {
                view V : StandardViewDefinitions::GeneralView { render Views::asInterconnectionDiagram; }
            }
            """);

        Assert.IsType<InterconnectionViewLayoutStrategy>(DiagramTypeRouter.GetStrategy(workspace.Declarations["P::V"], workspace, out _));
    }

    /// <summary>Loads SysML source text into a workspace with the standard library.</summary>
    private static async Task<SysmlWorkspace> LoadAsync(string sysml)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return result.Workspace;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
