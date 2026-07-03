// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
/// Defines the semantic-model AST node types produced by <see cref="WorkspaceLoader"/>.
/// </summary>
/// <remarks>
/// Despite the "Internal" folder name, the types in this namespace are public because they are
/// the concrete node types returned in <see cref="SysmlWorkspace.Declarations"/> — a consumer
/// pattern-matching over a loaded workspace needs to see them. <see cref="SysmlNode"/> is the
/// abstract base type; its subtypes include <c>SysmlDefinitionNode</c> ("part def", "attribute
/// def", ...), <c>SysmlFeatureNode</c> ("part", "port", "attribute", ...), <c>SysmlViewNode</c>,
/// <c>SysmlPackageNode</c>, and others. <see cref="SysmlEdge"/> and <see cref="SemanticIndex"/>
/// expose the resolved supertype/typing/import graph via <see cref="SysmlWorkspace.Index"/>, and
/// <see cref="SysmlAnnotation"/> captures <c>comment</c>/<c>documentation</c> members attached to
/// a node.
/// </remarks>
/// <example>
/// <code>
/// foreach (var (qualifiedName, node) in workspace.Declarations)
/// {
///     if (node is SysmlDefinitionNode { DefinitionKeyword: "part def" } partDef)
///     {
///         Console.WriteLine($"{qualifiedName}: {partDef.Children.Count} member(s)");
///     }
/// }
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
