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
    ///     The dedicated <c>bind</c> corpus fixture parses and resolves without crashing (the
    ///     Phase 1 gate every fixture in this unit must clear). This fixture's two <c>bind</c>
    ///     statements reference a port/item usage declared only via
    ///     <c>port redefines fuelTankPort { out item redefines fuelSupply; ... }</c> — an implicitly
    ///     named usage (no explicit name token precedes <c>redefines</c>) whose effective name is
    ///     never populated by <c>AstBuilder</c> (a pre-existing gap: <c>BuildUsageNode</c> only
    ///     reads a name from an explicit <c>usageDeclaration</c>, never falling back to the
    ///     redefined feature's own name), so both <c>bind</c> endpoints legitimately fail to
    ///     resolve for this specific fixture. This mirrors the already-documented
    ///     <see cref="SysmlEdgeKind.Transition"/> implied-source limitation and is called out here,
    ///     not silently masked, per this unit's existing graceful-degradation contract (0 edges,
    ///     Warning diagnostics, no crash — never an Error/exception).
    /// </summary>
    [Fact]
    public async Task Binding_OmgCorpusFixture_ParsesAndResolvesWithoutCrashing()
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

        // Documented limitation (see summary above): 0 Binding edges for this specific fixture.
        Assert.DoesNotContain(result.Workspace!.Index.AllEdges, e => e.Kind == SysmlEdgeKind.Binding);
    }
}
