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

    /// <summary>
    ///     The dedicated <c>17.Control</c> corpus fixtures exercise the two Action Flow View
    ///     sub-problems together: the combined-succession <c>actionBodyItem</c> shapes (the compact
    ///     <c>action a1; then a2;</c> idiom, plus <c>first start;</c>/<c>first start then X;</c>)
    ///     and the six control-node kinds (<c>merge</c>/<c>decide</c>/<c>join</c>/<c>fork</c>/
    ///     <c>accept</c>/<c>send</c>), including the dominant real-world anonymous-node idiom
    ///     (<c>then fork;</c>/<c>then decide;</c> with no declared name, immediately followed by
    ///     several <c>then &lt;name&gt;;</c>/guarded target successions). Assertions are on raw
    ///     <see cref="SysmlTransitionNode.Source"/>/<see cref="SysmlTransitionNode.Target"/> text
    ///     and feature kind/name — not full reference resolution — per the design decision that an
    ///     anonymous control node's synthesized <c>$</c>-prefixed name is expected to produce an
    ///     "Unresolved reference" warning (cosmetic only: <c>ActionFlowViewLayoutStrategy</c> reads
    ///     the raw text directly, never <c>ResolvedEdges</c>). Each fixture's leading `then`
    ///     (<c>sourceSuccessionMember</c>) is also asserted to synthesize its implicit incoming
    ///     succession from the immediately preceding sibling (e.g. <c>TurnOn-&gt;$fork0</c>,
    ///     <c>J-&gt;F</c>, and the <c>ChargeBattery</c> declare-then-declare chain), since the
    ///     grammar's leading marker itself carries no name of its own.
    /// </summary>
    [Fact]
    public async Task ControlNode_OmgCorpusFixture_ResolvesForkJoinDecisionMerge()
    {
        var omgRoot = FindOmgModelsRoot();
        var forkJoinFile = Path.Combine(omgRoot, "training", "17.Control", "ForkJoinExample.sysml");
        var decisionFile = Path.Combine(omgRoot, "training", "17.Control", "DecisionExample.sysml");
        var controlNodeFile = Path.Combine(omgRoot, "examples", "SimpleTests", "ControlNodeTest.sysml");
        Assert.All(
            [forkJoinFile, decisionFile, controlNodeFile],
            f => Assert.True(File.Exists(f), $"Expected fixture not found: {f}"));

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([forkJoinFile, decisionFile, controlNodeFile], stdlibTable);

        // ForkJoinExample.sysml: an anonymous fork feeding 3 branches that all join back together.
        var brake = (SysmlDefinitionNode)result.Workspace!.Declarations["'Fork Join Example'::Brake"];
        var brakeFork = Assert.Single(
            brake.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "fork");
        Assert.NotNull(brakeFork.Name);
        Assert.StartsWith("$", brakeFork.Name, StringComparison.Ordinal);

        var forkSuccessions = brake.Children
            .OfType<SysmlTransitionNode>()
            .Where(t => t.Source == brakeFork.Name)
            .ToList();
        Assert.Equal(3, forkSuccessions.Count);
        Assert.Contains(forkSuccessions, t => t.Target == "monitorBrakePedal");
        Assert.Contains(forkSuccessions, t => t.Target == "monitorTraction");
        Assert.Contains(forkSuccessions, t => t.Target == "braking");

        // The fork's own leading `then` (sourceSuccessionMember) synthesizes the implicit
        // incoming succession from the immediately preceding sibling action `TurnOn`.
        Assert.Contains(
            brake.Children.OfType<SysmlTransitionNode>(),
            t => t.Source == "TurnOn" && t.Target == brakeFork.Name);

        var joinNode = Assert.Single(
            brake.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "join");
        Assert.Equal("joinNode", joinNode.Name);
        Assert.Equal(3, brake.Children.OfType<SysmlTransitionNode>().Count(t => t.Target == "joinNode"));

        // DecisionExample.sysml: an anonymous decide with two guarded successions, plus a named
        // merge that the decide's default-path action succession eventually rejoins.
        var chargeBattery = (SysmlDefinitionNode)result.Workspace!.Declarations["'Decision Example'::ChargeBattery"];
        var decide = Assert.Single(
            chargeBattery.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "decide");
        Assert.NotNull(decide.Name);
        Assert.StartsWith("$", decide.Name, StringComparison.Ordinal);

        var decideSuccessions = chargeBattery.Children
            .OfType<SysmlTransitionNode>()
            .Where(t => t.Source == decide.Name)
            .ToList();
        Assert.Equal(2, decideSuccessions.Count);
        Assert.Contains(decideSuccessions, t => t.Target == "addCharge" && t.Guard == "monitor.batteryCharge<100");
        Assert.Contains(decideSuccessions, t => t.Target == "endCharging" && t.Guard == "monitor.batteryCharge>=100");

        Assert.Contains(
            chargeBattery.Children.OfType<SysmlFeatureNode>(),
            f => f.FeatureKeyword == "merge" && f.Name == "continueCharging");

        // Each of ChargeBattery's leading-`then` chain items (`first start; then merge
        // continueCharging; then action monitor: MonitorBattery{...}; then decide;`) synthesizes
        // its own implicit incoming succession from the immediately preceding sibling.
        var chargeBatteryTransitions = chargeBattery.Children.OfType<SysmlTransitionNode>().ToList();
        Assert.Contains(chargeBatteryTransitions, t => t.Source == "start" && t.Target == "continueCharging");
        Assert.Contains(chargeBatteryTransitions, t => t.Source == "continueCharging" && t.Target == "monitor");
        Assert.Contains(chargeBatteryTransitions, t => t.Source == "monitor" && t.Target == decide.Name);

        // ControlNodeTest.sysml: fully named fork/join/merge — the richest, most reliable fixture.
        var controlNodeTest = (SysmlDefinitionNode)result.Workspace!.Declarations["ControlNodeTest"];
        var controlFeatures = controlNodeTest.Children.OfType<SysmlFeatureNode>().ToList();
        Assert.Contains(controlFeatures, f => f.FeatureKeyword == "join" && f.Name == "J");
        Assert.Contains(controlFeatures, f => f.FeatureKeyword == "fork" && f.Name == "F");
        Assert.Contains(controlFeatures, f => f.FeatureKeyword == "merge" && f.Name == "M");

        var controlTransitions = controlNodeTest.Children.OfType<SysmlTransitionNode>().ToList();
        Assert.Contains(controlTransitions, t => t.Source == "A1" && t.Target == "J");
        Assert.Contains(controlTransitions, t => t.Source == "A2" && t.Target == "J");
        Assert.Contains(controlTransitions, t => t.Source == "F" && t.Target == "B1");
        Assert.Contains(controlTransitions, t => t.Source == "F" && t.Target == "B2");
        Assert.Contains(controlTransitions, t => t.Source == "B1" && t.Target == "M");
        Assert.Contains(controlTransitions, t => t.Source == "B2" && t.Target == "M");

        // The join's own leading `then` synthesizes the implicit incoming succession from the
        // immediately preceding sibling `J`.
        Assert.Contains(controlTransitions, t => t.Source == "J" && t.Target == "F");
    }

    /// <summary>
    ///     The dedicated <c>06.EnumerationDefinitions</c> corpus fixtures exercise all three
    ///     <c>enum def</c> literal forms found in real OMG models: bare literals
    ///     (<c>enum green;</c>), a redefinition-body literal (<c>unclassified { :&gt;&gt; code =
    ///     "..."; ... }</c>), and the value-assignment form (<c>A = 4.0;</c>) — confirming
    ///     <c>CollectEnumerationBodyChildren</c>'s narrow raw-children walk (needed because
    ///     <c>enumerationBody</c> uniquely alternates <c>annotatingMember</c>/
    ///     <c>enumerationUsageMember</c> directly, unlike the single-wrapping-rule shape of
    ///     <c>definitionBody</c>/<c>requirementBody</c>) and <c>VisitEnumeratedValue</c>'s
    ///     <c>"enum value"</c> keyword.
    /// </summary>
    [Fact]
    public async Task Enumeration_OmgCorpusFixtures_CaptureAllLiteralForms()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = new[]
        {
            Path.Combine(omgRoot, "training", "06.EnumerationDefinitions", "EnumerationDefinitions-1.sysml"),
            Path.Combine(omgRoot, "training", "06.EnumerationDefinitions", "EnumerationDefinitions-2.sysml"),
        };
        Assert.All(files, f => Assert.True(File.Exists(f), $"Expected fixture not found: {f}"));

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(files, stdlibTable);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Bare-literal form.
        var trafficLightColor =
            (SysmlDefinitionNode)result.Workspace!.Declarations["'Enumeration Definitions-1'::TrafficLightColor"];
        var trafficLightValues = trafficLightColor.Children
            .OfType<SysmlFeatureNode>()
            .Where(f => f.FeatureKeyword == "enum value")
            .Select(f => f.Name)
            .ToList();
        Assert.Equal(["green", "yellow", "red"], trafficLightValues);

        // Redefinition-body form (each literal redefines "code"/"color" attributes via `:>>`).
        var classificationKind =
            (SysmlDefinitionNode)result.Workspace!.Declarations["'Enumeration Definitions-2'::ClassificationKind"];
        var classificationValues = classificationKind.Children
            .OfType<SysmlFeatureNode>()
            .Where(f => f.FeatureKeyword == "enum value")
            .Select(f => f.Name)
            .ToList();
        Assert.Equal(["unclassified", "confidential", "secret"], classificationValues);

        // Value-assignment form (`A = 4.0;`) — the assigned value expression is not parsed, only
        // the literal's own name, an accepted minimal-capture gap.
        var gradePoints =
            (SysmlDefinitionNode)result.Workspace!.Declarations["'Enumeration Definitions-2'::GradePoints"];
        var gradeValues = gradePoints.Children
            .OfType<SysmlFeatureNode>()
            .Where(f => f.FeatureKeyword == "enum value")
            .Select(f => f.Name)
            .ToList();
        Assert.Equal(["A", "B", "C", "D", "F"], gradeValues);
    }

    /// <summary>
    ///     The dedicated <c>32.Requirements</c> corpus fixtures exercise the requirement
    ///     compartment-depth idioms found in real OMG models: a <c>requirement def</c> with
    ///     <c>doc</c>/<c>subject</c>/<c>require constraint</c>/<c>assume constraint</c> members, and
    ///     — the dominant real-corpus idiom — a <c>requirement</c> *usage* that specializes a
    ///     requirement def and supplies its own <c>subject</c>/<c>assume constraint</c> body
    ///     (confirming the deliberate extension of <c>VisitRequirementUsage</c> to collect
    ///     <c>Children</c>, beyond the plan's literal <c>*Definition</c>-only wording). The nested
    ///     <c>doc</c> inside <c>RequirementUsages.sysml</c>'s <c>assume constraint { doc /* ... */
    ///     ... }</c> is confirmed NOT captured (an accepted scope boundary — constraint bodies are
    ///     captured only as raw <c>ExpressionText</c>, with no nested-member traversal).
    /// </summary>
    [Fact]
    public async Task Requirement_OmgCorpusFixtures_CaptureSubjectAndConstraints()
    {
        var omgRoot = FindOmgModelsRoot();
        var definitionsFile = Path.Combine(omgRoot, "training", "32.Requirements", "RequirementDefinitions.sysml");
        var usagesFile = Path.Combine(omgRoot, "training", "32.Requirements", "RequirementUsages.sysml");
        Assert.All([definitionsFile, usagesFile], f => Assert.True(File.Exists(f), $"Expected fixture not found: {f}"));

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([definitionsFile, usagesFile], stdlibTable);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // RequirementDefinitions.sysml: MassLimitationRequirement has doc + require constraint.
        var massLimitation =
            (SysmlDefinitionNode)result.Workspace!.Declarations[
                "'Requirement Definitions'::MassLimitationRequirement"];
        Assert.Contains(massLimitation.Annotations, a => a.Kind == SysmlAnnotationKind.Documentation);
        var requireConstraint = Assert.Single(
            massLimitation.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "require constraint");
        Assert.Contains("massActual", requireConstraint.ExpressionText);
        Assert.Contains("massReqd", requireConstraint.ExpressionText);

        // VehicleMassLimitationRequirement (a requirement def specializing another) has subject +
        // assume constraint.
        var vehicleMassLimitation =
            (SysmlDefinitionNode)result.Workspace!.Declarations[
                "'Requirement Definitions'::VehicleMassLimitationRequirement"];
        var subject = Assert.Single(
            vehicleMassLimitation.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "subject");
        Assert.Equal("vehicle", subject.Name);
        Assert.Equal("Vehicle", subject.FeatureTyping);
        var assumeConstraint = Assert.Single(
            vehicleMassLimitation.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "assume constraint");
        Assert.Contains("fuelMass", assumeConstraint.ExpressionText);

        // RequirementUsages.sysml: fullVehicleMassLimit is a requirement *usage* specializing
        // VehicleMassLimitationRequirement, with its own subject + assume constraint body.
        var fullVehicleMassLimit =
            (SysmlFeatureNode)result.Workspace!.Declarations["'Requirement Usages'::fullVehicleMassLimit"];
        var usageSubject = Assert.Single(
            fullVehicleMassLimit.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "subject");
        Assert.Equal("vehicle", usageSubject.Name);
        var usageAssumeConstraint = Assert.Single(
            fullVehicleMassLimit.Children.OfType<SysmlFeatureNode>(), f => f.FeatureKeyword == "assume constraint");
        Assert.Contains("fuelFullMass", usageAssumeConstraint.ExpressionText);

        // The nested `doc` inside the assume constraint's own calculation body is NOT captured as
        // a separate Annotation anywhere on fullVehicleMassLimit — an accepted scope boundary.
        Assert.DoesNotContain(
            fullVehicleMassLimit.Annotations, a => a.Kind == SysmlAnnotationKind.Documentation);
    }

    /// <summary>
    ///     The dedicated <c>01.Packages/CommentExample.sysml</c> and
    ///     <c>DocumentationExample.sysml</c> corpus fixtures confirm <c>comment</c>/<c>doc</c>
    ///     annotations attach to their owning package/definition via the pre-existing
    ///     <c>AnnotationCapture</c> mechanism — the source of note-box text rendered by
    ///     <c>GeneralViewLayoutStrategy.AddAnnotationNote</c>.
    /// </summary>
    [Fact]
    public async Task CommentAndDocumentation_OmgCorpusFixtures_CaptureAnnotations()
    {
        var omgRoot = FindOmgModelsRoot();
        var commentsFile = Path.Combine(omgRoot, "examples", "CommentExamples", "Comments.sysml");
        var docFile = Path.Combine(omgRoot, "training", "01.Packages", "DocumentationExample.sysml");
        Assert.All([commentsFile, docFile], f => Assert.True(File.Exists(f), $"Expected fixture not found: {f}"));

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([commentsFile, docFile], stdlibTable);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // Comments.sysml's `part def C { doc /* ... */ comment /* Comment in Part Def */ ... }`
        // nests both an annotatingMember doc and comment directly inside C's own body, so both
        // attach to C itself (as opposed to the file's `comment about C ...`/`comment about
        // Comments ...` forms, which target an element by name via `about` — a reference the
        // existing AnnotationCapture mechanism does not resolve; such comments attach to their
        // syntactically-owning namespace instead, an existing, unchanged limitation).
        var partDefC = (SysmlDefinitionNode)result.Workspace!.Declarations["Comments::C"];
        Assert.Contains(partDefC.Annotations, a => a.Kind == SysmlAnnotationKind.Documentation);
        Assert.Contains(partDefC.Annotations, a => a.Kind == SysmlAnnotationKind.Comment);

        var automobileWithDoc =
            (SysmlDefinitionNode)result.Workspace!.Declarations["'Documentation Example'::Automobile"];
        Assert.Contains(automobileWithDoc.Annotations, a => a.Kind == SysmlAnnotationKind.Documentation);
    }
}
