// <copyright file="LayeredLayoutMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// Fixed spacing and tolerance constants shared by every layered-layout stage.
/// </summary>
/// <remarks>
/// These values are intentionally identical to the constants previously embedded in the
/// monolithic interconnection engine; the extraction preserves them exactly so the pipeline
/// reproduces the legacy output byte for byte.
/// </remarks>
internal static class LayeredLayoutMetrics
{
    /// <summary>Vertical gap between adjacent nodes stacked within the same layer.</summary>
    internal const double NodeSpacing = 30.0;

    /// <summary>Minimum corridor width (node-node spacing) between adjacent columns.</summary>
    internal const double CorridorMinWidth = 70.0;

    /// <summary>Slot-to-slot spacing within a corridor (ELK edgeEdgeSpacing).</summary>
    internal const double EdgeSpacing = 16.0;

    /// <summary>Clearance from corridor edge to the nearest routing slot (ELK edgeNodeSpacing).</summary>
    internal const double ConnectorClearance = 10.0;

    /// <summary>Uniform padding added around the placed content.</summary>
    internal const double Padding = 20.0;

    /// <summary>Number of Barycenter ordering sweeps (down + up = one round).</summary>
    internal const int BarycentricSweeps = 4;

    /// <summary>Tolerance for treating a segment as straight (no bend points needed).</summary>
    internal const double StraightTolerance = 1e-6;
}
