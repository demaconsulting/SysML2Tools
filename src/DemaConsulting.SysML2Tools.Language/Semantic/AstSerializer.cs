// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Serializes a <see cref="SymbolTable"/> and diagnostics to a binary blob for embedding as a resource.
/// </summary>
public static class AstSerializer
{
    /// <summary>
    ///     Serializes the given symbol table and diagnostics to a UTF-8 JSON byte array.
    /// </summary>
    /// <param name="table">The symbol table to serialize.</param>
    /// <param name="diagnostics">The diagnostics to include.</param>
    /// <returns>UTF-8 JSON bytes.</returns>
    public static byte[] Serialize(SymbolTable table, IReadOnlyList<SysmlDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var dto = new SerializedStdlib(
            new Dictionary<string, SysmlNode>(table.Symbols, StringComparer.Ordinal),
            diagnostics.ToList());
        return JsonSerializer.SerializeToUtf8Bytes(dto, AstSerializerContext.Default.SerializedStdlib);
    }
}
