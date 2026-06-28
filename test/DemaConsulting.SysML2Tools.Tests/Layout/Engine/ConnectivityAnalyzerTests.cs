// <copyright file="ConnectivityAnalyzerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="ConnectivityAnalyzer"/> topology analysis.
/// </summary>
public sealed class ConnectivityAnalyzerTests
{
    /// <summary>An empty graph yields empty results.</summary>
    [Fact]
    public void Analyze_Empty_ReturnsEmpty()
    {
        var result = ConnectivityAnalyzer.Analyze([], []);

        Assert.Empty(result.LayerHints);
        Assert.Empty(result.CommunityIds);
        Assert.Empty(result.Adjacency);
    }

    /// <summary>A single node sits at layer 0 in community 0.</summary>
    [Fact]
    public void Analyze_SingleNode_LayerZeroCommunityZero()
    {
        var result = ConnectivityAnalyzer.Analyze([new ConnectivityNode("a")], []);

        Assert.Equal(0, result.LayerHints[0]);
        Assert.Equal(0, result.CommunityIds[0]);
        Assert.Empty(result.Adjacency[0]);
    }

    /// <summary>A chain A->B->C assigns layers 0,1,2 and a single community.</summary>
    [Fact]
    public void Analyze_Chain_LayersAndOneCommunity()
    {
        var nodes = new[] { new ConnectivityNode("a"), new ConnectivityNode("b"), new ConnectivityNode("c") };
        var edges = new[] { new ConnectivityEdge(0, 1), new ConnectivityEdge(1, 2) };

        var result = ConnectivityAnalyzer.Analyze(nodes, edges);

        Assert.Equal([0, 1, 2], result.LayerHints);
        Assert.Equal(result.CommunityIds[0], result.CommunityIds[1]);
        Assert.Equal(result.CommunityIds[1], result.CommunityIds[2]);
    }

    /// <summary>A 5-spoke star (hub-and-spoke) collapses to one community.</summary>
    [Fact]
    public void Analyze_Star_SameCommunity()
    {
        var nodes = Enumerable.Range(0, 6).Select(i => new ConnectivityNode($"n{i}")).ToList();
        var edges = Enumerable.Range(1, 5).Select(i => new ConnectivityEdge(0, i)).ToList();

        var result = ConnectivityAnalyzer.Analyze(nodes, edges);

        var hub = result.CommunityIds[0];
        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(hub, result.CommunityIds[i]);
        }
    }

    /// <summary>Two disconnected components get different community ids.</summary>
    [Fact]
    public void Analyze_TwoComponents_DifferentCommunities()
    {
        var nodes = Enumerable.Range(0, 4).Select(i => new ConnectivityNode($"n{i}")).ToList();
        var edges = new[] { new ConnectivityEdge(0, 1), new ConnectivityEdge(2, 3) };

        var result = ConnectivityAnalyzer.Analyze(nodes, edges);

        Assert.NotEqual(result.CommunityIds[0], result.CommunityIds[2]);
        Assert.Equal(result.CommunityIds[0], result.CommunityIds[1]);
        Assert.Equal(result.CommunityIds[2], result.CommunityIds[3]);
    }

    /// <summary>Identical input yields identical analysis.</summary>
    [Fact]
    public void Analyze_SameInput_IsDeterministic()
    {
        var nodes = Enumerable.Range(0, 4).Select(i => new ConnectivityNode($"n{i}")).ToList();
        var edges = new[] { new ConnectivityEdge(0, 1), new ConnectivityEdge(1, 2), new ConnectivityEdge(2, 3) };

        var a = ConnectivityAnalyzer.Analyze(nodes, edges);
        var b = ConnectivityAnalyzer.Analyze(nodes, edges);

        Assert.Equal(a.LayerHints, b.LayerHints);
        Assert.Equal(a.CommunityIds, b.CommunityIds);
    }
}
