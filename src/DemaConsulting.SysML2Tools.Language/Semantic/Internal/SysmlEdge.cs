// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Classifies the kind of resolved reference a <see cref="SysmlEdge"/> represents.
/// </summary>
public enum SysmlEdgeKind
{
    /// <summary>
    ///     A specialization reference (<see cref="SysmlNode.SupertypeNames"/> / <c>specializes</c> / <c>:&gt;</c>).
    /// </summary>
    Supertype,

    /// <summary>
    ///     A feature typing reference (<see cref="SysmlFeatureNode.FeatureTyping"/> / the type after <c>:</c>).
    /// </summary>
    Typing,

    /// <summary>
    ///     An import reference (<see cref="SysmlNode.ImportedNames"/> / <c>import X::Y</c> or <c>import X::*</c>).
    /// </summary>
    Import,
}

/// <summary>
///     A resolved directed reference between two qualified names in the semantic model, used to
///     build reverse-lookup indexes for impact analysis and the <c>query</c> command.
/// </summary>
/// <param name="SourceQualifiedName">
///     Qualified name of the referencing node, or <see langword="null"/> when the referencing
///     node is anonymous (e.g., an unnamed import statement or inline feature).
/// </param>
/// <param name="TargetQualifiedName">Qualified name of the resolved target symbol.</param>
/// <param name="Kind">The kind of reference this edge represents.</param>
public sealed record SysmlEdge(string? SourceQualifiedName, string TargetQualifiedName, SysmlEdgeKind Kind);
