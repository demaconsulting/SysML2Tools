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

using System.Text.Json.Serialization;

namespace DemaConsulting.SysML2Tools.Export;

/// <summary>
///     Source-generator context for serializing the JSONL per-line wrapper records
///     (<see cref="ExportDeclarationLine"/>, <see cref="ExportEdgeLine"/>,
///     <see cref="ExportDiagnosticLine"/>) to compact, non-indented JSON — one record per line
///     (the <c>--format jsonl</c> path).
/// </summary>
/// <remarks>
///     Kept separate from <see cref="ExportResultSerializerContext"/> (which is
///     <c>WriteIndented = true</c> for the single-document <c>--format json</c> path) because
///     source-generated <see cref="JsonSerializerContext"/> types only support one context-level
///     indentation setting; JSONL's entire value proposition (one compact record per line, safe
///     for line-oriented tools like <c>grep</c>/<c>tail</c>) would be defeated by embedded
///     newlines from an indented serializer.
/// </remarks>
[JsonSerializable(typeof(ExportDeclarationLine))]
[JsonSerializable(typeof(ExportEdgeLine))]
[JsonSerializable(typeof(ExportDiagnosticLine))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal partial class ExportLineSerializerContext : JsonSerializerContext
{
}
