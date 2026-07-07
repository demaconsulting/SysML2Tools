// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Io;

/// <summary>
/// Resolves file glob patterns into concrete, deduplicated file paths on disk.
/// </summary>
/// <remarks>
/// <see cref="GlobFileCollector"/> is the single entry point: it accepts an ordered list of
/// glob patterns (with optional <c>!</c>-prefixed exclusions), a set of file extensions used to
/// filter bare-<c>*</c> patterns, and a working directory used to resolve relative patterns, and
/// returns a stable, sorted, deduplicated list of absolute file paths. It unifies the file
/// discovery behavior previously duplicated (and, for the <c>render</c>/<c>query</c> commands,
/// entirely absent) across the <c>lint</c>, <c>render</c>, and <c>query</c> CLI commands, so all
/// three support the same recursive-glob (<c>**</c>) and exclusion (<c>!pattern</c>) semantics.
/// <para>
/// This utility has no dependency on the SysML semantic model or rendering pipeline — it is a
/// pure filesystem-discovery helper reused by the Tool project's command implementations
/// (<c>LintCommand</c>, <c>RenderCommand</c>, <c>QueryCommand</c>) ahead of calling
/// <c>WorkspaceLoader.LoadAsync</c> (<see cref="Semantic.WorkspaceLoader"/>), which itself takes
/// only concrete, already-resolved file paths.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var files = GlobFileCollector.Collect(
///     patterns: ["src/**/*.sysml", "!src/generated/**"],
///     extensions: [".sysml", ".kerml"],
///     workingDirectory: Directory.GetCurrentDirectory());
///
/// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
/// var loadResult = await WorkspaceLoader.LoadAsync(files, stdlibTable);
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
