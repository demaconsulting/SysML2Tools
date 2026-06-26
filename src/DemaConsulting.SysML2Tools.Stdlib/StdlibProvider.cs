// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Stdlib;

/// <summary>
///     Provides the pre-compiled SysML v2 standard library symbol table.
/// </summary>
public static class StdlibProvider
{
    /// <summary>
    ///     Cached stdlib symbol table and diagnostics, loaded once on first access.
    /// </summary>
    private static readonly Lazy<(SymbolTable Table, IReadOnlyList<SysmlDiagnostic> Diagnostics)>
        _cached = new(LoadFromResource);

    /// <summary>
    ///     Returns the pre-compiled stdlib <see cref="SymbolTable"/> (deserialized once, cached forever).
    /// </summary>
    /// <returns>
    ///     A tuple of the stdlib symbol table and any diagnostics produced during pre-compilation.
    /// </returns>
    public static (SymbolTable Table, IReadOnlyList<SysmlDiagnostic> Diagnostics) GetSymbolTable()
        => _cached.Value;

    /// <summary>
    ///     Loads and deserializes the embedded stdlib.bin resource.
    /// </summary>
    private static (SymbolTable, IReadOnlyList<SysmlDiagnostic>) LoadFromResource()
    {
        using var stream = typeof(StdlibProvider).Assembly
            .GetManifestResourceStream("DemaConsulting.SysML2Tools.Stdlib.stdlib.bin")
            ?? throw new InvalidOperationException(
                "Embedded resource 'DemaConsulting.SysML2Tools.Stdlib.stdlib.bin' not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return AstDeserializer.Deserialize(ms.ToArray());
    }
}
