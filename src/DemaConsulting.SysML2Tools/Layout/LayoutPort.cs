// <copyright file="LayoutPort.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Side of a box that a port is attached to.
/// </summary>
public enum PortSide
{
    /// <summary>Port is on the top edge.</summary>
    Top,

    /// <summary>Port is on the bottom edge.</summary>
    Bottom,

    /// <summary>Port is on the left edge.</summary>
    Left,

    /// <summary>Port is on the right edge.</summary>
    Right,
}

/// <summary>
/// A port node pinned to the edge of its parent box. Position is absolute.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the port centre in logical pixels.</param>
/// <param name="CentreY">Absolute Y coordinate of the port centre in logical pixels.</param>
/// <param name="Side">Edge of the parent box that this port is attached to.</param>
/// <param name="Label">Optional text label displayed beside the port symbol.</param>
public sealed record LayoutPort(
    double CentreX,
    double CentreY,
    PortSide Side,
    string? Label) : LayoutNode;
