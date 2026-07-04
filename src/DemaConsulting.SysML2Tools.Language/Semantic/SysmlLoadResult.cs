// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Result of loading a SysML/KerML workspace including semantic analysis.
/// </summary>
/// <param name="Workspace">The semantic workspace, or null if loading failed entirely.</param>
/// <param name="Diagnostics">All diagnostics (parse errors, semantic warnings) from the load operation.</param>
/// <remarks>
///     <see cref="Workspace"/> is non-null for every result returned by
///     <see cref="WorkspaceLoader.LoadAsync"/> today — even a workspace containing only parse
///     errors still returns a (possibly empty) <see cref="SysmlWorkspace"/>. Check
///     <see cref="HasErrors"/> (or filter <see cref="Diagnostics"/> by <c>Severity</c>) to detect
///     problems; do not rely on <c>Workspace is null</c> as an error signal.
/// </remarks>
/// <example>
/// <code>
/// var result = await WorkspaceLoader.LoadAsync(["model.sysml"], stdlibTable);
/// foreach (var diagnostic in result.Diagnostics)
/// {
///     Console.WriteLine($"{diagnostic.FilePath}:{diagnostic.Line}:{diagnostic.Column} " +
///         $"{diagnostic.Severity}: {diagnostic.Message}");
/// }
///
/// if (result.HasErrors)
/// {
///     return; // one or more error-severity diagnostics were reported
/// }
///
/// // result.Workspace is safe to use here
/// </code>
/// </example>
public sealed record SysmlLoadResult(
    SysmlWorkspace? Workspace,
    IReadOnlyList<SysmlDiagnostic> Diagnostics)
{
    /// <summary>
    ///     Gets a value indicating whether the result contains any error-level diagnostics.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
