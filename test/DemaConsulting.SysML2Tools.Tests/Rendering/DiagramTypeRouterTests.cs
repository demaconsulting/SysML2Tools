// <copyright file="DiagramTypeRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

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
}
