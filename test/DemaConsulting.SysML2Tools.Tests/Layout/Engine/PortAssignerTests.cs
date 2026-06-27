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
}
