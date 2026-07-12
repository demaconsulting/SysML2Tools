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

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Primary integration suite for the 11 real <c>query</c> verb implementations: each test
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
    ///     'impact' with --depth 1 only reaches direct incoming references, not their own
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
            sysml, "impact", "--element", "Model::Root", "--depth", "1");

        Assert.Equal(0, exitCode);
        Assert.Contains("Model::Mid", output);
        Assert.DoesNotContain("Model::Leaf", output);
    }

    /// <summary>
    ///     'impact' with no --depth reaches the full transitive closure.
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
        // contain an embedded newline, and no orphan '*' continuation-marker lines exist.
        var lines = output.Split('\n');
        Assert.DoesNotContain(lines, line => line.Trim().StartsWith('*'));
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
