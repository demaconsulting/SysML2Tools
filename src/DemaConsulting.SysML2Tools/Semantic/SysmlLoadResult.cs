// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Result of loading a SysML/KerML workspace including semantic analysis.
/// </summary>
/// <param name="Workspace">The semantic workspace, or null if loading failed entirely.</param>
/// <param name="Diagnostics">All diagnostics (parse errors, semantic warnings) from the load operation.</param>
public sealed record SysmlLoadResult(
    SysmlWorkspace? Workspace,
    IReadOnlyList<SysmlDiagnostic> Diagnostics)
{
    /// <summary>
    ///     Gets a value indicating whether the result contains any error-level diagnostics.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
