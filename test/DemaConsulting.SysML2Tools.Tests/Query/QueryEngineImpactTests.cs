// <copyright file="QueryEngineImpactTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Query;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     API-only suite for connection-aware <c>impact</c> analysis and the structured traversal
///     metadata carried on each <see cref="QueryResultEntry"/>. Every test here follows the
///     non-CLI client pattern (as used by SysML2Workbench): load a workspace through
///     <see cref="WorkspaceLoader"/>, construct a <see cref="QueryOptions"/>, call
///     <see cref="QueryEngine"/>, and read the structured entry properties — never the CLI, and
///     never the human-readable <see cref="QueryResultEntry.Detail"/> text.
/// </summary>
[Collection("Sequential")]
public class QueryEngineImpactTests
{
    /// <summary>
    ///     Minimal connector topology: two motor part usages each connect one of their nested
    ///     ports to a distinct port of a shared hub part usage, with a supertype reference
    ///     between two definitions so both the reference and connector branches can be observed.
    /// </summary>
    private const string ConnectedPartsFixture = """
        package Model {
            part def Hub {
                port J1;
                port J2;
            }

            part def Motor {
                port power;
                port encoder;
            }

            part def ServoMotor specializes Motor;

            part def System {
                part hub : Hub;
                part motorA : Motor;
                part motorB : Motor;

                connect motorA.power to hub.J1;
                connect motorB.power to hub.J2;
            }
        }
        """;

    /// <summary>
    ///     Connector topology whose endpoints are themselves declared part usages rather than
    ///     nested ports, so the far-endpoint attribution has no ancestor to roll up to and must
    ///     report the endpoint itself.
    /// </summary>
    private const string DeclaredEndpointsFixture = """
        package Model {
            part def System {
                part alpha;
                part beta;
                part gamma;

                connect alpha to beta;
                bind beta = gamma;
            }
        }
        """;

    /// <summary>
    ///     Connector topology placing the nested-port endpoint on the connector's <b>source</b>
    ///     side, so the subject part usage is itself the incoming-edge key for the connector.
    ///     That orientation is what exposes duplicate attribution: an unfiltered reference pass
    ///     reports the raw port in addition to the correctly rolled-up owning part usage.
    /// </summary>
    private const string SourceSidePortFixture = """
        package Model {
            port def PowerPort;

            part def Hub {
                port J1 : PowerPort;
            }

            part def System {
                part hub : Hub;
                part motorA;

                connect hub.J1 to motorA;
            }
        }
        """;

    /// <summary>
    ///     Topology in which <c>b</c> is first reached from <c>s</c> over a connector (one hop)
    ///     and later re-reached one level deeper over the subsetting chain <c>b :&gt; s2 :&gt; s</c>
    ///     at zero hops, so the minimum-hop cycle guard must re-expand it for <c>z</c> to be found.
    /// </summary>
    private const string MinimumHopFixture = """
        package Model {
            part def Assembly {
                part s;
                part s2 :> s;
                part b :> s2;
                part z;

                connect b to s;
                connect z to b;
            }
        }
        """;

    /// <summary>
    ///     Writes the fixture to a temp file, loads it exactly as a non-CLI library caller
    ///     would, resolves the named element, and returns both for a direct
    ///     <see cref="QueryEngine"/> call.
    /// </summary>
    /// <param name="sysml">The inline SysML source to load.</param>
    /// <param name="qualifiedName">The qualified name of the element to resolve.</param>
    /// <returns>The loaded workspace and the resolved element.</returns>
    private static async Task<(SysmlWorkspace Workspace, SysmlNode Element)> LoadAsync(
        string sysml, string qualifiedName)
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var loadResult = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(loadResult.Workspace);
            Assert.True(
                loadResult.Workspace.Declarations.TryGetValue(qualifiedName, out var element),
                $"'{qualifiedName}' was not found in the loaded workspace.");

            return (loadResult.Workspace, element);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A non-CLI caller enables connection-aware impact purely through
    ///     <see cref="QueryOptions.IncludeConnections"/> and receives the connected part usage as
    ///     a result entry, with no CLI parsing or console output involved.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_ThroughPublicApi_ReturnsConnectedPartEntries()
    {
        var (workspace, element) = await LoadAsync(ConnectedPartsFixture, "Model::System::motorA");

        var withoutConnections = QueryEngine.Impact(
            workspace, element, new QueryOptions { Verb = QueryVerb.Impact, Element = "Model::System::motorA" });
        var withConnections = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::motorA",
                IncludeConnections = true
            });

        Assert.Empty(withoutConnections.Entries);
        Assert.Contains(withConnections.Entries, e => e.QualifiedName == "Model::System::hub");
    }

    /// <summary>
    ///     A connection entry carries machine-readable depth, relation, and far-endpoint
    ///     metadata, so a client never has to parse the free-form detail text.
    /// </summary>
    [Fact]
    public async Task Impact_ConnectionEntry_RecordsDepthRelationAndViaQualifiedName()
    {
        var (workspace, element) = await LoadAsync(ConnectedPartsFixture, "Model::System::motorA");

        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::motorA",
                IncludeConnections = true
            });

        var entry = Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::hub");
        Assert.Equal(1, entry.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, entry.Relation);
        Assert.Equal("Model::System::hub::J1", entry.ViaQualifiedName);
    }

    /// <summary>
    ///     A reference entry carries the same structured depth metadata and reports the resolved
    ///     reference edge kind that reached it, with no far-endpoint roll-up recorded.
    /// </summary>
    [Fact]
    public async Task Impact_ReferenceEntry_RecordsDepthAndReferenceRelation()
    {
        var (workspace, element) = await LoadAsync(ConnectedPartsFixture, "Model::Motor");

        var result = QueryEngine.Impact(
            workspace, element, new QueryOptions { Verb = QueryVerb.Impact, Element = "Model::Motor" });

        var entry = Assert.Single(result.Entries, e => e.QualifiedName == "Model::ServoMotor");
        Assert.Equal(1, entry.Depth);
        Assert.Equal(SysmlEdgeKind.Supertype, entry.Relation);
        Assert.Null(entry.ViaQualifiedName);
    }

    /// <summary>
    ///     When a connector's far endpoint is itself a declared element, the endpoint is reported
    ///     as the impacted item. Neither the enclosing definition that also owns the subject nor
    ///     the subject itself is ever reported, so <c>impact</c> agrees with the topology that the
    ///     <c>connections</c> verb reports for the same connector.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_DeclaredFarEndpoint_ReportsEndpointItself()
    {
        var (workspace, element) = await LoadAsync(DeclaredEndpointsFixture, "Model::System::alpha");

        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::alpha",
                IncludeConnections = true
            });

        var entry = Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::beta");
        Assert.Equal(1, entry.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, entry.Relation);
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::System");
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::System::alpha");
    }

    /// <summary>
    ///     A connection entry whose far endpoint required no roll-up omits
    ///     <see cref="QueryResultEntry.ViaQualifiedName"/>, honoring the documented "null when no
    ///     roll-up occurred" contract, while the notes still name the connector's raw endpoints so
    ///     no information is lost.
    /// </summary>
    [Fact]
    public async Task Impact_ConnectionEntry_WithoutRollUp_OmitsViaQualifiedName()
    {
        var (workspace, element) = await LoadAsync(DeclaredEndpointsFixture, "Model::System::alpha");

        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::alpha",
                IncludeConnections = true
            });

        var entry = Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::beta");
        Assert.Null(entry.ViaQualifiedName);
        Assert.Contains(entry.Notes, n => n.Contains("Model::System::beta", StringComparison.Ordinal));
        Assert.Contains(entry.Notes, n => n.Contains("Model::System::alpha", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A connector whose nested-port endpoint sits on the source side yields exactly one
    ///     entry — the port's owning part usage — and never an additional raw-port entry.
    /// </summary>
    /// <remarks>
    ///     <c>Assert.Single</c> is applied to the whole entry list rather than through the
    ///     predicate overload on purpose: the predicate overload asserts only that a
    ///     <i>matching</i> entry is unique and is structurally blind to an extra non-matching
    ///     entry, which is exactly how the duplicate raw-port entry escaped detection.
    /// </remarks>
    [Fact]
    public async Task Impact_IncludeConnections_SourceSidePortEndpoint_ProducesExactlyOneEntry()
    {
        var (workspace, element) = await LoadAsync(SourceSidePortFixture, "Model::System::motorA");

        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::motorA",
                IncludeConnections = true
            });

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Model::System::hub", entry.QualifiedName);
        Assert.Equal(1, entry.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, entry.Relation);
        Assert.Equal("Model::System::hub::J1", entry.ViaQualifiedName);
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::System::hub::J1");
    }

    /// <summary>
    ///     An element re-reached at a strictly lower connector-hop count is re-expanded so
    ///     elements beyond it are not lost, while its already-recorded first-arrival depth and
    ///     relation attribution are retained and no duplicate entry is emitted.
    /// </summary>
    /// <remarks>
    ///     <c>z</c> is expected at depth 3, not 2: <c>b</c> is re-reached cheaply at
    ///     breadth-first level 2 and therefore expands its connectors at level 3, which is
    ///     genuinely the first level at which <c>z</c> is reachable within the hop budget.
    /// </remarks>
    [Fact]
    public async Task Impact_IncludeConnections_ReReachedAtLowerHopCount_KeepsFirstArrivalDepthAndAttribution()
    {
        var (workspace, element) = await LoadAsync(MinimumHopFixture, "Model::Assembly::s");

        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::Assembly::s",
                IncludeConnections = true
            });

        var b = Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::b");
        Assert.Equal(1, b.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, b.Relation);

        var z = Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::z");
        Assert.Equal(3, z.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, z.Relation);
    }
}
