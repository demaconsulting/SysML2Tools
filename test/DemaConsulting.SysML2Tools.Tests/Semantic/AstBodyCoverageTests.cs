// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests for the AST nodes <c>AstBuilder</c> builds for the constructs the OMG corpus references
///     but the AST once lacked (issue #137): nested action, calculation, constraint and case bodies,
///     bare directed usages, portion usages, <c>perform</c>/<c>exhibit</c>/<c>include</c>/<c>assert</c>
///     usages, a transition's effect, multiple redefinitions and anonymous owners — and for the name
///     resolution they enable. <c>AstBuilder</c> is internal, so every test goes through the public
///     <see cref="WorkspaceLoader"/> entry point, like the other AST-builder tests.
/// </summary>
public sealed class AstBodyCoverageTests
{
    /// <summary>
    ///     Loads <paramref name="source"/> as one file (with the standard library) in strict
    ///     unresolved-reference mode, so any reference that does not resolve is an error.
    /// </summary>
    private static async Task<SysmlLoadResult> LoadAsync(string source)
    {
        var file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(file, source, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([file], stdlibTable, strictUnresolved: true);
            Assert.NotNull(result.Workspace);
            return result;
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Asserts that the load produced no diagnostic of any kind.</summary>
    private static void AssertClean(SysmlLoadResult result) =>
        Assert.True(
            result.Diagnostics.Count == 0,
            string.Join("\n", result.Diagnostics.Select(d => $"({d.Line},{d.Column}) {d.Severity}: {d.Message}")));

    /// <summary>Returns the feature declared at <paramref name="qualifiedName"/>.</summary>
    private static SysmlFeatureNode FeatureAt(SysmlLoadResult result, string qualifiedName) =>
        Assert.IsType<SysmlFeatureNode>(result.Workspace!.Declarations[qualifiedName]);

    /// <summary>Returns the definition declared at <paramref name="qualifiedName"/>.</summary>
    private static SysmlDefinitionNode DefinitionAt(SysmlLoadResult result, string qualifiedName) =>
        Assert.IsType<SysmlDefinitionNode>(result.Workspace!.Declarations[qualifiedName]);

    /// <summary>Returns whether the load recorded the given resolved edge.</summary>
    private static bool HasEdge(SysmlLoadResult result, SysmlEdgeKind kind, string source, string target) =>
        result.Workspace!.Index.AllEdges.Any(e =>
            e.Kind == kind && e.SourceQualifiedName == source && e.TargetQualifiedName == target);

    // ---------------------------------------------------------------- bare directed usages

    /// <summary>
    ///     Bare directed parameters of an action definition build <c>ref</c> features carrying the
    ///     direction, typing and multiplicity, owned by the definition and excluded from diagrams.
    /// </summary>
    [Fact]
    public async Task AstBuilder_DefaultReferenceUsage_InActionDef_BuildsRefNodesWithDirectionTypingAndMultiplicity()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                item def T;
                action def A { in x : T; out y; inout z[2] : T; }
            }
            """);

        // Assert
        AssertClean(result);
        var x = FeatureAt(result, "P::A::x");
        Assert.Equal("ref", x.FeatureKeyword);
        Assert.Equal(SysmlFeatureDirection.In, x.Direction);
        Assert.Equal("T", x.FeatureTyping);
        Assert.False(x.IsReference);
        Assert.True(x.IsExcludedFromDiagrams);
        var y = FeatureAt(result, "P::A::y");
        Assert.Equal(SysmlFeatureDirection.Out, y.Direction);
        Assert.Null(y.FeatureTyping);
        var z = FeatureAt(result, "P::A::z");
        Assert.Equal(SysmlFeatureDirection.InOut, z.Direction);
        Assert.Equal("[2]", z.Multiplicity);
        Assert.Equal(3, DefinitionAt(result, "P::A").Children.Count);
        Assert.Empty(DefinitionAt(result, "P::A").DiagramChildren);
    }

    /// <summary>
    ///     Bare parameters in the body of an action usage are built, so a binding to a feature chain
    ///     through them resolves.
    /// </summary>
    [Fact]
    public async Task AstBuilder_DefaultReferenceUsage_InActionUsageBody_BindingResolves()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                item def Scene;
                action def Focus { in scene : Scene; }
                action def Take {
                    in item scene : Scene;
                    bind focus.scene = scene;
                    action focus : Focus { in scene; }
                }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.Equal(SysmlFeatureDirection.In, FeatureAt(result, "P::Take::focus::scene").Direction);
        Assert.False(FeatureAt(result, "P::Take::focus").IsExcludedFromDiagrams);
        Assert.True(FeatureAt(result, "P::Take::focus::scene").IsExcludedFromDiagrams);
    }

    /// <summary>A bare usage with no direction (in a package) is a <c>ref</c> feature without a direction.</summary>
    [Fact]
    public async Task AstBuilder_DefaultReferenceUsage_NoDirectionInPackage_BuildsRefNode()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Q;
                attribute base : Q;
                distancePerVolume :> base = 5;
            }
            """);

        // Assert
        AssertClean(result);
        var bare = FeatureAt(result, "P::distancePerVolume");
        Assert.Equal("ref", bare.FeatureKeyword);
        Assert.Null(bare.Direction);
        Assert.Equal(["base"], bare.SupertypeNames);
        Assert.Equal("= 5", bare.ValueText);
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "P::distancePerVolume", "P::base"));
    }

    /// <summary>A bare usage that only redefines a feature takes the redefined feature's name.</summary>
    [Fact]
    public async Task AstBuilder_DefaultReferenceUsage_RedefinitionOnly_TakesRedefinedName()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def A { attribute x; }
                part def B :> A { in :>> x; }
            }
            """);

        // Assert
        AssertClean(result);
        var redefining = FeatureAt(result, "P::B::x");
        Assert.Equal("x", redefining.RedefinedFeatureName);
        Assert.Equal(SysmlFeatureDirection.In, redefining.Direction);
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "P::B::x", "P::A::x"));
    }

    /// <summary>A <c>return</c> parameter has the <c>Out</c> direction.</summary>
    [Fact]
    public async Task AstBuilder_ReturnParameter_InCalcDef_HasOutDirection()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def V;
                calc def Twice { in v : V; return r : V = v; }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.Equal(SysmlFeatureDirection.In, FeatureAt(result, "P::Twice::v").Direction);
        Assert.Equal(SysmlFeatureDirection.Out, FeatureAt(result, "P::Twice::r").Direction);
        Assert.Equal("V", FeatureAt(result, "P::Twice::r").FeatureTyping);
    }

    /// <summary>A lone <c>;</c> in a body is not a usage and builds no node.</summary>
    [Fact]
    public async Task AstBuilder_DefaultReferenceUsage_WithoutDeclaration_BuildsNoNode()
    {
        // Arrange / Act
        var result = await LoadAsync("package P { action def A { ; } }");

        // Assert
        Assert.Empty(DefinitionAt(result, "P::A").Children);
    }

    // ---------------------------------------------------------------- portion usages

    /// <summary>Timeslice and snapshot usages build features whose keyword is the portion kind.</summary>
    [Fact]
    public async Task AstBuilder_PortionUsage_TimesliceAndSnapshot_BuildNodesAndSuccessionResolves()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Date;
                part def Vehicle {
                    timeslice assembly;
                    first assembly then delivery;
                    snapshot delivery { attribute deliveryDate : Date; }
                    then timeslice ownership[0..*] ordered {
                        snapshot sale = start;
                    }
                }
            }
            """);

        // Assert
        AssertClean(result);
        var assembly = FeatureAt(result, "P::Vehicle::assembly");
        Assert.Equal("timeslice", assembly.FeatureKeyword);
        Assert.True(assembly.IsExcludedFromDiagrams);
        var delivery = FeatureAt(result, "P::Vehicle::delivery");
        Assert.Equal("snapshot", delivery.FeatureKeyword);
        Assert.True(FeatureAt(result, "P::Vehicle::delivery::deliveryDate").IsExcludedFromDiagrams);
        Assert.Equal("timeslice", FeatureAt(result, "P::Vehicle::ownership").FeatureKeyword);
        Assert.Equal("snapshot", FeatureAt(result, "P::Vehicle::ownership::sale").FeatureKeyword);
    }

    /// <summary>An individual usage is an <c>occurrence</c> feature flagged individual.</summary>
    [Fact]
    public async Task AstBuilder_PortionUsage_IndividualUsage_BuildsIndividualOccurrence()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def Car;
                individual vehicle1 : Car;
                individual part vehicle2 : Car;
            }
            """);

        // Assert
        AssertClean(result);
        var individual = FeatureAt(result, "P::vehicle1");
        Assert.Equal("occurrence", individual.FeatureKeyword);
        Assert.True(individual.IsIndividual);
        Assert.True(individual.IsExcludedFromDiagrams);
        Assert.False(FeatureAt(result, "P::vehicle2").IsExcludedFromDiagrams);
    }

    /// <summary>The members a <c>timeslice</c> inherits (here its <c>start</c>) resolve with no explicit specialization.</summary>
    [Fact]
    public async Task NameResolver_PortionUsage_ImplicitSpecialization_MemberResolves()
    {
        // Arrange
        var result = await LoadAsync("package P { part def V { timeslice t; snapshot s; } }");
        var resolver = new NameResolver(result.Workspace!.Declarations);

        // Act / Assert
        Assert.Contains("Occurrences::Occurrence::timeSlices", resolver.LookupGeneralTypes("P::V::t"));
        Assert.Contains("Occurrences::Occurrence::snapshots", resolver.LookupGeneralTypes("P::V::s"));
        Assert.NotNull(resolver.Resolve("start", "P::V::t"));
    }

    // ---------------------------------------------------------------- behavior bodies

    /// <summary>Nested action usages are built (excluded from diagrams), so a deep feature chain resolves.</summary>
    [Fact]
    public async Task AstBuilder_ActionUsage_NestedActions_BuildsChildrenAndChainResolves()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                action def Inner { in item v; }
                part host {
                    action run {
                        action one : Inner;
                        action nested { action two : Inner; }
                    }
                    bind run.nested.two.v = run.one.v;
                }
            }
            """);

        // Assert
        AssertClean(result);
        var nested = FeatureAt(result, "P::host::run::nested");
        Assert.True(nested.IsExcludedFromDiagrams);
        Assert.True(FeatureAt(result, "P::host::run::nested::two").IsExcludedFromDiagrams);
        Assert.False(FeatureAt(result, "P::host::run").IsExcludedFromDiagrams);
    }

    /// <summary>The bare parameters of a calculation definition are reachable through a typed calculation usage.</summary>
    [Fact]
    public async Task AstBuilder_CalcDef_BareParameters_ResolveThroughTypedUsage()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Real;
                calc def Law { in rho : Real; in volume : Real; return mass : Real = rho * volume; }
                part solver {
                    attribute density : Real;
                    calc solve : Law;
                    bind solve.rho = density;
                }
            }
            """);

        // Assert
        AssertClean(result);
        var rho = FeatureAt(result, "P::Law::rho");
        Assert.Equal("ref", rho.FeatureKeyword);
        Assert.Equal(SysmlFeatureDirection.In, rho.Direction);
        Assert.True(rho.IsExcludedFromDiagrams);
    }

    /// <summary>
    ///     A constraint definition keeps its synthesized expression child first (the only one a
    ///     diagram draws) and appends the members of its body; an <c>assert constraint</c> typed by
    ///     it makes the members reachable.
    /// </summary>
    [Fact]
    public async Task AstBuilder_ConstraintDef_BodyMembers_ResolveThroughAssertConstraint()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Mass;
                constraint def Sum { in total : Mass; in parts : Mass[0..*]; total == parts }
                part vehicle {
                    attribute mass : Mass;
                    assert constraint check : Sum;
                    bind check.total = mass;
                }
            }
            """);

        // Assert
        AssertClean(result);
        var definition = DefinitionAt(result, "P::Sum");
        Assert.Equal("constraint", Assert.IsType<SysmlFeatureNode>(definition.Children[0]).FeatureKeyword);
        Assert.False(definition.Children[0].IsExcludedFromDiagrams);
        Assert.Contains(definition.Children, c => c.Name == "total" && c.IsExcludedFromDiagrams);
        Assert.Equal("assert constraint", FeatureAt(result, "P::vehicle::check").FeatureKeyword);
        Assert.Equal("Sum", FeatureAt(result, "P::vehicle::check").FeatureTyping);
    }

    /// <summary>A constraint usage's body is built.</summary>
    [Fact]
    public async Task AstBuilder_ConstraintUsage_Body_BuildsMembers()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Mass;
                constraint limit { in m : Mass; m <= 5 }
                part p { attribute mass : Mass; bind limit.m = mass; }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.True(FeatureAt(result, "P::limit::m").IsExcludedFromDiagrams);
    }

    /// <summary>The members of a verification definition's body resolve through a verification usage.</summary>
    [Fact]
    public async Task AstBuilder_VerificationDef_BodyAction_ResolvesThroughUsage()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                item def Sample;
                verification def Check {
                    subject s : Sample;
                    action collect { in item data : Sample; }
                    return verdict : VerificationCases::VerdictKind;
                }
                part rig {
                    item sample : Sample;
                    verification check : Check;
                    bind check.collect.data = sample;
                }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.True(FeatureAt(result, "P::Check::collect").IsExcludedFromDiagrams);
        Assert.Equal("subject", FeatureAt(result, "P::Check::s").FeatureKeyword);
        Assert.Equal(SysmlFeatureDirection.Out, FeatureAt(result, "P::Check::verdict").Direction);
    }

    /// <summary>The members of a use case and a case usage body are built.</summary>
    [Fact]
    public async Task AstBuilder_CaseBodies_UseCaseAndCaseUsage_BuildMembers()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def Actor1;
                use case def Drive {
                    subject car : Actor1;
                    action steer { in item angle; }
                }
                case c1 { action step1; objective { doc /* nothing */ } }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.True(FeatureAt(result, "P::Drive::steer").IsExcludedFromDiagrams);
        Assert.Equal("subject", FeatureAt(result, "P::Drive::car").FeatureKeyword);
        Assert.True(FeatureAt(result, "P::c1::step1").IsExcludedFromDiagrams);
        Assert.DoesNotContain(result.Workspace!.Declarations.Keys, key => key.StartsWith("P::c1::obj", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- perform / exhibit / include / assert

    /// <summary>
    ///     A <c>perform</c> usage naming only the feature it performs is named by that feature's
    ///     simple name (so a chain through it resolves) and lives under a synthetic qualified name.
    /// </summary>
    [Fact]
    public async Task AstBuilder_PerformWithReference_NamedByReferencedFeature()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                action def Start { action turnOn; action trigger; }
                part sys {
                    action startVehicle : Start;
                    part driver { perform startVehicle.turnOn; }
                    part vehicle { perform startVehicle.trigger; }
                    bind driver.turnOn = vehicle.trigger;
                }
            }
            """);

        // Assert
        AssertClean(result);
        var perform = result.Workspace!.Declarations.Values
            .OfType<SysmlFeatureNode>()
            .Single(f => f.FeatureKeyword == "perform" && f.Name == "turnOn");
        Assert.Equal("startVehicle.turnOn", perform.ReferenceSubsettingName);
        Assert.StartsWith("P::sys::driver::$perform", perform.QualifiedName, StringComparison.Ordinal);
        Assert.True(perform.IsExcludedFromDiagrams);
    }

    /// <summary>
    ///     A <c>perform action</c> with a body, and a send action in it, resolve down to the sent
    ///     message through the library's send action.
    /// </summary>
    [Fact]
    public async Task AstBuilder_PerformAction_WithBody_BuildsNestedSend()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Sig;
                part producer {
                    port publicationPort;
                    perform action producerBehavior {
                        action publish send new Sig() via publicationPort;
                    }
                }
                part link { attribute msg; bind producer.producerBehavior.publish.sentMessage = msg; }
            }
            """);

        // Assert
        AssertClean(result);
        var behavior = FeatureAt(result, "P::producer::producerBehavior");
        Assert.Equal("perform", behavior.FeatureKeyword);
        Assert.Equal("send", FeatureAt(result, "P::producer::producerBehavior::publish").FeatureKeyword);
    }

    /// <summary>
    ///     An <c>exhibit state</c> usage with a named transition resolves the transition's accepter
    ///     and effect: a transition specializes the library's transition action, and its
    ///     <c>do send</c> clause is an <c>effect</c> feature of the send kind.
    /// </summary>
    [Fact]
    public async Task AstBuilder_ExhibitState_BodyAndTransition_ResolvesAccepterAndEffect()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Sig;
                part server {
                    port inPort;
                    exhibit state behavior {
                        state waiting;
                        transition delivering
                            first waiting
                            accept pub : Sig via inPort
                            do send new Sig() to server
                            then waiting;
                    }
                }
                part link {
                    attribute msg;
                    bind server.behavior.delivering.accepter.acceptedMessage = msg;
                    bind server.behavior.delivering.effect.sentMessage = msg;
                }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.Equal("exhibit", FeatureAt(result, "P::server::behavior").FeatureKeyword);
        var effect = FeatureAt(result, "P::server::behavior::delivering::effect");
        Assert.Equal("send", effect.FeatureKeyword);
        Assert.True(effect.IsExcludedFromDiagrams);
        var transition = Assert.IsType<SysmlTransitionNode>(result.Workspace!.Declarations["P::server::behavior::delivering"]);
        Assert.Contains(effect, transition.Children);
    }

    /// <summary>A named transition with no <c>do</c> clause owns no effect feature.</summary>
    [Fact]
    public async Task AstBuilder_Transition_WithoutEffect_HasNoEffectChild()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                state def S {
                    state a; state b;
                    transition t first a then b;
                    transition u first a do action act then b;
                }
            }
            """);

        // Assert
        Assert.Empty(result.Workspace!.Declarations["P::S::t"].Children);
        Assert.Equal("action", Assert.IsType<SysmlFeatureNode>(Assert.Single(result.Workspace.Declarations["P::S::u"].Children)).FeatureKeyword);
    }

    /// <summary>
    ///     A <c>perform</c> or <c>assert</c> usage named the same as the feature it references
    ///     neither collides with it nor resolves to itself.
    /// </summary>
    [Fact]
    public async Task AstBuilder_PerformAndAssertSameNameAsReferenced_DoNotCollide()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                action vehicleMassTest { action collectData { in item sample; } }
                attribute def Mass;
                constraint massLimitation { in m : Mass; m <= 5 }
                part vehicle {
                    perform vehicleMassTest { in item sample; }
                    assert massLimitation;
                    perform vehicleMassTest.collectData { in item :>> sample; }
                }
                part check { bind vehicle.vehicleMassTest.sample = vehicle.massLimitation.m; }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved", StringComparison.Ordinal));
        var asserted = result.Workspace!.Declarations.Values
            .OfType<SysmlFeatureNode>()
            .Single(f => f.FeatureKeyword == "assert constraint" && f.Name == "massLimitation");
        Assert.StartsWith("P::vehicle::$assert", asserted.QualifiedName, StringComparison.Ordinal);
        Assert.Equal("massLimitation", asserted.ReferenceSubsettingName);
        Assert.NotEqual("P::massLimitation", asserted.QualifiedName);
    }

    /// <summary>An <c>include use case</c> usage is a feature with the keyword <c>include</c> and its body members.</summary>
    [Fact]
    public async Task AstBuilder_IncludeUseCase_BuildsIncludeNode()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                use case def Drive { action steer; }
                use case commute {
                    include use case drive : Drive { action extra; }
                }
            }
            """);

        // Assert
        AssertClean(result);
        var include = FeatureAt(result, "P::commute::drive");
        Assert.Equal("include", include.FeatureKeyword);
        Assert.Equal("Drive", include.FeatureTyping);
        Assert.True(include.IsExcludedFromDiagrams);
        Assert.True(FeatureAt(result, "P::commute::drive::extra").IsExcludedFromDiagrams);
    }

    /// <summary>A form with neither a name nor a reference (<c>assert constraint { ... }</c>) builds no node.</summary>
    [Fact]
    public async Task AstBuilder_AssertConstraint_WithoutNameOrReference_BuildsNoNode()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part p { attribute a; assert constraint { a > 0 } }
            }
            """);

        // Assert
        Assert.DoesNotContain(
            result.Workspace!.Declarations.Values.OfType<SysmlFeatureNode>(),
            f => f.FeatureKeyword == "assert constraint" && f.QualifiedName!.StartsWith("P::", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- multiple redefinitions

    /// <summary>Every redefinition target is recorded, however the targets are written.</summary>
    [Fact]
    public async Task AstBuilder_MultipleRedefinitions_RecordsAllTargets()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def A { attribute a; attribute b; attribute c; }
                part x : A {
                    attribute :>> a :>> b;
                    attribute second :>> b, c;
                    attribute single :>> a;
                }
            }
            """);

        // Assert
        var chained = result.Workspace!.Declarations.Values.OfType<SysmlFeatureNode>()
            .Single(f => f.QualifiedName == "P::x::a");
        Assert.Equal("a", chained.RedefinedFeatureName);
        Assert.Equal(["b"], chained.AdditionalRedefinedFeatureNames);
        var listed = FeatureAt(result, "P::x::second");
        Assert.Equal("b", listed.RedefinedFeatureName);
        Assert.Equal(["c"], listed.AdditionalRedefinedFeatureNames);
        Assert.Null(FeatureAt(result, "P::x::single").AdditionalRedefinedFeatureNames);
    }

    /// <summary>
    ///     Every redefinition target gets its own edge, and the redefining feature inherits the
    ///     members of the second target, which the first does not have.
    /// </summary>
    [Fact]
    public async Task AstBuilder_MultipleRedefinitions_ResolvesEveryTargetAndInheritsFromSecond()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def Driver1 { port setSpeedPort; }
                part def Driver2 { port sensedSpeedPort; }
                part def Sys { part driver : Driver1; part driverB : Driver2; }
                part s : Sys {
                    part :>> driver :>> driverB {
                        port :>> sensedSpeedPort;
                        port :>> setSpeedPort;
                    }
                }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "P::s::driver", "P::Sys::driver"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "P::s::driver", "P::Sys::driverB"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "P::s::driver::sensedSpeedPort", "P::Driver2::sensedSpeedPort"));
    }

    /// <summary>An unresolved second redefinition target is reported once, at its own token.</summary>
    [Fact]
    public async Task AstBuilder_MultipleRedefinitions_UnresolvedSecondTarget_ReportsOneDiagnostic()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def A { attribute a; }
                part x : A {
                    attribute :>> a :>> missing;
                }
            }
            """);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("Unresolved reference: 'missing'", diagnostic.Message);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(4, diagnostic.Line);
        Assert.Equal(29, diagnostic.Column);
    }

    /// <summary>Redefinitions and subsettings are recorded for every kind of usage, not only generic ones.</summary>
    [Fact]
    public async Task AstBuilder_Redefinition_OnActionAndRequirementUsages_IsRecorded()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                requirement def R { attribute limit; }
                action def Act { action step; }
                part p {
                    requirement base : R;
                    requirement derivedReq :> base { attribute :>> limit = 3; }
                    action go : Act { action myStep :>> step; }
                }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.Equal(["base"], FeatureAt(result, "P::p::derivedReq").SupertypeNames);
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "P::p::derivedReq", "P::p::base"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "P::p::go::myStep", "P::Act::step"));
    }

    // ---------------------------------------------------------------- anonymous owner

    /// <summary>An anonymous usage with a body gets a synthetic scope and keeps a null name.</summary>
    [Fact]
    public async Task AstBuilder_AnonymousUsageWithBody_GetsSyntheticScope()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def Base { part elems; }
                part x : Base;
                part y {
                    part :> x { part :>> elems; }
                }
            }
            """);

        // Assert
        AssertClean(result);
        var anonymous = Assert.IsType<SysmlFeatureNode>(Assert.Single(result.Workspace!.Declarations["P::y"].Children));
        Assert.Null(anonymous.Name);
        Assert.StartsWith("P::y::$anon", anonymous.QualifiedName, StringComparison.Ordinal);
        var member = Assert.IsType<SysmlFeatureNode>(Assert.Single(anonymous.Children));
        Assert.Equal($"{anonymous.QualifiedName}::elems", member.QualifiedName);
        Assert.False(anonymous.IsExcludedFromDiagrams);
    }

    /// <summary>
    ///     The members of an anonymous owner resolve in the owner's own scope, which inherits from
    ///     the usage it subsets — not in the enclosing one.
    /// </summary>
    [Fact]
    public async Task AstBuilder_AnonymousSubsettingOwner_ResolvesRedefinedMember()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                attribute def Q { attribute :> OrderedSet::elements; }
                part y {
                    attribute :> differencesOf[1] {
                        item :>> elements = (1, 2);
                    }
                }
            }
            """);

        // Assert: the redefinition of the library member resolves (no unresolved reference)
        Assert.DoesNotContain(result.Diagnostics, d => d.Message == "Unresolved reference: 'elements'");
    }

    /// <summary>An anonymous usage without a body has no scope.</summary>
    [Fact]
    public async Task AstBuilder_AnonymousUsageWithoutBody_HasNoQualifiedName()
    {
        // Arrange / Act
        var result = await LoadAsync("package P { part def T; part y { part : T; } }");

        // Assert
        var anonymous = Assert.IsType<SysmlFeatureNode>(Assert.Single(result.Workspace!.Declarations["P::y"].Children));
        Assert.Null(anonymous.QualifiedName);
    }

    // ---------------------------------------------------------------- resolver

    /// <summary>A reference subsetting is a general type for lookup only: members resolve through it, with no edge.</summary>
    [Fact]
    public async Task NameResolver_ReferenceSubsetting_IsLookupGeneralType()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                part def Base { attribute a; }
                part target : Base;
                part holder {
                    ref part link ::> target { attribute :>> a; }
                }
            }
            """);

        // Assert
        AssertClean(result);
        Assert.DoesNotContain(
            result.Workspace!.Index.AllEdges,
            e => e.SourceQualifiedName == "P::holder::link" && e.TargetQualifiedName == "P::target");
        var resolver = new NameResolver(result.Workspace.Declarations);
        Assert.Contains("P::target", resolver.GeneralTypes("P::holder::link"));
    }

    /// <summary>A transition implicitly specializes the library's transition action.</summary>
    [Fact]
    public async Task NameResolver_Transition_ImplicitlySpecializesTransitionActions()
    {
        // Arrange
        var result = await LoadAsync("""
            package P { state def S { state a; state b; transition go first a then b; } }
            """);
        var resolver = new NameResolver(result.Workspace!.Declarations);

        // Act
        var lookup = resolver.LookupGeneralTypes("P::S::go");

        // Assert
        Assert.Contains("Actions::transitionActions", lookup);
        Assert.NotNull(resolver.Resolve("accepter", "P::S::go"));
    }

    /// <summary>The implicit-specialization rows of the new usage kinds name library features.</summary>
    [Theory]
    [InlineData("perform", "Actions::actions")]
    [InlineData("exhibit", "States::stateActions")]
    [InlineData("include", "UseCases::useCases")]
    [InlineData("assert constraint", "Constraints::constraintChecks")]
    [InlineData("timeslice", "Occurrences::Occurrence::timeSlices")]
    [InlineData("snapshot", "Occurrences::Occurrence::snapshots")]
    public async Task NameResolver_ImplicitSpecialization_NewUsageKinds_AddLibraryGeneralType(string keyword, string expected)
    {
        // Arrange
        var result = await LoadAsync("""
            package P {
                action a1;
                part holder {
                    perform action pa { }
                    exhibit state ex { }
                    include use case inc { }
                    assert constraint ac : Constraints::ConstraintCheck { }
                    timeslice ts;
                    snapshot sn;
                }
            }
            """);
        var resolver = new NameResolver(result.Workspace!.Declarations);
        var node = result.Workspace.Declarations.Values.OfType<SysmlFeatureNode>()
            .Single(f => f.FeatureKeyword == keyword && f.QualifiedName!.StartsWith("P::holder::", StringComparison.Ordinal));

        // Act / Assert
        Assert.Contains(expected, resolver.LookupGeneralTypes(node.QualifiedName!));
        Assert.Contains(expected, NameResolver.ImplicitSpecializationTargets);
    }

    /// <summary><see cref="SysmlNode.DiagramChildren"/> omits the excluded children.</summary>
    [Fact]
    public void SysmlNode_DiagramChildren_OmitsExcludedChildren()
    {
        // Arrange
        var drawn = new SysmlFeatureNode { Name = "a", FeatureKeyword = "part" };
        var hidden = new SysmlFeatureNode { Name = "b", FeatureKeyword = "ref", IsExcludedFromDiagrams = true };
        var parent = new SysmlPackageNode { Name = "P", Children = [drawn, hidden] };

        // Act / Assert
        Assert.Equal([drawn], parent.DiagramChildren);
        Assert.Equal(2, parent.Children.Count);
    }

    /// <summary>The synthetic names of the anonymous action nodes inside a body built for resolution do not shift those of drawn nodes.</summary>
    [Fact]
    public async Task AstBuilder_AnonymousActionNodes_InExcludedBodies_DoNotShiftDrawnNodeNames()
    {
        // Arrange / Act
        var result = await LoadAsync("""
            package P {
                action busy {
                    first start;
                    then fork;
                    then done;
                }
                action def Drawn {
                    first start;
                    then terminate;
                }
            }
            """);

        // Assert
        var terminate = Assert.IsType<SysmlFeatureNode>(
            DefinitionAt(result, "P::Drawn").Children.Single(c => c.Name is { } n && n.StartsWith("$terminate", StringComparison.Ordinal)));
        Assert.Equal("$terminate0", terminate.Name);
        Assert.False(terminate.IsExcludedFromDiagrams);
    }
}
