// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Export;

/// <summary>
///     Envelope for the <c>export</c> command's <c>--format json</c> output: the full resolved
///     semantic model (declarations, edges, diagnostics) as a single document.
/// </summary>
/// <remarks>
///     Deliberately reuses <see cref="SysmlNode"/>/<see cref="SysmlEdge"/>/
///     <see cref="SysmlDiagnostic"/> directly rather than introducing a fourth parallel shape —
///     see <c>docs/design/sysml2-tools-tool/export.md</c> for the rationale (this is a bulk,
///     lossless, round-trip-capable dump, the opposite goal from <c>query</c>'s narrow
///     <see cref="Query.QueryResult"/> summarization shape, so the two are not shared).
///     <see cref="Declarations"/> is a qualified-name-keyed dictionary (not an array) because
///     qualified names are already unique keys, giving an agent O(1) lookup by name without an
///     extra index pass; <see cref="Edges"/>/<see cref="Diagnostics"/> have no natural unique key
///     and are therefore arrays.
/// </remarks>
internal sealed record ExportResult
{
    /// <summary>
    ///     Gets the exported declarations, keyed by fully-qualified name.
    /// </summary>
    /// <remarks>
    ///     Excludes OMG standard-library declarations unless <c>--include-stdlib</c> was
    ///     supplied — see <see cref="ExportCommand"/>'s stdlib-filtering remarks.
    /// </remarks>
    public required IReadOnlyDictionary<string, SysmlNode> Declarations { get; init; }

    /// <summary>
    ///     Gets the exported resolved reference edges.
    /// </summary>
    /// <remarks>
    ///     Excludes any edge whose source or target is an OMG standard-library qualified name,
    ///     unless <c>--include-stdlib</c> was supplied.
    /// </remarks>
    public required IReadOnlyList<SysmlEdge> Edges { get; init; }

    /// <summary>
    ///     Gets the parse/semantic-resolution diagnostics produced while loading the workspace.
    /// </summary>
    /// <remarks>
    ///     Never stdlib-filtered: <see cref="Parser.WorkspaceParser"/> diagnostics only ever
    ///     originate from the user's own files, since the stdlib symbol table is a pre-resolved
    ///     seed loaded via <c>StdlibProvider.GetSymbolTable()</c>, not re-parsed per invocation.
    /// </remarks>
    public required IReadOnlyList<SysmlDiagnostic> Diagnostics { get; init; }
}

/// <summary>
///     One JSON Lines record for a declaration, emitted by <c>--format jsonl</c>.
/// </summary>
/// <param name="Kind">Always <c>"declaration"</c>; the JSONL discriminator field.</param>
/// <param name="QualifiedName">The declaration's fully-qualified name (the dictionary key it
///     would otherwise occupy in <see cref="ExportResult.Declarations"/>).</param>
/// <param name="Node">The declaration node itself (polymorphic; see <see cref="SysmlNode"/>).</param>
internal sealed record ExportDeclarationLine(string Kind, string QualifiedName, SysmlNode Node)
{
    /// <summary>
    ///     Creates an <see cref="ExportDeclarationLine"/> for the given qualified name/node pair.
    /// </summary>
    /// <param name="qualifiedName">The declaration's fully-qualified name.</param>
    /// <param name="node">The declaration node.</param>
    /// <returns>The constructed line record, with <see cref="Kind"/> set to <c>"declaration"</c>.</returns>
    public static ExportDeclarationLine Create(string qualifiedName, SysmlNode node) =>
        new("declaration", qualifiedName, node);
}

/// <summary>
///     One JSON Lines record for an edge, emitted by <c>--format jsonl</c>.
/// </summary>
/// <param name="Kind">Always <c>"edge"</c>; the JSONL discriminator field.</param>
/// <param name="SourceQualifiedName">See <see cref="SysmlEdge.SourceQualifiedName"/>.</param>
/// <param name="TargetQualifiedName">See <see cref="SysmlEdge.TargetQualifiedName"/>.</param>
/// <param name="EdgeKind">See <see cref="SysmlEdge.Kind"/>.</param>
internal sealed record ExportEdgeLine(string Kind, string? SourceQualifiedName, string TargetQualifiedName, SysmlEdgeKind EdgeKind)
{
    /// <summary>
    ///     Creates an <see cref="ExportEdgeLine"/> for the given edge.
    /// </summary>
    /// <param name="edge">The edge to flatten into a JSONL line.</param>
    /// <returns>The constructed line record, with <see cref="Kind"/> set to <c>"edge"</c>.</returns>
    public static ExportEdgeLine Create(SysmlEdge edge) =>
        new("edge", edge.SourceQualifiedName, edge.TargetQualifiedName, edge.Kind);
}

/// <summary>
///     One JSON Lines record for a diagnostic, emitted by <c>--format jsonl</c>.
/// </summary>
/// <param name="Kind">Always <c>"diagnostic"</c>; the JSONL discriminator field.</param>
/// <param name="FilePath">See <see cref="SysmlDiagnostic.FilePath"/>.</param>
/// <param name="Line">See <see cref="SysmlDiagnostic.Line"/>.</param>
/// <param name="Column">See <see cref="SysmlDiagnostic.Column"/>.</param>
/// <param name="Severity">See <see cref="SysmlDiagnostic.Severity"/>.</param>
/// <param name="Message">See <see cref="SysmlDiagnostic.Message"/>.</param>
internal sealed record ExportDiagnosticLine(string Kind, string FilePath, int Line, int Column, DiagnosticSeverity Severity, string Message)
{
    /// <summary>
    ///     Creates an <see cref="ExportDiagnosticLine"/> for the given diagnostic.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to flatten into a JSONL line.</param>
    /// <returns>The constructed line record, with <see cref="Kind"/> set to <c>"diagnostic"</c>.</returns>
    public static ExportDiagnosticLine Create(SysmlDiagnostic diagnostic) =>
        new("diagnostic", diagnostic.FilePath, diagnostic.Line, diagnostic.Column, diagnostic.Severity, diagnostic.Message);
}
