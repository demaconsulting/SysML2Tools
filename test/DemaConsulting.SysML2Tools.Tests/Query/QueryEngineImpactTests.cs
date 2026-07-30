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
    ///     Topology in which <c>b</c> is reachable from <c>s</c> both over a connector
    ///     (<c>connect b to s</c>, one relationship) and over the subsetting chain
    ///     <c>b :&gt; s2 :&gt; s</c> (two relationships), so the shorter of the two must win, and
    ///     <c>z</c> sits one further connector beyond <c>b</c>.
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
    ///     Connector chain of five connectors joining six declared sibling part usages, so
    ///     <c>a</c> is one connector hop from <c>b</c>, two from <c>c</c>, three from <c>d</c>,
    ///     four from <c>e</c>, and five from <c>f</c>. Every endpoint is a declared part usage
    ///     rather than a nested port, so a reference pass that failed to exclude connector kinds
    ///     would be detected here as duplicate entries or entries attributed to the wrong
    ///     connector. The chain is long enough to distinguish an unlimited budget from a merely
    ///     generous one and to observe a bound of three stopping the walk partway.
    /// </summary>
    private const string LongConnectorChainFixture = """
        package Model {
            part def System {
                part a;
                part b;
                part c;
                part d;
                part e;
                part f;

                connect b to a;
                connect c to b;
                connect d to c;
                connect e to d;
                connect f to e;
            }
        }
        """;

    /// <summary>
    ///     Cyclic connector topology: four part usages joined into a ring. Traversal from
    ///     <c>r1</c> must terminate with no depth bound at all, and <c>r3</c> — reachable
    ///     around either side of the ring — must be attributed its minimum ring distance rather
    ///     than a traversal-order-dependent one.
    /// </summary>
    private const string RingConnectorFixture = """
        package Model {
            part def System {
                part r1;
                part r2;
                part r3;
                part r4;

                connect r2 to r1;
                connect r3 to r2;
                connect r4 to r3;
                connect r1 to r4;
            }
        }
        """;

    /// <summary>
    ///     Topology placing a three-link pure-reference chain and a three-hop pure-connector
    ///     chain on the same subject, so a single depth bound can be observed cutting both
    ///     chains at exactly the same distance.
    /// </summary>
    private const string MixedReferenceAndConnectorFixture = """
        package Model {
            part def Assembly {
                part ref0;
                part ref1 :> ref0;
                part ref2 :> ref1;
                part ref3 :> ref2;
                part con1;
                part con2;
                part con3;

                connect con1 to ref0;
                connect con2 to con1;
                connect con3 to con2;
            }
        }
        """;

    /// <summary>
    ///     Topology in which two elements are each reachable from <c>origin</c> by both a
    ///     reference path and a connector path, with the shorter path being of a different class
    ///     in each case: <c>viaConnector</c> is one connector away but two references away, and
    ///     <c>viaReference</c> is one reference away but two connectors away. Under one uniform
    ///     depth, each must be reported once, at the shorter distance, carrying the relation of
    ///     the path that achieved it.
    /// </summary>
    private const string DualPathFixture = """
        package Model {
            part def Assembly {
                part origin;

                part link :> origin;
                part viaConnector :> link;
                connect viaConnector to origin;

                part viaReference :> origin;
                part relay;
                connect relay to origin;
                connect viaReference to relay;
            }
        }
        """;

    /// <summary>
    ///     Hub-and-spoke connector topology whose ports are declared <b>inline on the part usage</b>
    ///     (<c>part hub { port J1; port J2; }</c>) rather than on a part definition reached through
    ///     a typed usage, as in <see cref="ConnectedPartsFixture"/>.
    ///     <para>
    ///     The distinction is the whole point of this fixture and is invisible in the rendered
    ///     output: declaring the ports inline makes the connector endpoint path
    ///     <c>M::S::hub::J1</c> itself a key in <c>SysmlWorkspace.Declarations</c>, whereas the
    ///     definition-side style leaves the endpoint path undeclared (the declaration is
    ///     <c>Hub::J1</c>). A far-endpoint roll-up that stops at the first declared ancestor
    ///     therefore reports the raw port for this shape only — and, because the reported name is
    ///     also the name enqueued onto the traversal frontier, dead-ends the walk at the port so
    ///     <c>motorB</c> is never reached at any depth. Both modeling styles are legal, so both
    ///     shapes must be covered.
    ///     </para>
    /// </summary>
    private const string InlineUsagePortsFixture = """
        package M {
            part def S {
                part hub {
                    port J1;
                    port J2;
                }

                part motorA;
                part motorB;

                connect hub.J1 to motorA;
                connect hub.J2 to motorB;
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
    ///     An element reachable by both a connector path and a longer reference path is reported
    ///     exactly once, at the shorter of the two distances, and the elements beyond it are
    ///     attributed relative to that shorter distance.
    /// </summary>
    /// <remarks>
    ///     <c>z</c> is expected at depth 2, not 3: with one uniform budget the shortest path is
    ///     <c>s</c> → <c>b</c> (the connector <c>connect b to s</c>) → <c>z</c> (the connector
    ///     <c>connect z to b</c>), two relationships. The two-relationship subsetting detour
    ///     <c>b :&gt; s2 :&gt; s</c> no longer delays <c>b</c>'s connector expansion, because
    ///     there is no second budget for a connector hop to exhaust.
    /// </remarks>
    [Fact]
    public async Task Impact_IncludeConnections_ReachedByTwoPaths_ReportsShortestDistance()
    {
        // Arrange: 'b' is one connector from 's' and two subsettings from 's'; 'z' is one
        // further connector beyond 'b'
        var (workspace, element) = await LoadAsync(MinimumHopFixture, "Model::Assembly::s");

        // Act: walk with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::Assembly::s",
                IncludeConnections = true
            });

        // Assert: 'b' is reported once at the shorter connector distance, and 'z' one beyond it
        var b = Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::b");
        Assert.Equal(1, b.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, b.Relation);

        var z = Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::z");
        Assert.Equal(2, z.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, z.Relation);
    }

    /// <summary>
    ///     With no walk depth supplied the walk is unlimited for connector edges just as it is
    ///     for reference edges, so an entire connector chain is reported, each element at its
    ///     shortest relationship distance from the subject.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_NoWalkDepth_ReachesEntireConnectorChainAtShortestDistance()
    {
        // Arrange: a six-part chain joined by five connectors, queried from one end
        var (workspace, element) = await LoadAsync(LongConnectorChainFixture, "Model::System::a");

        // Act: enable connections and supply no depth bound at all
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::a",
                IncludeConnections = true
            });

        // Assert: every element in the chain is reported exactly once, at its true hop distance
        Assert.Equal(5, result.Entries.Count);
        var expectedDepths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Model::System::b"] = 1,
            ["Model::System::c"] = 2,
            ["Model::System::d"] = 3,
            ["Model::System::e"] = 4,
            ["Model::System::f"] = 5
        };
        foreach (var (name, expectedDepth) in expectedDepths)
        {
            var entry = Assert.Single(result.Entries, e => e.QualifiedName == name);
            Assert.Equal(expectedDepth, entry.Depth);
            Assert.Equal(SysmlEdgeKind.Connect, entry.Relation);
        }
    }

    /// <summary>
    ///     A walk depth of three means "everything within three relationships of the subject",
    ///     so a connector chain is followed exactly three hops and no further.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_WalkDepthThree_BoundsConnectorChainToThreeHops()
    {
        // Arrange: the same six-part connector chain
        var (workspace, element) = await LoadAsync(LongConnectorChainFixture, "Model::System::a");

        // Act: bound the walk to three relationships
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::a",
                IncludeConnections = true,
                WalkDepth = 3
            });

        // Assert: exactly b, c and d are reported — never e or f
        Assert.Equal(3, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.QualifiedName == "Model::System::b");
        Assert.Contains(result.Entries, e => e.QualifiedName == "Model::System::c");
        Assert.Contains(result.Entries, e => e.QualifiedName == "Model::System::d");
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::System::e");
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::System::f");
    }

    /// <summary>
    ///     One walk depth bounds reference and connector relationships identically: a reference
    ///     chain and a connector chain of the same length are both cut at the same distance.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_MixedPaths_WalkDepthBoundsBothEdgeClassesIdentically()
    {
        // Arrange: a three-link reference chain and a three-hop connector chain on one subject
        var (workspace, element) = await LoadAsync(MixedReferenceAndConnectorFixture, "Model::Assembly::ref0");

        // Act: bound the walk to two relationships of any kind
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::Assembly::ref0",
                IncludeConnections = true,
                WalkDepth = 2
            });

        // Assert: both chains are followed to exactly two and cut at exactly three
        Assert.Equal(1, Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::ref1").Depth);
        Assert.Equal(2, Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::ref2").Depth);
        Assert.Equal(1, Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::con1").Depth);
        Assert.Equal(2, Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::con2").Depth);
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::Assembly::ref3");
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "Model::Assembly::con3");
    }

    /// <summary>
    ///     An element reachable by both a reference path and a connector path is reported once,
    ///     at the shorter distance, carrying the relation of the path that achieved it —
    ///     whichever class that path happens to be.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_ReachableByReferenceAndConnector_ReportedOnceAtShorterDistance()
    {
        // Arrange: one element is closer over a connector, another is closer over a reference
        var (workspace, element) = await LoadAsync(DualPathFixture, "Model::Assembly::origin");

        // Act: walk with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::Assembly::origin",
                IncludeConnections = true,
                IncludeStdlib = false
            });

        // Assert: the connector path wins where it is shorter, keeping its Connect relation
        var viaConnector = Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::viaConnector");
        Assert.Equal(1, viaConnector.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, viaConnector.Relation);

        // Assert: the reference path wins where it is shorter, keeping its reference relation
        var viaReference = Assert.Single(result.Entries, e => e.QualifiedName == "Model::Assembly::viaReference");
        Assert.Equal(1, viaReference.Depth);
        Assert.Equal(SysmlEdgeKind.Supertype, viaReference.Relation);
    }

    /// <summary>
    ///     A cyclic connector topology with no depth bound at all terminates and attributes each
    ///     element its minimum ring distance rather than a traversal-order-dependent one.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_RingTopology_NoDepthBound_TerminatesWithShortestDistances()
    {
        // Arrange: four part usages joined into a connector ring
        var (workspace, element) = await LoadAsync(RingConnectorFixture, "Model::System::r1");

        // Act: walk the ring with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::r1",
                IncludeConnections = true
            });

        // Assert: the walk terminated, reporting each neighbour once at its minimum ring distance
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(1, Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::r2").Depth);
        Assert.Equal(1, Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::r4").Depth);
        Assert.Equal(2, Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::r3").Depth);
    }

    /// <summary>
    ///     On a hub-and-spoke topology, a sibling spoke reached through the shared hub is
    ///     reported at depth two rather than suppressed, because no per-edge-class budget is
    ///     exhausted by the first hop into the hub.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_HubTopology_SpokeReachesOtherSpokeAtDepthTwo()
    {
        // Arrange: two motors each connected to a distinct port of a shared hub
        var (workspace, element) = await LoadAsync(ConnectedPartsFixture, "Model::System::motorA");

        // Act: walk from one spoke with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "Model::System::motorA",
                IncludeConnections = true
            });

        // Assert: the hub is at depth one and the sibling spoke beyond it at depth two
        Assert.Equal(1, Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::hub").Depth);
        Assert.Equal(2, Assert.Single(result.Entries, e => e.QualifiedName == "Model::System::motorB").Depth);
    }

    /// <summary>
    ///     Regression: a connector endpoint that is a port declared inline on a part usage — and
    ///     therefore itself a declaration key — is still attributed to the owning part usage, not
    ///     reported raw, with the port preserved in
    ///     <see cref="QueryResultEntry.ViaQualifiedName"/>.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_InlineUsagePortEndpoint_ReportsOwningPartUsage()
    {
        // Arrange: hub-and-spoke topology whose ports are declared inline on the hub usage
        var (workspace, element) = await LoadAsync(InlineUsagePortsFixture, "M::S::motorA");

        // Act: walk from one spoke with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "M::S::motorA",
                IncludeConnections = true
            });

        // Assert: the owning part usage is reported at depth one, with the raw port recorded
        var entry = Assert.Single(result.Entries, e => e.QualifiedName == "M::S::hub");
        Assert.Equal(1, entry.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, entry.Relation);
        Assert.Equal("M::S::hub::J1", entry.ViaQualifiedName);
        Assert.DoesNotContain(result.Entries, e => e.QualifiedName == "M::S::hub::J1");
    }

    /// <summary>
    ///     Regression: rolling an inline-declared port endpoint up to its owning part usage also
    ///     places that part — not the port — on the traversal frontier, so the walk continues
    ///     through the hub and reaches the far spoke. Stopping at the port dead-ends the walk and
    ///     makes the far spoke unreachable at any depth, which is what this asserts against.
    /// </summary>
    [Fact]
    public async Task Impact_IncludeConnections_InlineUsagePortEndpoint_ContinuesTraversalBeyondPortOwner()
    {
        // Arrange: hub-and-spoke topology whose ports are declared inline on the hub usage
        var (workspace, element) = await LoadAsync(InlineUsagePortsFixture, "M::S::motorA");

        // Act: walk from one spoke with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions
            {
                Verb = QueryVerb.Impact,
                Element = "M::S::motorA",
                IncludeConnections = true
            });

        // Assert: the sibling spoke beyond the hub is reached, at exactly one further hop
        var entry = Assert.Single(result.Entries, e => e.QualifiedName == "M::S::motorB");
        Assert.Equal(2, entry.Depth);
        Assert.Equal(SysmlEdgeKind.Connect, entry.Relation);
        Assert.Null(entry.ViaQualifiedName);
    }

    /// <summary>
    ///     Regression: no impact row names a port, for either port-declaration style. A port is
    ///     an attachment point rather than an actionable impact subject, so its presence would be
    ///     a name the caller can neither act on nor feed back into another query.
    /// </summary>
    /// <param name="fixture">The connector fixture to walk.</param>
    /// <param name="subject">The qualified name of the spoke to query from.</param>
    [Theory]
    [InlineData(InlineUsagePortsFixture, "M::S::motorA")]
    [InlineData(ConnectedPartsFixture, "Model::System::motorA")]
    public async Task Impact_IncludeConnections_PortDeclarationStyles_ReportNoPortEntries(
        string fixture, string subject)
    {
        // Arrange: load the fixture and resolve the spoke to query from
        var (workspace, element) = await LoadAsync(fixture, subject);

        // Act: walk with connections enabled and no depth bound
        var result = QueryEngine.Impact(
            workspace,
            element,
            new QueryOptions { Verb = QueryVerb.Impact, Element = subject, IncludeConnections = true });

        // Assert: results are non-empty and no reported element is a port
        Assert.NotEmpty(result.Entries);
        Assert.DoesNotContain(result.Entries, e => e.Kind == "port");
    }
}
