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
/// (see the <c>DemaConsulting.SysML2Tools.Semantic.Internal</c> namespace for the node types,
/// e.g. <c>SysmlDefinitionNode</c>, <c>SysmlFeatureNode</c>, <c>SysmlViewNode</c>).
/// <para>
/// <see cref="WorkspaceLoader.LoadAsync"/> should always be seeded with the pre-compiled
/// standard library symbol table from <c>StdlibProvider.GetSymbolTable()</c>
/// (the Stdlib project's <c>StdlibProvider</c> class), so that user models can reference
/// stdlib types (e.g. <c>ScalarValues::Real</c>) without re-parsing the library on every load.
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
