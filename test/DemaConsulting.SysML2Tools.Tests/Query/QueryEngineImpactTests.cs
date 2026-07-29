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
}
