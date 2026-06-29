// <copyright file="PortAssignerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Layout.Engine;

namespace DemaConsulting.SysML2Tools.Tests.Layout.Engine;

/// <summary>
///     Tests for <see cref="PortAssigner"/> port-side assignment and slot distribution.
/// </summary>
public sealed class PortAssignerTests
{
    /// <summary>A box at the origin; ports are assigned to the side facing their target.</summary>
    [Theory]
    [InlineData(500, 50, PortSide.Right)]
    [InlineData(-500, 50, PortSide.Left)]
    [InlineData(50, 500, PortSide.Bottom)]
    [InlineData(50, -500, PortSide.Top)]
    public void Assign_SinglePort_ChoosesSideFacingTarget(double towardX, double towardY, PortSide expected)
    {
        // Arrange: a 100x100 box at the origin with one port heading toward the target
        var box = new Rect(0, 0, 100, 100);
        var requests = new[] { new PortRequest(box, new Point2D(towardX, towardY)) };

        // Act
        var placements = PortAssigner.Assign(requests);

        // Assert: the port is on the expected side
        Assert.Single(placements);
        Assert.Equal(expected, placements[0].Side);
    }

    /// <summary>A port's centre lies on the boundary of its assigned side.</summary>
    [Fact]
    public void Assign_Port_CentreLiesOnBoxBoundary()
    {
        var box = new Rect(10, 20, 100, 80);
        var requests = new[] { new PortRequest(box, new Point2D(1000, 60)) };

        var placements = PortAssigner.Assign(requests);

        // Right side: x == box right edge, y within the box vertical extent
        Assert.Equal(PortSide.Right, placements[0].Side);
        Assert.Equal(110.0, placements[0].CentreX, 6);
        Assert.InRange(placements[0].CentreY, 20.0, 100.0);
    }

    /// <summary>Multiple ports on the same side are distributed to distinct, evenly spaced slots.</summary>
    [Fact]
    public void Assign_MultiplePortsSameSide_AreEvenlyDistributed()
    {
        // Arrange: three ports all heading right, so all land on the right side
        var box = new Rect(0, 0, 100, 120);
        var requests = new[]
        {
            new PortRequest(box, new Point2D(500, 10)),
            new PortRequest(box, new Point2D(500, 60)),
            new PortRequest(box, new Point2D(500, 110)),
        };

        // Act
        var placements = PortAssigner.Assign(requests);

        // Assert: all on the right side at distinct Y positions
        Assert.All(placements, p => Assert.Equal(PortSide.Right, p.Side));
        var ys = placements.Select(p => p.CentreY).OrderBy(y => y).ToList();
        Assert.True(ys[0] < ys[1] && ys[1] < ys[2], "Ports should occupy distinct slots.");

        // Evenly spaced at 1/4, 2/4, 3/4 of the height
        Assert.Equal(30.0, ys[0], 6);
        Assert.Equal(60.0, ys[1], 6);
        Assert.Equal(90.0, ys[2], 6);
    }

    /// <summary>An empty request list yields no placements.</summary>
    [Fact]
    public void Assign_Empty_ReturnsEmpty()
    {
        Assert.Empty(PortAssigner.Assign([]));
    }

    /// <summary>Ports sharing side, direction, corridor, and connector type merge to one trunk group.</summary>
    [Fact]
    public void AssignHighway_SameKey_ShareTrunkGroup()
    {
        // Arrange: two outgoing ports on the same box heading right, same corridor and connector type
        var box = new Rect(0, 0, 100, 100);
        var requests = new[]
        {
            new HighwayPortRequest(box, new Point2D(500, 10), "flow", CorridorId: 3, IsOutgoing: true),
            new HighwayPortRequest(box, new Point2D(500, 90), "flow", CorridorId: 3, IsOutgoing: true),
        };

        // Act
        var placements = PortAssigner.AssignHighway(requests, approachZone: 18.0);

        // Assert: both ports collapse onto a single trunk with a shared positive group id
        Assert.NotEqual(-1, placements[0].TrunkGroupId);
        Assert.Equal(placements[0].TrunkGroupId, placements[1].TrunkGroupId);
        Assert.Equal(placements[0].CentreX, placements[1].CentreX, 6);
        Assert.Equal(placements[0].CentreY, placements[1].CentreY, 6);
    }

    /// <summary>A differing connector type prevents merging, producing distinct trunk groups.</summary>
    [Fact]
    public void AssignHighway_DifferentConnectorType_DistinctGroups()
    {
        // Arrange: two outgoing ports, same corridor/side, but different connector types
        var box = new Rect(0, 0, 100, 100);
        var requests = new[]
        {
            new HighwayPortRequest(box, new Point2D(500, 10), "flow", CorridorId: 3, IsOutgoing: true),
            new HighwayPortRequest(box, new Point2D(500, 90), "binding", CorridorId: 3, IsOutgoing: true),
        };

        // Act
        var placements = PortAssigner.AssignHighway(requests, approachZone: 18.0);

        // Assert: each port forms its own trunk group
        Assert.NotEqual(placements[0].TrunkGroupId, placements[1].TrunkGroupId);
    }

    /// <summary>A port with corridor id -1 stays independent (trunk group -1).</summary>
    [Fact]
    public void AssignHighway_NoCorridor_StaysIndependent()
    {
        var box = new Rect(0, 0, 100, 100);
        var requests = new[] { new HighwayPortRequest(box, new Point2D(500, 50), "flow", CorridorId: -1, IsOutgoing: true) };

        var placements = PortAssigner.AssignHighway(requests, approachZone: 18.0);

        Assert.Equal(-1, placements[0].TrunkGroupId);
    }

    /// <summary>A merged group of two ports forms its trunk one approach zone off the face.</summary>
    [Fact]
    public void AssignHighway_MergedGroup_TrunkSitsOffFace()
    {
        // Arrange: two outgoing ports heading right share a corridor, so they merge off the right face
        var box = new Rect(0, 0, 100, 100);
        var requests = new[]
        {
            new HighwayPortRequest(box, new Point2D(500, 20), "flow", CorridorId: 2, IsOutgoing: true),
            new HighwayPortRequest(box, new Point2D(500, 80), "flow", CorridorId: 2, IsOutgoing: true),
        };

        // Act
        var placements = PortAssigner.AssignHighway(requests, approachZone: 18.0);

        // Assert: the trunk sits one approach zone beyond the right face (x = 100 + 18)
        Assert.Equal(PortSide.Right, placements[0].Side);
        Assert.Equal(118.0, placements[0].CentreX, 6);
    }

    /// <summary>A single corridor port routes to the face midpoint, not an off-face merge point.</summary>
    [Fact]
    public void AssignHighway_SingleCorridorPort_RoutesToFace()
    {
        // Arrange: one outgoing port on a corridor heads right
        var box = new Rect(0, 0, 100, 100);
        var requests = new[] { new HighwayPortRequest(box, new Point2D(500, 50), "flow", CorridorId: 2, IsOutgoing: true) };

        // Act
        var placements = PortAssigner.AssignHighway(requests, approachZone: 18.0);

        // Assert: the port sits on the right face, not stepped off it
        Assert.Equal(100.0, placements[0].CentreX, 6);
    }

    /// <summary>On a short face, many ports compress to the minimum slot width centred on the face.</summary>
    [Fact]
    public void Assign_ManyPortsOnShortFace_UsesMinimumSlot()
    {
        // Arrange: a 30-wide top face cannot hold five even slots, so it compresses to 11px slots
        var box = new Rect(0, 0, 30, 100);
        var requests = new[]
        {
            new PortRequest(box, new Point2D(-5, -500)),
            new PortRequest(box, new Point2D(0, -500)),
            new PortRequest(box, new Point2D(5, -500)),
            new PortRequest(box, new Point2D(10, -500)),
            new PortRequest(box, new Point2D(15, -500)),
        };

        // Act
        var placements = PortAssigner.Assign(requests);

        // Assert: adjacent ports are exactly 11px apart and centred on the face midpoint (x=15)
        var xs = placements.Select(p => p.CentreX).OrderBy(x => x).ToList();
        Assert.Equal(11.0, xs[1] - xs[0], 6);
        Assert.Equal(15.0, (xs[0] + xs[^1]) / 2.0, 6);
    }
}
