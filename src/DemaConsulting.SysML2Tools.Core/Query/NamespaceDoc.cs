// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
/// Analyzes a loaded SysML v2 workspace and answers structured questions about its elements.
/// </summary>
/// <remarks>
/// <see cref="QueryEngine"/> is the entry point: twelve verb methods (<c>Uses</c>, <c>UsedBy</c>,
/// <c>Dependencies</c>, <c>Impact</c>, <c>Describe</c>, <c>Hierarchy</c>, <c>Requirements</c>,
/// <c>Interface</c>, <c>Connections</c>, <c>States</c>, <c>List</c>, <c>Find</c> — see
/// <see cref="QueryVerb"/>) each take a loaded <c>SysmlWorkspace</c>, a resolved target
/// <c>SysmlNode</c> (not required by <c>List</c>/<c>Find</c>), and a <see cref="QueryOptions"/>,
/// returning a uniform <see cref="QueryResult"/>. <see cref="QueryEngine.Execute"/> dispatches to
/// the right verb method from a single <see cref="QueryOptions.Verb"/> value, so a caller does
/// not need to write its own verb switch.
/// <para>
/// <see cref="QueryResultRenderer"/> renders a <see cref="QueryResult"/> as either Markdown lines
/// or a JSON string; <see cref="QueryResultExporter"/> additionally writes either rendering
/// directly to a file. <see cref="QueryArgumentParser"/> parses a token list (e.g., CLI arguments)
/// into a <see cref="QueryOptions"/> instance, for callers that accept the same command-line-style
/// grammar as the Tool project's <c>query</c> command.
/// </para>
/// <para>
/// This namespace has no dependency on any CLI/console concept — it depends only on
/// <c>DemaConsulting.SysML2Tools.Semantic</c>/<c>Semantic.Model</c> (the loaded workspace and its
/// resolved nodes/edges) and this project's own <see cref="Utilities.QualifiedNameShortener"/>
/// (used by <see cref="QueryResultRenderer"/> to compact the <c>dependencies</c> verb's Markdown
/// prose). It is reused by the Tool project's <c>query</c> CLI command
/// (<c>QueryCommand</c>/<c>QueryCliArgumentParser</c>), which layers glob-based file resolution,
/// workspace loading, element lookup, and console/log output on top of this namespace's pure,
/// library-friendly API.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
/// var loadResult = await WorkspaceLoader.LoadAsync(["Model.sysml"], stdlibTable);
/// var workspace = loadResult.Workspace!;
///
/// var options = new QueryOptions { Verb = QueryVerb.Uses, Element = "Model::Vehicle" };
/// workspace.Declarations.TryGetValue(options.Element!, out var element);
///
/// var result = QueryEngine.Execute(workspace, options, element);
/// QueryResultExporter.WriteMarkdown(result, "uses.md");
/// </code>
/// </example>
/// <example>
/// A non-CLI application (for example, SysML2Workbench) uses the same API without any console
/// or file I/O, and reads the structured traversal metadata on each entry rather than parsing
/// the human-readable <see cref="QueryResultEntry.Detail"/> text:
/// <code>
/// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
/// var loadResult = await WorkspaceLoader.LoadAsync(["Model.sysml"], stdlibTable);
/// var workspace = loadResult.Workspace!;
///
/// var options = new QueryOptions
/// {
///     Verb = QueryVerb.Impact,
///     Element = "Model::System::motorA",
///     IncludeConnections = true
/// };
/// workspace.Declarations.TryGetValue(options.Element!, out var element);
///
/// var result = QueryEngine.Impact(workspace, element!, options);
/// foreach (var entry in result.Entries)
/// {
///     // entry.Depth            : int?           - traversal depth; never parse "depth N" from Detail
///     // entry.Relation         : SysmlEdgeKind? - Supertype/Typing/... versus Connect/Binding
///     // entry.ViaQualifiedName : string?        - the far port a connection entry rolled up from
///     Console.WriteLine($"{entry.QualifiedName} {entry.Relation} @{entry.Depth} via {entry.ViaQualifiedName}");
/// }
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
