// <copyright file="HighwayAssignerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="HighwayAssigner"/> corridor detection and bundling.
/// </summary>
public sealed class HighwayAssignerTests
{
    /// <summary>An empty graph yields an empty result.</summary>
    [Fact]
    public void HighwayAssigner_Assign_EmptyGraph_ReturnsEmpty()
    {
        // Act: assign with no boxes and no edges
        var result = HighwayAssigner.Assign([], [], gridUnit: 10, wireSpacing: 6, minGap: 20);

        // Assert: nothing detected, nothing assigned
        Assert.Empty(result.Corridors);
        Assert.Empty(result.Assignments);
        Assert.Empty(result.CostMultipliers);
    }

    /// <summary>Two rows with a single wire form one corridor that is not a highway.</summary>
    [Fact]
    public void HighwayAssigner_Assign_TwoBoxesOneWire_OneCorridorNoHighway()
    {
        // Arrange: two stacked boxes with one connecting wire
        var boxes = new[]
        {
            new HighwayBox(0, 0, 100, 40, "a"),
            new HighwayBox(0, 200, 100, 40, "b"),
        };
        var edges = new[] { new HighwayEdge(0, 1, "connection") };

        // Act
        var result = HighwayAssigner.Assign(boxes, edges, gridUnit: 10, wireSpacing: 6, minGap: 100);

        // Assert: a single horizontal corridor, carrying one wire, below the highway threshold
        Assert.Single(result.Corridors);
        Assert.Equal(0, result.Assignments[0].CorridorIndex);
        Assert.False(result.Corridors[0].IsHighway);
    }

    /// <summary>A six-box fan into a common row drives peak occupancy at least three lanes wide.</summary>
    [Fact]
    public void HighwayAssigner_Assign_SixBoxFan_PeakLanesAtLeastThreeAndHighway()
    {
        // Arrange: five top sources fanning down into one bottom hub through a shared corridor
        var boxes = new[]
        {
            new HighwayBox(0, 0, 60, 40, "s0"),
            new HighwayBox(100, 0, 60, 40, "s1"),
            new HighwayBox(200, 0, 60, 40, "s2"),
            new HighwayBox(300, 0, 60, 40, "s3"),
            new HighwayBox(400, 0, 60, 40, "s4"),
            new HighwayBox(200, 300, 60, 40, "hub"),
        };
        var edges = new[]
        {
            new HighwayEdge(0, 5, "connection"),
            new HighwayEdge(1, 5, "connection"),
            new HighwayEdge(2, 5, "connection"),
            new HighwayEdge(3, 5, "connection"),
            new HighwayEdge(4, 5, "connection"),
        };

        // Act
        var result = HighwayAssigner.Assign(boxes, edges, gridUnit: 10, wireSpacing: 6, minGap: 20);

        // Assert: at least three lanes overlap and the busiest corridor is a highway
        var highway = result.Corridors.First(c => c.IsHighway);
        var lanes = (highway.ReservedWidth - (2 * 20)) / 6;
        Assert.True(lanes >= 3, "expected at least three concurrent lanes");
    }

    /// <summary>A highway corridor discounts routing cost; an ordinary corridor stays neutral.</summary>
    [Fact]
    public void HighwayAssigner_Assign_Multipliers_HighwayCheaperNormalNeutral()
    {
        // Arrange: a dense fan (highway) plus a lone wire (non-highway)
        var boxes = new[]
        {
            new HighwayBox(0, 0, 60, 40, "s0"),
            new HighwayBox(100, 0, 60, 40, "s1"),
            new HighwayBox(200, 0, 60, 40, "s2"),
            new HighwayBox(200, 300, 60, 40, "hub"),
        };
        var edges = new[]
        {
            new HighwayEdge(0, 3, "connection"),
            new HighwayEdge(1, 3, "connection"),
            new HighwayEdge(2, 3, "connection"),
        };

        // Act
        var result = HighwayAssigner.Assign(boxes, edges, gridUnit: 10, wireSpacing: 6, minGap: 20);

        // Assert: highway multipliers are below 1.0; any non-highway corridor stays at 1.0
        for (var i = 0; i < result.Corridors.Count; i++)
        {
            if (result.Corridors[i].IsHighway)
            {
                Assert.True(result.CostMultipliers[i] < 1.0);
            }
            else
            {
                Assert.Equal(1.0, result.CostMultipliers[i], 6);
            }
        }

        Assert.Contains(result.Corridors, c => c.IsHighway);
    }
}
