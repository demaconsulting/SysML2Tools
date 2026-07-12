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

using System.Text.Json;
using DemaConsulting.SysML2Tools.Export;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Utilities;

namespace DemaConsulting.SysML2Tools.Tests.Export;

/// <summary>
///     JSON/JSONL shape assertions for the Export subsystem: <c>$type</c> discriminator
///     round-tripping, <see cref="SysmlEdgeKind"/> string values, diagnostic field presence, and
///     a full CLI end-to-end integration test exporting a real OMG fixture file.
/// </summary>
[Collection("Sequential")]
public class ExportRenderingTests
{
    /// <summary>
    ///     Serializing an <see cref="ExportResult"/> containing a <see cref="SysmlDefinitionNode"/>
    ///     produces a <c>"$type": "definition"</c> discriminator, and the JSON round-trips back to
    ///     an equivalent declaration when deserialized.
    /// </summary>
    [Fact]
    public void ExportResultSerializerContext_DefinitionNode_RoundTripsTypeDiscriminator()
    {
        var node = new SysmlDefinitionNode
        {
            Name = "Wheel",
            QualifiedName = "Model::Wheel",
            DefinitionKeyword = "part def"
        };
        var result = new ExportResult
        {
            Declarations = new Dictionary<string, SysmlNode> { ["Model::Wheel"] = node },
            Edges = [],
            Diagnostics = []
        };

        var json = JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult);

        Assert.Contains("\"$type\": \"definition\"", json);
        Assert.Contains("\"DefinitionKeyword\": \"part def\"", json);

        var deserialized = JsonSerializer.Deserialize(json, ExportResultSerializerContext.Default.ExportResult);
        Assert.NotNull(deserialized);
        var roundTripped = Assert.IsType<SysmlDefinitionNode>(deserialized!.Declarations["Model::Wheel"]);
        Assert.Equal("part def", roundTripped.DefinitionKeyword);
    }

    /// <summary>
    ///     A <see cref="SysmlFeatureNode"/> serializes with a <c>"$type": "feature"</c>
    ///     discriminator.
    /// </summary>
    [Fact]
    public void ExportResultSerializerContext_FeatureNode_UsesFeatureTypeDiscriminator()
    {
        var node = new SysmlFeatureNode
        {
            Name = "w",
            QualifiedName = "Model::Car::w",
            FeatureKeyword = "part",
            FeatureTyping = "Model::Wheel"
        };
        var result = new ExportResult
        {
            Declarations = new Dictionary<string, SysmlNode> { ["Model::Car::w"] = node },
            Edges = [],
            Diagnostics = []
        };

        var json = JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult);

        Assert.Contains("\"$type\": \"feature\"", json);
    }

    /// <summary>
    ///     An edge's <see cref="SysmlEdgeKind"/> value serializes as its underlying numeric
    ///     representation and round-trips to the exact same enum value.
    /// </summary>
    [Theory]
    [InlineData(SysmlEdgeKind.Supertype)]
    [InlineData(SysmlEdgeKind.Typing)]
    [InlineData(SysmlEdgeKind.Satisfy)]
    [InlineData(SysmlEdgeKind.Connect)]
    [InlineData(SysmlEdgeKind.Transition)]
    public void ExportResultSerializerContext_EdgeKind_RoundTripsExactly(SysmlEdgeKind kind)
    {
        var edge = new SysmlEdge("Model::A", "Model::B", kind);
        var result = new ExportResult
        {
            Declarations = new Dictionary<string, SysmlNode>(),
            Edges = [edge],
            Diagnostics = []
        };

        var json = JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult);
        var deserialized = JsonSerializer.Deserialize(json, ExportResultSerializerContext.Default.ExportResult);

        Assert.NotNull(deserialized);
        var roundTripped = Assert.Single(deserialized!.Edges);
        Assert.Equal(kind, roundTripped.Kind);
        Assert.Equal("Model::A", roundTripped.SourceQualifiedName);
        Assert.Equal("Model::B", roundTripped.TargetQualifiedName);
    }

    /// <summary>
    ///     A diagnostic's fields (FilePath, Line, Column, Severity, Message) are all present in
    ///     the serialized JSON document.
    /// </summary>
    [Fact]
    public void ExportResultSerializerContext_Diagnostic_AllFieldsPresent()
    {
        var diagnostic = new SysmlDiagnostic("model.sysml", 3, 7, DiagnosticSeverity.Warning, "unresolved reference");
        var result = new ExportResult
        {
            Declarations = new Dictionary<string, SysmlNode>(),
            Edges = [],
            Diagnostics = [diagnostic]
        };

        var json = JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult);

        Assert.Contains("\"FilePath\": \"model.sysml\"", json);
        Assert.Contains("\"Line\": 3", json);
        Assert.Contains("\"Column\": 7", json);
        Assert.Contains("\"Message\": \"unresolved reference\"", json);
    }

    /// <summary>
    ///     The JSONL declaration line wraps a "kind": "declaration" discriminator, the qualified
    ///     name, and the node itself, and serializes on a single compact (non-indented) line.
    /// </summary>
    [Fact]
    public void ExportLineSerializerContext_DeclarationLine_HasKindDiscriminatorAndIsCompact()
    {
        var node = new SysmlDefinitionNode { Name = "Wheel", QualifiedName = "Model::Wheel", DefinitionKeyword = "part def" };
        var line = ExportDeclarationLine.Create("Model::Wheel", node);

        var json = JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportDeclarationLine);

        Assert.DoesNotContain('\n', json);
        Assert.Contains("\"Kind\":\"declaration\"", json);
        Assert.Contains("\"QualifiedName\":\"Model::Wheel\"", json);
        Assert.Contains("\"$type\":\"definition\"", json);
    }

    /// <summary>
    ///     The JSONL edge line wraps a "kind": "edge" discriminator and the flattened edge fields,
    ///     on a single compact line.
    /// </summary>
    [Fact]
    public void ExportLineSerializerContext_EdgeLine_HasKindDiscriminatorAndIsCompact()
    {
        var line = ExportEdgeLine.Create(new SysmlEdge("Model::A", "Model::B", SysmlEdgeKind.Typing));

        var json = JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportEdgeLine);

        Assert.DoesNotContain('\n', json);
        Assert.Contains("\"Kind\":\"edge\"", json);
        Assert.Contains("\"SourceQualifiedName\":\"Model::A\"", json);
        Assert.Contains("\"TargetQualifiedName\":\"Model::B\"", json);
    }

    /// <summary>
    ///     The JSONL diagnostic line wraps a "kind": "diagnostic" discriminator and the flattened
    ///     diagnostic fields, on a single compact line.
    /// </summary>
    [Fact]
    public void ExportLineSerializerContext_DiagnosticLine_HasKindDiscriminatorAndIsCompact()
    {
        var line = ExportDiagnosticLine.Create(
            new SysmlDiagnostic("model.sysml", 1, 0, DiagnosticSeverity.Error, "boom"));

        var json = JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportDiagnosticLine);

        Assert.DoesNotContain('\n', json);
        Assert.Contains("\"Kind\":\"diagnostic\"", json);
        Assert.Contains("\"Message\":\"boom\"", json);
    }

    /// <summary>
    ///     Full CLI end-to-end integration test: runs the built tool via <c>dotnet</c> against a
    ///     real OMG test fixture (<c>test/SysMLModels/OMG/examples/VehicleExample/VehicleDefinitions.sysml</c>),
    ///     for both <c>--format json</c> and <c>--format jsonl</c>, and validates the produced
    ///     output deserializes and contains the expected declarations/edges/diagnostics shape,
    ///     with stdlib correctly excluded (default) and included (<c>--include-stdlib</c>).
    /// </summary>
    [Fact]
    public void ExportIntegration_RealFixture_ProducesValidJsonAndJsonl()
    {
        var dllPath = PathHelpers.SafePathCombine(AppContext.BaseDirectory, "DemaConsulting.SysML2Tools.dll");
        Assert.True(File.Exists(dllPath), $"Could not find SysML2 Tools DLL at {dllPath}");

        var fixtureRoot = FindSysMlModelsRoot();
        Assert.NotNull(fixtureRoot);
        var fixtureFile = Path.Combine(fixtureRoot!, "OMG", "examples", "VehicleExample", "VehicleDefinitions.sysml");
        Assert.True(File.Exists(fixtureFile), $"Could not find fixture file at {fixtureFile}");

        // --- JSON, default (stdlib excluded) ---
        var exitCodeJson = Runner.Run(out var jsonOutput, "dotnet", dllPath, "export", "--format", "json", fixtureFile);
        Assert.Equal(0, exitCodeJson);

        // Strip the banner/status lines preceding the JSON document itself. Searching for the
        // first '{' is sufficient here since none of the preceding banner/status lines contain
        // that character, and is robust to platform line-ending differences (\n vs \r\n).
        var jsonStart = jsonOutput.IndexOf('{');
        Assert.True(jsonStart >= 0, $"Could not find start of JSON document in output:\n{jsonOutput}");
        var jsonDocumentText = jsonOutput[jsonStart..];

        using (var document = JsonDocument.Parse(jsonDocumentText))
        {
            var root = document.RootElement;
            Assert.True(root.TryGetProperty("Declarations", out var declarations));
            Assert.True(declarations.EnumerateObject().Any());
            Assert.True(root.TryGetProperty("Edges", out var edges));
            Assert.True(edges.GetArrayLength() > 0);
            Assert.True(root.TryGetProperty("Diagnostics", out _));

            // Stdlib excluded by default: no OMG ScalarValues stdlib declarations present
            Assert.DoesNotContain(
                declarations.EnumerateObject(),
                property => property.Name.StartsWith("ScalarValues", StringComparison.Ordinal));
        }

        // --- JSON, --include-stdlib ---
        var exitCodeStdlib = Runner.Run(
            out var stdlibOutput, "dotnet", dllPath, "export", "--format", "json", "--include-stdlib", fixtureFile);
        Assert.Equal(0, exitCodeStdlib);
        Assert.Contains("ScalarValues", stdlibOutput);
        Assert.True(stdlibOutput.Length > jsonOutput.Length);

        // --- JSONL ---
        var exitCodeJsonl = Runner.Run(out var jsonlOutput, "dotnet", dllPath, "export", "--format", "jsonl", fixtureFile);
        Assert.Equal(0, exitCodeJsonl);

        var jsonlLines = jsonlOutput
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("{\"Kind\"", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(jsonlLines);

        var declarationLines = jsonlLines.Count(line => line.Contains("\"Kind\":\"declaration\"", StringComparison.Ordinal));
        var edgeLines = jsonlLines.Count(line => line.Contains("\"Kind\":\"edge\"", StringComparison.Ordinal));
        Assert.True(declarationLines > 0);
        Assert.True(edgeLines > 0);

        // Every JSONL line must be independently parseable as JSON (one record per line)
        foreach (var line in jsonlLines)
        {
            using var lineDocument = JsonDocument.Parse(line);
            Assert.True(lineDocument.RootElement.TryGetProperty("Kind", out _));
        }
    }

    /// <summary>
    ///     Full CLI end-to-end integration test: runs the built tool via <c>dotnet</c> against the
    ///     real <c>VehicleDefinitions.sysml</c> fixture with both <c>--target</c> and
    ///     <c>--filter</c> supplied together, validating that the composed narrowing produces a
    ///     smaller, valid JSON document scoped to the target's containment subtree, with every
    ///     declaration key and edge endpoint confined to that subtree.
    /// </summary>
    [Fact]
    public void ExportIntegration_RealFixture_TargetAndFilter_ProducesScopedJson()
    {
        var dllPath = PathHelpers.SafePathCombine(AppContext.BaseDirectory, "DemaConsulting.SysML2Tools.dll");
        Assert.True(File.Exists(dllPath), $"Could not find SysML2 Tools DLL at {dllPath}");

        var fixtureRoot = FindSysMlModelsRoot();
        Assert.NotNull(fixtureRoot);
        var fixtureFile = Path.Combine(fixtureRoot!, "OMG", "examples", "VehicleExample", "VehicleDefinitions.sysml");
        Assert.True(File.Exists(fixtureFile), $"Could not find fixture file at {fixtureFile}");

        // --- Unscoped baseline, for a size comparison below ---
        var exitCodeBaseline = Runner.Run(out var baselineOutput, "dotnet", dllPath, "export", "--format", "json", fixtureFile);
        Assert.Equal(0, exitCodeBaseline);

        // --- --target + --filter together: --target narrows to the 'Vehicle' subtree; the
        // "not @NoSuchMetadataType" --filter expression is a supported Phase 1 construct
        // (negated classification test) that matches every candidate (since none carry that
        // nonexistent metadata type), so it exercises the composed pipeline without further
        // narrowing, keeping the expected result predictable. ---
        var exitCodeScoped = Runner.Run(
            out var scopedOutput,
            "dotnet", dllPath, "export", "--format", "json",
            "--target", "VehicleDefinitions::Vehicle",
            "--filter", "not @NoSuchMetadataType",
            fixtureFile);
        Assert.Equal(0, exitCodeScoped);

        var jsonStart = scopedOutput.IndexOf('{');
        Assert.True(jsonStart >= 0, $"Could not find start of JSON document in output:\n{scopedOutput}");
        var scopedDocumentText = scopedOutput[jsonStart..];

        using var document = JsonDocument.Parse(scopedDocumentText);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("Declarations", out var declarations));
        Assert.True(root.TryGetProperty("Edges", out var edges));
        Assert.True(root.TryGetProperty("Diagnostics", out var diagnostics));

        // No --filter parse failure occurred, so no synthetic warning diagnostic was appended.
        Assert.DoesNotContain(diagnostics.EnumerateArray(), d => d.GetProperty("FilePath").GetString() == "<--filter>");

        // Every declaration key is 'VehicleDefinitions::Vehicle' itself or lies within its
        // containment subtree — never an unrelated top-level definition like 'Transmission'.
        var declarationNames = declarations.EnumerateObject().Select(p => p.Name).ToList();
        Assert.NotEmpty(declarationNames);
        Assert.All(declarationNames, name =>
            Assert.True(
                name == "VehicleDefinitions::Vehicle" || name.StartsWith("VehicleDefinitions::Vehicle::", StringComparison.Ordinal),
                $"Declaration '{name}' is outside the --target subtree."));

        // Every edge's endpoints (when present) also lie within the same subtree.
        foreach (var edge in edges.EnumerateArray())
        {
            var target = edge.GetProperty("TargetQualifiedName").GetString();
            Assert.NotNull(target);
            Assert.True(
                target == "VehicleDefinitions::Vehicle" || target!.StartsWith("VehicleDefinitions::Vehicle::", StringComparison.Ordinal),
                $"Edge target '{target}' is outside the --target subtree.");

            var source = edge.GetProperty("SourceQualifiedName").GetString();
            if (source is not null)
            {
                Assert.True(
                    source == "VehicleDefinitions::Vehicle" || source.StartsWith("VehicleDefinitions::Vehicle::", StringComparison.Ordinal),
                    $"Edge source '{source}' is outside the --target subtree.");
            }
        }

        // The scoped, composed export is strictly smaller than the unscoped baseline, confirming
        // the narrowing had an observable effect rather than silently exporting everything.
        Assert.True(scopedOutput.Length < baselineOutput.Length);
    }

    /// <summary>
    ///     <c>--output</c> pointing at a file inside a directory that doesn't yet exist succeeds,
    ///     creating the missing parent directory (mirroring <c>render</c>'s
    ///     <c>Directory.CreateDirectory</c> guard for its own <c>--output</c> directory).
    /// </summary>
    [Fact]
    public void ExportIntegration_OutputInMissingDirectory_CreatesDirectoryAndSucceeds()
    {
        var dllPath = PathHelpers.SafePathCombine(AppContext.BaseDirectory, "DemaConsulting.SysML2Tools.dll");
        Assert.True(File.Exists(dllPath), $"Could not find SysML2 Tools DLL at {dllPath}");

        var fixtureRoot = FindSysMlModelsRoot();
        Assert.NotNull(fixtureRoot);
        var fixtureFile = Path.Combine(fixtureRoot!, "OMG", "examples", "VehicleExample", "VehicleDefinitions.sysml");
        Assert.True(File.Exists(fixtureFile), $"Could not find fixture file at {fixtureFile}");

        var tempRoot = Path.Combine(Path.GetTempPath(), "sysml2tools-export-test-" + Guid.NewGuid().ToString("N"));
        var outputFile = Path.Combine(tempRoot, "nested", "out.json");
        try
        {
            Assert.False(Directory.Exists(Path.GetDirectoryName(outputFile)));

            var exitCode = Runner.Run(
                out _, "dotnet", dllPath, "export", "--format", "json", "--output", outputFile, fixtureFile);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputFile));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    ///     <c>--output</c> pointing at an existing directory (rather than a file) fails cleanly
    ///     with a non-zero exit code and a user-facing error message, instead of an unhandled
    ///     exception/stack trace.
    /// </summary>
    [Fact]
    public void ExportIntegration_OutputIsExistingDirectory_FailsCleanly()
    {
        var dllPath = PathHelpers.SafePathCombine(AppContext.BaseDirectory, "DemaConsulting.SysML2Tools.dll");
        Assert.True(File.Exists(dllPath), $"Could not find SysML2 Tools DLL at {dllPath}");

        var fixtureRoot = FindSysMlModelsRoot();
        Assert.NotNull(fixtureRoot);
        var fixtureFile = Path.Combine(fixtureRoot!, "OMG", "examples", "VehicleExample", "VehicleDefinitions.sysml");
        Assert.True(File.Exists(fixtureFile), $"Could not find fixture file at {fixtureFile}");

        var tempDir = Path.Combine(Path.GetTempPath(), "sysml2tools-export-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var exitCode = Runner.Run(
                out var output, "dotnet", dllPath, "export", "--format", "json", "--output", tempDir, fixtureFile);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("export: failed to write output file", output);
            Assert.DoesNotContain("Unhandled exception", output);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Finds the <c>test/SysMLModels</c> directory relative to the test assembly, mirroring
    ///     <c>Query.QueryOmgFixtureTests.FindSysMlModelsRoot</c>.
    /// </summary>
    private static string? FindSysMlModelsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "test", "SysMLModels");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}
