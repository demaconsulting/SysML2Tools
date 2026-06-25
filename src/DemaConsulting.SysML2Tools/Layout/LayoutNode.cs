// <copyright file="LayoutNode.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout;

/// <summary>
/// Abstract base for all layout nodes. Renderers switch on concrete type; unknown subtypes should be skipped for forward compatibility.
/// </summary>
// S2094: intentionally empty abstract record — acts as a discriminated-union root; no shared members are needed
#pragma warning disable S2094
public abstract record LayoutNode;
#pragma warning restore S2094
