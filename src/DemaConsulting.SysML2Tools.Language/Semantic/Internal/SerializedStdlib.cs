// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Data-transfer object for serializing/deserializing the pre-compiled stdlib binary.
/// </summary>
internal sealed record SerializedStdlib(
    Dictionary<string, SysmlNode> Symbols,
    IReadOnlyList<SysmlDiagnostic> Diagnostics);
