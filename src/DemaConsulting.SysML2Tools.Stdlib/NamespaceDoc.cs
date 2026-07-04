// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

namespace DemaConsulting.SysML2Tools.Stdlib;

/// <summary>
/// Provides the pre-compiled SysML v2 standard library symbol table.
/// </summary>
/// <remarks>
/// <see cref="StdlibProvider.GetSymbolTable"/> returns the symbol table produced by
/// pre-compiling the embedded KerML/SysML standard library at build time (see
/// <c>StdlibGen</c>), so it is available on first call without re-parsing the library source.
/// The returned symbol table is the required seed for
/// <see cref="Semantic.WorkspaceLoader.LoadAsync"/> — without it, user models that reference
/// standard-library types (e.g. <c>ScalarValues::Real</c>, <c>Base::Anything</c>) fail to
/// resolve.
/// </remarks>
/// <example>
/// <code>
/// var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
/// var result = await WorkspaceLoader.LoadAsync(["model.sysml"], stdlibTable);
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
