// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Parser;

/// <summary>
/// Low-level SysML/KerML syntax parsing (source text to concrete syntax tree).
/// </summary>
/// <remarks>
/// This namespace performs syntax-only parsing: <c>WorkspaceParser.ParseSource</c> and
/// <c>ParseSourceToCst</c> turn source text into diagnostics (and, for the latter, an
/// ANTLR-generated concrete syntax tree). No symbol table, reference resolution, or
/// specialization walking happens here.
/// <para>
/// Most consumers should not need this namespace directly — use
/// <see cref="Semantic.WorkspaceLoader"/> instead, which wraps parsing together with symbol
/// registration and reference resolution to produce a fully-resolved
/// <see cref="Semantic.SysmlWorkspace"/>. This namespace is intended for lower-level scenarios
/// such as syntax-only validation of a single file without loading the standard library.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
