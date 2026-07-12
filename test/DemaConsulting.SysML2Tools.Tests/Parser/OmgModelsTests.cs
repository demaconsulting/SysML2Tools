// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Parser;

/// <summary>
///     Tests that validate the parser against the OMG reference model files.
/// </summary>
public sealed class OmgModelsTests
{
    /// <summary>
    ///     Finds the test/SysMLModels/OMG directory relative to the test assembly.
    /// </summary>
    private static string FindOmgModelsRoot()
    {
        // Walk up from the test assembly location to find the repo root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "test", "SysMLModels", "OMG")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Cannot locate test/SysMLModels/OMG from test assembly location.");
        }

        return Path.Combine(dir.FullName, "test", "SysMLModels", "OMG");
    }

    /// <summary>
    ///     Every OMG reference model file (examples, training, validation) must
    ///     parse without syntax errors. This is the Phase 1 gate from the architecture.
    /// </summary>
    [Fact]
    public async Task Parse_OmgModels_NoSyntaxErrors()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = Directory.GetFiles(omgRoot, "*.sysml", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var loadResult = await WorkspaceLoader.LoadAsync(files, stdlibTable);
        var result = loadResult;

        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            $"{errors.Count} syntax error(s) in OMG models:{Environment.NewLine}" +
            string.Join(Environment.NewLine, errors.Select(d =>
                $"  {d.FilePath}({d.Line},{d.Column}): {d.Message}")));
    }

    /// <summary>
    ///     Confirms all 251 expected OMG model files are present.
    /// </summary>
    [Fact]
    public async Task OmgModels_FileCount_IsExpected()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = Directory.GetFiles(omgRoot, "*.sysml", SearchOption.AllDirectories);
        Assert.True(files.Length >= 251,
            $"Expected at least 251 OMG model files, found {files.Length}");
        await Task.CompletedTask;
    }

    /// <summary>
    ///     The dedicated standalone-<c>dependency</c> corpus fixtures (both the <c>from</c>/<c>to</c>
    ///     comma-list form and the no-<c>FROM</c>-keyword implicit-from form) resolve into the
    ///     expected <see cref="SysmlEdgeKind.Dependency"/> edges, with no unresolved-reference
    ///     diagnostics — confirming <c>VisitDependency</c>'s token-position from/to split and
    ///     <c>ReferenceResolver</c>'s from×to cross-product resolution against real, quoted,
    ///     multi-word OMG model names (not just synthetic single-word fixtures).
    /// </summary>
    [Fact]
    public async Task Dependency_OmgCorpusFixtures_ResolveExpectedEdges()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = new[]
        {
            Path.Combine(omgRoot, "training", "37.Dependencies", "DependencyExample.sysml"),
            Path.Combine(omgRoot, "validation", "12-DependencyRelationships", "12a-Dependency.sysml"),
        };
        Assert.All(files, f => Assert.True(File.Exists(f), $"Expected fixture not found: {f}"));

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(files, stdlibTable);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);

        var dependencyEdges = result.Workspace!.Index.AllEdges
            .Where(e => e.Kind == SysmlEdgeKind.Dependency)
            .Select(e => (Source: e.SourceQualifiedName ?? string.Empty, Target: e.TargetQualifiedName))
            .ToList();

        // DependencyExample.sysml: two dependency statements — one with a single from/to pair,
        // one with a single "from" and a comma-separated list of two "to" names (cross product).
        Assert.Contains(
            ("'Dependency Example'::'System Assembly'::'Computer Subsystem'", "'Dependency Example'::'Software Design'"),
            dependencyEdges);
        Assert.Contains(
            ("'Dependency Example'::'System Assembly'::'Storage Subsystem'", "'Dependency Example'::'Software Design'::MessageSchema"),
            dependencyEdges);
        Assert.Contains(
            ("'Dependency Example'::'System Assembly'::'Storage Subsystem'", "'Dependency Example'::'Software Design'::DataSchema"),
            dependencyEdges);

        // 12a-Dependency.sysml: two package-to-package statements, plus the no-FROM-keyword
        // implicit-from shape ("dependency z to x, y;") producing a from×to cross product.
        Assert.Contains(("'12a-Dependency'::'Application Layer'", "'12a-Dependency'::'Service Layer'"), dependencyEdges);
        Assert.Contains(("'12a-Dependency'::'Service Layer'", "'12a-Dependency'::'Data Layer'"), dependencyEdges);
        Assert.Contains(("'12a-Dependency'::z", "'12a-Dependency'::x"), dependencyEdges);
        Assert.Contains(("'12a-Dependency'::z", "'12a-Dependency'::y"), dependencyEdges);

        Assert.Equal(7, dependencyEdges.Count);
    }

    /// <summary>
    ///     The dedicated <c>bind</c> corpus fixture parses and resolves its two <c>bind</c>
    ///     statements, whose endpoints reference a port/item usage declared only via
    ///     <c>port redefines fuelTankPort { out item redefines fuelSupply; ... }</c> — an implicitly
    ///     named usage (no explicit name token precedes <c>redefines</c>). Per SysML v2 semantics
    ///     such a usage's implicit name is the name of the feature it redefines, so
    ///     <c>fuelTankPort</c>/<c>fuelSupply</c> etc. are resolvable simple names despite never
    ///     being written explicitly — <c>AstBuilder.BuildUsageNode</c>'s <c>effectiveName</c>
    ///     fallback derives them from <see cref="SysmlFeatureNode.RedefinedFeatureName"/>.
    /// </summary>
    [Fact]
    public async Task Binding_OmgCorpusFixture_ResolvesBindingEdgesViaImplicitRedefinitionNames()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = new[]
        {
            Path.Combine(omgRoot, "training", "12.BindingConnectors", "BindingConnectorsExample-1.sysml"),
            Path.Combine(omgRoot, "training", "10.Ports", "PortExample.sysml"),
        };
        Assert.All(files, f => Assert.True(File.Exists(f), $"Expected fixture not found: {f}"));

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(files, stdlibTable);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var bindingEdges = result.Workspace!.Index.AllEdges
            .Where(e => e.Kind == SysmlEdgeKind.Binding)
            .Select(e => (Source: e.SourceQualifiedName ?? string.Empty, Target: e.TargetQualifiedName))
            .ToList();

        Assert.Contains(
            ("'Binding Connectors Example-1'::vehicle::tank::fuelTankPort::fuelSupply",
                "'Binding Connectors Example-1'::vehicle::tank::pump::pumpOut"),
            bindingEdges);
        Assert.Contains(
            ("'Binding Connectors Example-1'::vehicle::tank::fuelTankPort::fuelReturn",
                "'Binding Connectors Example-1'::vehicle::tank::tank::fuelIn"),
            bindingEdges);
        Assert.Equal(2, bindingEdges.Count);
    }
}
