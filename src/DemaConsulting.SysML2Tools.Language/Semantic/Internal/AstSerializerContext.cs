// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Source-generator context for serializing/deserializing the stdlib binary.
/// </summary>
[JsonSerializable(typeof(SerializedStdlib))]
[JsonSerializable(typeof(Dictionary<string, SysmlNode>))]
internal partial class AstSerializerContext : JsonSerializerContext
{
}
