// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Internal;

namespace DemaConsulting.SysML2Tools.Semantic;

/// <summary>
///     Deserializes a pre-compiled stdlib binary back to a <see cref="SymbolTable"/> and diagnostics.
/// </summary>
public static class AstDeserializer
{
    /// <summary>
    ///     Deserializes the given UTF-8 JSON bytes back to a symbol table and diagnostics.
    /// </summary>
    /// <param name="data">UTF-8 JSON bytes produced by <see cref="AstSerializer.Serialize"/>.</param>
    /// <returns>The deserialized symbol table and diagnostics.</returns>
    public static (SymbolTable Table, IReadOnlyList<SysmlDiagnostic> Diagnostics) Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var dto = JsonSerializer.Deserialize(data, AstSerializerContext.Default.SerializedStdlib)
            ?? throw new InvalidOperationException("Failed to deserialize stdlib binary.");
        return (new SymbolTable(dto.Symbols), dto.Diagnostics);
    }
}
