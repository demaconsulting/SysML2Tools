// <copyright file="Rect.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine;

/// <summary>
/// An axis-aligned rectangle in logical pixels, shared across the layout engine.
/// </summary>
/// <param name="X">Absolute X coordinate of the left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the top edge in logical pixels.</param>
/// <param name="Width">Width in logical pixels.</param>
/// <param name="Height">Height in logical pixels.</param>
internal readonly record struct Rect(double X, double Y, double Width, double Height);
