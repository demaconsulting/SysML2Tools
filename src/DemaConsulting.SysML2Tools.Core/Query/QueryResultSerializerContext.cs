// <copyright file="QueryResultSerializerContext.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Source-generator context for serializing <see cref="QueryResult"/> to JSON, mirroring
///     the AOT-safe source-gen pattern used by
///     <c>DemaConsulting.SysML2Tools.Semantic.Model.AstSerializerContext</c> in the Language
///     project.
/// </summary>
[JsonSerializable(typeof(QueryResult))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class QueryResultSerializerContext : JsonSerializerContext
{
}
