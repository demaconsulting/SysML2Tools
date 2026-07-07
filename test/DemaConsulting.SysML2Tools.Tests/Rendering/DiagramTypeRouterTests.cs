// <copyright file="DiagramTypeRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

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
}
