// <copyright file="ConnectorLabelPlacerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="ConnectorLabelPlacer"/>.
/// </summary>
public sealed class ConnectorLabelPlacerTests
{
    /// <summary>A line without a label is omitted from the result.</summary>
    [Fact]
    public void Place_LineWithoutLabel_IsOmitted()
    {
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(100, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: null);

        var result = ConnectorLabelPlacer.Place([line], fontSize: 12);

        Assert.Empty(result);
    }

    /// <summary>A single labelled line is placed at the midpoint of its longest segment.</summary>
    [Fact]
    public void Place_SingleLine_UsesLongestSegmentMidpoint()
    {
        // A short vertical stub then a long horizontal run: the label should land on the long run.
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(0, 10), new Point2D(200, 10)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[guard]");

        var result = ConnectorLabelPlacer.Place([line], fontSize: 12);

        var (x, y) = result[line];
        Assert.Equal(100, x, precision: 3);
        Assert.Equal(10, y, precision: 3);
    }

    /// <summary>Two labels whose preferred positions coincide are separated so they do not overlap.</summary>
    [Fact]
    public void Place_CollidingLabels_AreSeparated()
    {
        // Two lines whose longest-segment midpoints are the same point.
        var a = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[atFloor]");
        var b = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[timeout]");

        var result = ConnectorLabelPlacer.Place([a, b], fontSize: 12);

        var posA = result[a];
        var posB = result[b];

        // The first keeps the preferred midpoint; the second is nudged away vertically.
        Assert.Equal(100, posA.X, precision: 3);
        Assert.Equal(0, posA.Y, precision: 3);
        Assert.NotEqual(posB.Y, posA.Y, precision: 3);
    }
}
