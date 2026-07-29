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
using DemaConsulting.SysML2Tools.Query;

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Primary integration suite for the 12 real <c>query</c> verb implementations: each test
///     builds a small, fully-controlled inline SysML fixture and asserts on the resulting
///     Markdown output produced end-to-end through <see cref="Program.RunAsync"/>.
/// </summary>
[Collection("Sequential")]
public class QueryVerbsTests
{
    /// <summary>
    ///     'uses' reports an element's outgoing supertype, typing, and import edges.
    /// </summary>
    [Fact]
    public async Task Uses_ReportsOutgoingSupertypeTypingImportEdges()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def Engine;
                part def Truck :> Vehicle {
                    part engine : Engine;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "uses", "--element", "Model::Truck");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.Contains("supertype", output);
    }

    /// <summary>
    ///     'used-by' reports the reverse of 'uses': elements that reference the target.
    /// </summary>
    [Fact]
    public async Task UsedBy_ReportsIncomingReferences()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def Truck specializes Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "used-by", "--element", "Model::Vehicle");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Car", output);
        Assert.Contains("Model::Truck", output);
    }

    /// <summary>
    ///     'dependencies' combines 'uses' (outgoing) and 'used-by' (incoming) for one element
    ///     into a single prose result: a "Depends on" bullet for each outgoing reference and a
    ///     "Used by" bullet for each incoming reference.
    /// </summary>
    [Fact]
    public async Task Dependencies_CombinesOutgoingAndIncoming_ReportsBothDirections()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def Truck specializes Car;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "dependencies", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Depends on **Vehicle** (supertype)", output);
        Assert.Contains("Used by **Truck** (supertype)", output);
    }

    /// <summary>
    ///     'dependencies' reports a single prose line (instead of a bullet list) when the
    ///     target element has no outgoing references.
    /// </summary>
    [Fact]
    public async Task Dependencies_NoOutgoingReferences_ReportsProseLineInsteadOfBulletList()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "dependencies", "--element", "Model::Vehicle");

        Assert.Equal(0, exitCode);
        Assert.Contains("Vehicle has no outgoing references.", output);
        Assert.DoesNotContain("Depends on", output);
    }

    /// <summary>
    ///     'dependencies' reports a single prose line (instead of a bullet list) when no other
    ///     element references the target element.
    /// </summary>
    [Fact]
    public async Task Dependencies_NoIncomingReferences_ReportsProseLineInsteadOfBulletList()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "dependencies", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("No elements reference Car.", output);
        Assert.DoesNotContain("Used by", output);
    }

    /// <summary>
    ///     'dependencies' renders its body as prose bullets, never a Markdown table, unlike
    ///     every other verb.
    /// </summary>
    [Fact]
    public async Task Dependencies_MarkdownOutput_ContainsNoTable()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def Truck specializes Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "dependencies", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("| Qualified Name | Kind | Detail |", output);
    }

    /// <summary>
    ///     'impact' with --walk-depth 1 only reaches direct incoming references, not their own
    ///     incoming references.
    /// </summary>
    [Fact]
    public async Task Impact_DepthOne_OnlyReachesDirectReferences()
    {
        const string sysml = """
            package Model {
                part def Root;
                part def Mid specializes Root;
                part def Leaf specializes Mid;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "impact", "--element", "Model::Root", "--walk-depth", "1");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Mid", output);
        Assert.DoesNotContain("Model::Leaf", output);
    }

    /// <summary>
    ///     'impact' with no --walk-depth reaches the full transitive closure.
    /// </summary>
    [Fact]
    public async Task Impact_Unbounded_ReachesTransitiveClosure()
    {
        const string sysml = """
            package Model {
                part def Root;
                part def Mid specializes Root;
                part def Leaf specializes Mid;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "impact", "--element", "Model::Root");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Mid", output);
        Assert.Contains("Model::Leaf", output);
    }

    /// <summary>
    ///     'impact' without --include-connections keeps its existing reference-only semantics:
    ///     a part usage whose only relationship to the rest of the model is a connector reports
    ///     no impacted elements.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnectionsFlagAbsent_ReportsReferenceOnlyResult()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.GantryConnections, "impact", "--element", "Model::System::motorA");

        Assert.Equal(0, exitCode);
        Assert.Contains("0 element(s) transitively impacted", output);
        Assert.DoesNotContain("Model::System::hub", output);
        Assert.DoesNotContain("including connections", output);
    }

    /// <summary>
    ///     'impact --include-connections' reaches the part usage on the far side of a connector,
    ///     which the reference-only walk cannot see.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_ReachesConnectedSiblingPart()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.GantryConnections,
            "impact",
            "--element",
            "Model::System::motorA",
            "--include-connections");

        Assert.Equal(0, exitCode);
        Assert.Contains("| Model::System::hub | part |", output);
        Assert.Contains("including connections (connection hops <= 1)", output);
    }

    /// <summary>
    ///     Connector traversal is undirected: querying from the endpoint that the connector
    ///     declaration names second (the hub) reaches the originating parts just as querying
    ///     from the first-named endpoint reaches the hub.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_QueriedFromFarEndpoint_ReachesOriginatingPart()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.GantryConnections,
            "impact",
            "--element",
            "Model::System::hub",
            "--include-connections");

        Assert.Equal(0, exitCode);
        Assert.Contains("| Model::System::motorA | part |", output);
        Assert.Contains("| Model::System::motorB | part |", output);
    }

    /// <summary>
    ///     Connector endpoints are nested ports, but impact entries are attributed to the
    ///     nearest owning part usage, with the raw port retained in the entry notes so no
    ///     information is lost.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_PortEndpoints_RollUpToOwningPartUsage()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.GantryConnections,
            "impact",
            "--element",
            "Model::System::motorA",
            "--include-connections");

        Assert.Equal(0, exitCode);
        Assert.Contains("| Model::System::hub | part |", output);
        Assert.Contains("Model::System::motorA::power -> Model::System::hub::J1", output);
    }

    /// <summary>
    ///     With no --walk-depth supplied, reference-edge traversal stays unlimited but connector
    ///     hops are bounded to one, so the second motor (two connector hops away, through the
    ///     hub) is not reported.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_NoWalkDepth_BoundsConnectionHopsToOne()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.GantryConnections,
            "impact",
            "--element",
            "Model::System::motorA",
            "--include-connections");

        Assert.Equal(0, exitCode);
        Assert.Contains("| Model::System::hub | part |", output);
        Assert.DoesNotContain("Model::System::motorB", output);
    }

    /// <summary>
    ///     Supplying --walk-depth raises the connector hop bound to that same value, so the
    ///     second motor is reached through the hub at the second hop.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_WalkDepthTwo_ReachesSecondConnectionHop()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.GantryConnections,
            "impact",
            "--element",
            "Model::System::motorA",
            "--include-connections",
            "--walk-depth",
            "2");

        Assert.Equal(0, exitCode);
        Assert.Contains("| Model::System::hub | part |", output);
        Assert.Contains("| Model::System::motorB | part |", output);
        Assert.Contains("including connections (connection hops <= 2)", output);
    }

    /// <summary>
    ///     A cyclic connector topology (two parts joined by two connectors in opposite textual
    ///     order) terminates and reports each impacted element exactly once.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_CyclicConnections_TerminatesWithoutDuplicates()
    {
        const string sysml = """
            package Model {
                part def Hub {
                    port J1;
                    port J2;
                }

                part def Motor {
                    port power;
                    port encoder;
                }

                part def System {
                    part hub : Hub;
                    part motorA : Motor;

                    connect motorA.power to hub.J1;
                    connect hub.J2 to motorA.encoder;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "impact", "--element", "Model::System::motorA", "--include-connections", "--walk-depth", "5");

        Assert.Equal(0, exitCode);
        Assert.Contains("1 element(s) transitively impacted", output);
        Assert.Single(
            output.Split('\n'),
            line => line.StartsWith("| Model::System::hub |", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Binding connectors ('bind A = B;') are traversed undirected exactly like connection
    ///     connectors, reachable from either bound side.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_BindingEdges_AreTraversedUndirected()
    {
        const string sysml = """
            package Model {
                part def Controller {
                    attribute setPoint;
                }

                part def Actuator {
                    attribute command;
                }

                part def System {
                    part controller : Controller;
                    part actuator : Actuator;

                    bind controller.setPoint = actuator.command;
                }
            }
            """;

        var (fromA, exitCodeA) = await QueryTestFixtures.RunQueryAsync(
            sysml, "impact", "--element", "Model::System::controller", "--include-connections");
        var (fromB, exitCodeB) = await QueryTestFixtures.RunQueryAsync(
            sysml, "impact", "--element", "Model::System::actuator", "--include-connections");

        Assert.Equal(0, exitCodeA);
        Assert.Equal(0, exitCodeB);
        Assert.Contains("| Model::System::actuator | part |", fromA);
        Assert.Contains("| Model::System::controller | part |", fromB);
    }

    /// <summary>
    ///     When a connector names directly declared sibling part usages as its endpoints, the far
    ///     endpoint requires no roll-up and is reported as the impacted element. The enclosing
    ///     'part def' that owns both endpoints is never reported in its place.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_DeclaredEndpointConnector_ReportsSiblingPartNotOwningDefinition()
    {
        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            QueryTestFixtures.DeclaredEndpointConnections,
            "impact",
            "--element",
            "Model::System::alpha",
            "--include-connections");

        Assert.Equal(0, exitCode);
        Assert.Contains("| Model::System::beta | part |", output);
        Assert.DoesNotContain("| Model::System | part def |", output);
    }

    /// <summary>
    ///     'describe' reports the element's kind, supertypes, annotation text, and child count.
    /// </summary>
    [Fact]
    public async Task Describe_ReportsKindSupertypesAnnotationsAndChildren()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                comment about Car /* A road vehicle */
                part def Car specializes Vehicle {
                    part engine;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("part def", output);
        Assert.Contains("Model::Vehicle", output);
        Assert.Contains("Children: 1", output);
    }

    /// <summary>
    ///     'describe' reports a <c>Children: N</c> count that always matches the number of rows
    ///     in the Entries table. A bare metadata annotation (<c>@Critical;</c>) is a non-element
    ///     child with no qualified name, so it is present in the underlying AST child list but
    ///     excluded from both the count and the table; only the one real child (<c>engine</c>)
    ///     is counted and shown.
    /// </summary>
    [Fact]
    public async Task Describe_ChildIncludesNonElementMetadataAnnotation_ChildrenCountMatchesEntryRows()
    {
        const string sysml = """
            package Model {
                metadata def Critical;

                part def Car {
                    @Critical;
                    part engine;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Children: 1", output);
        Assert.Contains("Model::Car::engine", output);

        var tableRows = output
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith("| Model::Car::", StringComparison.Ordinal));
        Assert.Equal(1, tableRows);
    }

    /// <summary>
    ///     'describe' collapses a multi-line comment/documentation annotation into a single
    ///     summary line, so the Markdown output keeps one fact per bullet rather than letting
    ///     the annotation's embedded newlines and '*' continuation markers spill across
    ///     multiple raw lines.
    /// </summary>
    [Fact]
    public async Task Describe_MultiLineComment_CollapsesToSingleSummaryLine()
    {
        const string sysml = """
            package Model {
                part def Car {
                    doc /*
                     * A multi-line
                     * doc comment.
                     */
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Documentation: A multi-line doc comment.", output);

        // The bullet list must remain one fact per line: no bullet's text should itself
        // contain an embedded newline, and no orphan '* ' continuation-marker lines exist
        // (checked as "* " with a trailing space, so this doesn't false-positive on
        // legitimate "**Bold**" entries-label lines, which have no space after the stars).
        var lines = output.Split('\n');
        Assert.DoesNotContain(lines, line => line.Trim().StartsWith("* ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     'describe' reports a bare (no attributes) applied metadata annotation as a single
    ///     <c>"Metadata {Type}"</c> summary line, without a trailing attribute suffix.
    /// </summary>
    [Fact]
    public async Task Describe_BareMetadataAnnotation_ReportsMetadataTypeLineOnly()
    {
        const string sysml = """
            package Model {
                metadata def Critical;

                part def Car {
                    @Critical;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Metadata Critical", output);
        Assert.DoesNotContain("Metadata Critical.", output);
    }

    /// <summary>
    ///     'describe' reports each scalar (boolean/number/string) attribute of an applied
    ///     metadata annotation as its own <c>"Metadata {Type}.{Attribute}: {value}"</c> summary
    ///     line.
    /// </summary>
    [Fact]
    public async Task Describe_MetadataWithScalarAttributes_ReportsOnePerAttributeLine()
    {
        const string sysml = """
            package Model {
                metadata def SoftwareInfo {
                    attribute description : String;
                    attribute priority : Number;
                    attribute isCritical : Boolean;
                }

                part def Car {
                    @SoftwareInfo {
                        description = "Engine control";
                        priority = 1;
                        isCritical = true;
                    }
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Metadata SoftwareInfo.description: Engine control", output);
        Assert.Contains("Metadata SoftwareInfo.priority: 1", output);
        Assert.Contains("Metadata SoftwareInfo.isCritical: true", output);
    }

    /// <summary>
    ///     'describe' falls back to the verbatim raw text of a non-scalar (e.g. list-valued)
    ///     metadata attribute value, since only scalar boolean/number/string literals are
    ///     evaluated - the value is never silently dropped.
    /// </summary>
    [Fact]
    public async Task Describe_MetadataWithUnsupportedListAttribute_FallsBackToRawText()
    {
        const string sysml = """
            package Model {
                metadata def SoftwareInfo {
                    attribute sourceFiles : String[0..*];
                }

                part def Car {
                    @SoftwareInfo {
                        sourceFiles = ("a.cs", "b.cs");
                    }
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Metadata SoftwareInfo.sourceFiles: (\"a.cs\",\"b.cs\")", output);
    }

    /// <summary>
    ///     'describe' reports multiple metadata annotations applied to the same element
    ///     independently: each produces its own summary line(s), and neither overwrites the
    ///     other.
    /// </summary>
    [Fact]
    public async Task Describe_MultipleMetadataAnnotations_ReportsEachIndependently()
    {
        const string sysml = """
            package Model {
                metadata def Safety;
                metadata def Critical;

                part def Car {
                    @Safety;
                    @Critical;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Metadata Safety", output);
        Assert.Contains("Metadata Critical", output);
    }

    /// <summary>
    ///     'describe --format json' includes the same "Metadata ..." summary lines in the
    ///     JSON <c>Summary</c> array as the Markdown output, since Summary is rendered directly
    ///     from the shared <see cref="QueryResult"/> without any Markdown-only transformation.
    /// </summary>
    [Fact]
    public async Task Describe_FormatJson_IncludesMetadataInSummaryArray()
    {
        const string sysml = """
            package Model {
                metadata def SoftwareInfo {
                    attribute isCritical : Boolean;
                }

                part def Car {
                    @SoftwareInfo {
                        isCritical = true;
                    }
                }
            }
            """;

        var (json, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "describe", "--element", "Model::Car", "--format", "json");

        Assert.Equal(0, exitCode);

        // The unresolved-reference diagnostic for the built-in "Boolean" type is written as a
        // single-line "SysmlDiagnostic { ... }" record before the JSON result, so locate the
        // JSON document's opening brace via the unambiguous "Verb" property rather than the
        // first '{' in the captured output.
        var verbIndex = json.IndexOf("\"Verb\":", StringComparison.Ordinal);
        var jsonStart = json.LastIndexOf('{', verbIndex);
        var deserialized = JsonSerializer.Deserialize(
            json[jsonStart..], QueryResultSerializerContext.Default.QueryResult);

        Assert.NotNull(deserialized);
        Assert.Contains("Metadata SoftwareInfo.isCritical: true", deserialized.Summary);
    }

    /// <summary>
    ///     'hierarchy' with --direction up reports only supertypes.
    /// </summary>
    [Fact]
    public async Task Hierarchy_DirectionUp_ReportsOnlySupertypes()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def SportsCar specializes Car;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "hierarchy", "--element", "Model::Car", "--direction", "up");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.DoesNotContain("Model::SportsCar", output);
    }

    /// <summary>
    ///     'hierarchy' with --direction down reports only subtypes.
    /// </summary>
    [Fact]
    public async Task Hierarchy_DirectionDown_ReportsOnlySubtypes()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def SportsCar specializes Car;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "hierarchy", "--element", "Model::Car", "--direction", "down");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::SportsCar", output);
        Assert.DoesNotContain("Model::Vehicle", output);
    }

    /// <summary>
    ///     'hierarchy' with --direction both (the default) reports both supertypes and subtypes.
    /// </summary>
    [Fact]
    public async Task Hierarchy_DirectionBoth_ReportsSupertypesAndSubtypes()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
                part def SportsCar specializes Car;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "hierarchy", "--element", "Model::Car");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.Contains("Model::SportsCar", output);
    }

    /// <summary>
    ///     'requirements' reports satisfy, verify, and allocate edges in both directions.
    /// </summary>
    [Fact]
    public async Task Requirements_ReportsSatisfyVerifyAndAllocateEdges()
    {
        const string sysml = """
            package Model {
                requirement def R;
                requirement req : R;
                part def Q {}
                part subj : Q;
                satisfy req by subj;

                requirement outer {
                    verify req;
                }

                part a : Q;
                part b : Q;
                allocate a to b;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "requirements", "--element", "Model::req");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::subj", output);
        Assert.Contains("satisfied-by", output);
        Assert.Contains("Model::outer", output);
        Assert.Contains("verified-by", output);
    }

    /// <summary>
    ///     'interface' reports ports and typed features of a definition.
    /// </summary>
    [Fact]
    public async Task Interface_ReportsPortsAndTypedFeatures()
    {
        const string sysml = """
            package Model {
                part def FuelPort;
                part def Engine {
                    port fuelPort : FuelPort;
                    attribute mass;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "interface", "--element", "Model::Engine");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Engine::fuelPort", output);
        Assert.Contains("FuelPort", output);
        Assert.DoesNotContain("Model::Engine::mass", output);
    }

    /// <summary>
    ///     'connections' reports resolved connection endpoints, including a dotted feature-chain
    ///     endpoint.
    /// </summary>
    [Fact]
    public async Task Connections_ReportsResolvedEndpointsIncludingFeatureChain()
    {
        const string sysml = """
            package Model {
                part def Port;
                part def Engine {
                    port fuelPort : Port;
                }
                part def Tank {
                    port outlet : Port;
                }
                part def Vehicle {
                    part engine : Engine;
                    part tank : Tank;
                    connection link connect engine.fuelPort to tank.outlet;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "connections", "--element", "Model::Vehicle::engine");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle::tank::outlet", output);
        Assert.Contains("connection", output);
    }

    /// <summary>
    ///     'states' reports states and guarded transitions nested under a state-machine element.
    /// </summary>
    [Fact]
    public async Task States_ReportsStatesAndGuardedTransitions()
    {
        const string sysml = """
            package Model {
                state def Light {
                    state stop;
                    state go;
                    transition first stop if t then go;
                }
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(
            sysml, "states", "--element", "Model::Light");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Light::stop", output);
        Assert.Contains("Model::Light::go", output);
        Assert.Contains("transition", output);
        Assert.Contains(" if t", output);
    }

    /// <summary>
    ///     'list' with no filters returns every non-stdlib element in the workspace.
    /// </summary>
    [Fact]
    public async Task List_NoFilters_ReturnsAllNonStdlibElements()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Car specializes Vehicle;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(sysml, "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.Contains("Model::Car", output);
    }

    /// <summary>
    ///     'list' with --kind filters by the element's display kind.
    /// </summary>
    [Fact]
    public async Task List_KindFilter_OnlyMatchesGivenKind()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                attribute def Mass;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(sysml, "list", "--kind", "part def");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.DoesNotContain("Model::Mass", output);
    }

    /// <summary>
    ///     'list' with --name filters by a substring of the name/qualified name.
    /// </summary>
    [Fact]
    public async Task List_NameFilter_OnlyMatchesGivenSubstring()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Engine;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(sysml, "list", "--name", "Vehicle");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Vehicle", output);
        Assert.DoesNotContain("Model::Engine", output);
    }

    /// <summary>
    ///     'find' with a --name filter succeeds and behaves like 'list' with the same filter.
    /// </summary>
    [Fact]
    public async Task Find_WithNameFilter_Succeeds()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
                part def Engine;
            }
            """;

        var (output, exitCode) = await QueryTestFixtures.RunQueryAsync(sysml, "find", "--name", "Engine");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Engine", output);
        Assert.DoesNotContain("Model::Vehicle", output);
    }

    /// <summary>
    ///     '--include-stdlib' includes OMG standard-library elements; without it, they are
    ///     excluded from 'list' output.
    /// </summary>
    [Fact]
    public async Task List_IncludeStdlib_TogglesStandardLibraryVisibility()
    {
        const string sysml = """
            package Model {
                part def Vehicle;
            }
            """;

        var (withoutStdlib, _) = await QueryTestFixtures.RunQueryAsync(sysml, "list");
        var (withStdlib, _) = await QueryTestFixtures.RunQueryAsync(sysml, "list", "--include-stdlib");

        Assert.Contains("Model::Vehicle", withoutStdlib);
        Assert.Contains("Model::Vehicle", withStdlib);
        Assert.True(withStdlib.Length > withoutStdlib.Length);
    }
}
