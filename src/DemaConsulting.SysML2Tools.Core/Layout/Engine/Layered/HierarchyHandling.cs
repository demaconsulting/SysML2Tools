// <copyright file="HierarchyHandling.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Layout.Engine.Layered;

/// <summary>
/// How a layered layout treats nested (compound) nodes.
/// </summary>
/// <remarks>
/// Only <see cref="Flat"/> is exercised by the current behavior-preserving extraction;
/// <see cref="Recursive"/> is reserved scaffolding for a later phase that lays out each
/// container's children bottom-up and treats each container as a fixed-size atomic node.
/// </remarks>
internal enum HierarchyHandling
{
    /// <summary>Run the pipeline once over a single flat graph.</summary>
    Flat,

    /// <summary>Run the flat pipeline per container, bottom-up (reserved for a later phase).</summary>
    Recursive,
}
