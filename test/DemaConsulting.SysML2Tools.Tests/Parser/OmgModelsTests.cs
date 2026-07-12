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

    /// <summary>
    ///     The dedicated <c>25.Transitions/TransitionActions.sysml</c> corpus fixture exercises all
    ///     three ROADMAP.md sub-problems together: the attached-transition state-body idiom
    ///     (<c>state off; accept VehicleStartSignal then starting;</c>), named/unnamed entry/do/exit
    ///     action features (<c>state on { entry performSelfTest{...}; do action providePower{...};
    ///     exit action applyParkingBrake{...}; }</c>), and an inherited-pseudostate-feature initial
    ///     transition (<c>first start then off;</c>, where <c>start</c> is inherited from
    ///     <c>Actions::Action</c> since <c>state def VehicleStates;</c> declares no explicit
    ///     supertype). Before the fix this fixture produced 0 resolved states/transitions plus
    ///     "Unresolved reference" warnings for every never-registered name; the fixed counts below
    ///     were confirmed by directly exporting this fixture and inspecting the resulting AST
    ///     (see the developer report for the exact command).
    /// </summary>
    [Fact]
    public async Task Transition_OmgCorpusFixture_ResolvesAllStatesAndTransitions()
    {
        var omgRoot = FindOmgModelsRoot();
        var file = Path.Combine(omgRoot, "training", "25.Transitions", "TransitionActions.sysml");
        Assert.True(File.Exists(file), $"Expected fixture not found: {file}");

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([file], stdlibTable);

        Assert.DoesNotContain(result.Diagnostics,
            d => d.Message.Contains("Unresolved reference: 'start'", StringComparison.Ordinal) ||
                 d.Message.Contains("Unresolved reference: 'off'", StringComparison.Ordinal) ||
                 d.Message.Contains("Unresolved reference: 'starting'", StringComparison.Ordinal) ||
                 d.Message.Contains("Unresolved reference: 'on'", StringComparison.Ordinal));

        var vehicleStates = (SysmlFeatureNode)result.Workspace!.Declarations["'Transition Actions'::vehicleStates"];

        // 3 declared state-keyword children (off, starting, on); vehicleStates itself is the
        // enclosing state usage, not counted among "states of the machine".
        var declaredStates = vehicleStates.Children
            .OfType<SysmlFeatureNode>()
            .Where(f => f.FeatureKeyword == "state")
            .ToList();
        Assert.Equal(3, declaredStates.Count);
        Assert.Equal(["off", "starting", "on"], declaredStates.Select(s => s.Name));

        // 4 transitions: start->off (pseudostate/initial), off->starting, starting->on, on->off —
        // all fully resolved (each has a Transition ResolvedEdge, none unresolved).
        var transitions = vehicleStates.Children.OfType<SysmlTransitionNode>().ToList();
        Assert.Equal(4, transitions.Count);
        Assert.All(transitions, t => Assert.Contains(t.ResolvedEdges, e => e.Kind == SysmlEdgeKind.Transition));

        var transitionEdges = transitions
            .SelectMany(t => t.ResolvedEdges)
            .Where(e => e.Kind == SysmlEdgeKind.Transition)
            .Select(e => (Source: e.SourceQualifiedName ?? string.Empty, Target: e.TargetQualifiedName))
            .ToList();
        Assert.Contains(("Actions::Action::start", "'Transition Actions'::vehicleStates::off"), transitionEdges);
        Assert.Contains(
            ("'Transition Actions'::vehicleStates::off", "'Transition Actions'::vehicleStates::starting"),
            transitionEdges);
        Assert.Contains(
            ("'Transition Actions'::vehicleStates::starting", "'Transition Actions'::vehicleStates::on"),
            transitionEdges);
        Assert.Contains(
            ("'Transition Actions'::vehicleStates::on", "'Transition Actions'::vehicleStates::off"),
            transitionEdges);

        // The "on" state's entry/do/exit action features are all registered: the entry action is
        // the fixture's unnamed reference-subsetting form (Name null), do/exit are named.
        var onState = declaredStates.Single(s => s.Name == "on");
        var actionFeatures = onState.Children.OfType<SysmlFeatureNode>().ToList();
        Assert.Contains(actionFeatures, f => f.FeatureKeyword == "entry" && f.Name is null);
        Assert.Contains(actionFeatures, f => f.FeatureKeyword == "do" && f.Name == "providePower");
        Assert.Contains(actionFeatures, f => f.FeatureKeyword == "exit" && f.Name == "applyParkingBrake");
    }
}
