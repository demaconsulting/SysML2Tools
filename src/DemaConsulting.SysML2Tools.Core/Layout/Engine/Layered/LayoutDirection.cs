// <copyright file="LayoutDirection.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// The primary flow direction of a layered layout.
/// </summary>
/// <remarks>
/// Stages always compute in the RIGHT-equivalent abstract axes (along = layer progression on
/// the +X axis, cross = within-layer on the +Y axis). A final <see cref="AxisTransform"/> stage
/// rotates or flips those abstract coordinates onto screen coordinates for the requested
/// direction, isolating all direction handling to a single unit.
/// </remarks>
internal enum LayoutDirection
{
    /// <summary>Layers progress left-to-right (the abstract, identity direction).</summary>
    Right,

    /// <summary>Layers progress top-to-bottom.</summary>
    Down,

    /// <summary>Layers progress right-to-left.</summary>
    Left,

    /// <summary>Layers progress bottom-to-top.</summary>
    Up,
}
