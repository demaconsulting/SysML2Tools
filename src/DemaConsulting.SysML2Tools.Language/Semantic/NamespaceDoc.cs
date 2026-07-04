// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
/// Loads SysML/KerML source files into a semantically-resolved workspace.
/// </summary>
/// <remarks>
/// This is the primary "getting started" namespace for consumers of the library:
/// <see cref="WorkspaceLoader"/> parses one or more source files, resolves references,
/// walks specialization (supertype) chains, and returns a <see cref="SysmlWorkspace"/> whose
/// <see cref="SysmlWorkspace.Declarations"/> map fully-qualified names to the resolved AST
/// (see the <c>DemaConsulting.SysML2Tools.Semantic.Model</c> namespace for the node types,
/// e.g. <c>SysmlDefinitionNode</c>, <c>SysmlFeatureNode</c>, <c>SysmlViewNode</c>).
/// <para>
/// Real-world SysML v2 models almost universally reference standard-library types (e.g.
/// <c>ScalarValues::Real</c>, <c>Base::Anything</c>) for primitive attribute types and common
/// base definitions. <see cref="WorkspaceLoader.LoadAsync"/> should be seeded with
/// <c>StdlibProvider.GetSymbolTable()</c> (the Stdlib project's <c>StdlibProvider</c> class) for
/// any model exercising these; without the seed, such references resolve to unresolved-reference
/// Warning diagnostics rather than a loaded declaration. Omitting the seed is only appropriate
/// for isolated syntax-only checks (e.g. parser unit tests) that do not exercise reference
/// resolution.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
/// var result = await WorkspaceLoader.LoadAsync([path], stdlibTable);
///
/// if (result.Workspace!.Declarations.ContainsKey("Foo"))
/// {
///     // "Foo" was declared in the loaded file(s)
/// }
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
