// <copyright file="QueryEngineElementLookupTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text.Json;
using DemaConsulting.SysML2Tools.Query;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     API-only suite for element lookup by any name that resolves under SysML v2 name
///     resolution: a facade package's <c>public import</c>, a nested facade chain, and an alias,
///     alongside the unchanged owning-qualified-name and not-found behavior. Each test loads a
///     workspace through <see cref="WorkspaceLoader"/> and drives <see cref="QueryEngine"/> and
///     <see cref="QueryResultRenderer"/> exactly as a non-CLI library caller would.
/// </summary>
[Collection("Sequential")]
public class QueryEngineElementLookupTests
{
    /// <summary>
    ///     A unit package re-exported publicly by a facade package, a unit package imported
    ///     privately by a second package, and an alias declared beside its target.
    /// </summary>
    private const string FacadeFixture = """
        package UnitPkg {
            part def Unit;
        }

        package Facade {
            public import UnitPkg::*;
        }

        package Hidden {
            private import UnitPkg::*;
        }

        package Aliased {
            part def Original;
            alias Short for Original;
        }

        package OtherPkg {
            part def Unit;
        }

        package Conflicting {
            public import UnitPkg::*;
            public import OtherPkg::*;
        }

        alias RootShort for UnitPkg::Unit;
        """;

    /// <summary>
    ///     A three-level facade chain: a system facade re-exports a subsystem facade, which
    ///     re-exports a unit package.
    /// </summary>
    private const string ChainFixture = """
        package UnitPkg {
            part def Unit;
        }

        package SubsystemPkg {
            public import UnitPkg::*;
        }

        package SystemPkg {
            public import SubsystemPkg::*;
        }
        """;

    /// <summary>
    ///     Loads the fixture exactly as a non-CLI library caller would.
    /// </summary>
    /// <param name="sysml">The inline SysML source to load.</param>
    /// <returns>The loaded workspace.</returns>
    private static async Task<SysmlWorkspace> LoadAsync(string sysml)
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var loadResult = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(loadResult.Workspace);
            return loadResult.Workspace;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Looks <paramref name="name"/> up and, when found, runs <c>describe</c> for it.
    /// </summary>
    /// <param name="workspace">The loaded workspace.</param>
    /// <param name="name">The name to look up.</param>
    /// <returns>The <c>describe</c> result.</returns>
    private static QueryResult Describe(SysmlWorkspace workspace, string name)
    {
        Assert.True(
            QueryEngine.TryResolveElement(workspace, name, out var element),
            $"'{name}' was not found in the loaded workspace.");

        var options = new QueryOptions { Verb = QueryVerb.Describe, Element = name };
        return QueryEngine.Execute(workspace, options, element);
    }

    /// <summary>
    ///     A name reached through a facade's public import resolves to the unit's declaration,
    ///     and the result reports the owning qualified name plus the name it was found under.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_FacadePublicImport_ReportsOwningNameAndLookupName()
    {
        // Arrange: a facade package that publicly imports a unit package
        var workspace = await LoadAsync(FacadeFixture);

        // Act: describe the unit through the facade name
        var result = Describe(workspace, "Facade::Unit");

        // Assert: the owning name is reported, with the facade name as the lookup name
        Assert.Equal("UnitPkg::Unit", result.Element);
        Assert.Equal("Facade::Unit", result.LookupName);
    }

    /// <summary>
    ///     A name reached through a chain of facades (system, subsystem, unit) resolves to the
    ///     unit's owning qualified name.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_NestedFacadeChain_ResolvesToOwningName()
    {
        // Arrange: a system facade over a subsystem facade over a unit package
        var workspace = await LoadAsync(ChainFixture);

        // Act: describe the unit through the outermost facade
        var result = Describe(workspace, "SystemPkg::Unit");

        // Assert: the owning name is reported, with the system name as the lookup name
        Assert.Equal("UnitPkg::Unit", result.Element);
        Assert.Equal("SystemPkg::Unit", result.LookupName);
    }

    /// <summary>
    ///     An alias name resolves to the aliased element's owning qualified name.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_Alias_ResolvesToAliasedElement()
    {
        // Arrange: a package with an alias for one of its definitions
        var workspace = await LoadAsync(FacadeFixture);

        // Act: describe the element through the alias name
        var result = Describe(workspace, "Aliased::Short");

        // Assert: the aliased element is reported under its owning name
        Assert.Equal("Aliased::Original", result.Element);
        Assert.Equal("Aliased::Short", result.LookupName);
    }

    /// <summary>
    ///     An alias written at the root of the file (outside any package) is a membership of the
    ///     global namespace and resolves to its target's owning qualified name.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_RootAlias_ResolvesToAliasedElement()
    {
        // Arrange: a file-scope alias for a unit declared in a package
        var workspace = await LoadAsync(FacadeFixture);

        // Act: describe the element through the file-scope alias name
        var result = Describe(workspace, "RootShort");

        // Assert: the aliased element is reported under its owning name
        Assert.Equal("UnitPkg::Unit", result.Element);
        Assert.Equal("RootShort", result.LookupName);
    }

    /// <summary>
    ///     A name two publicly imported namespaces both expose is ambiguous: it is not found, and
    ///     every candidate is reported rather than whichever import comes first.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_NameFromTwoPublicImports_IsAmbiguous()
    {
        // Arrange: a facade that publicly imports two packages that both declare Unit
        var workspace = await LoadAsync(FacadeFixture);

        // Act
        var found = QueryEngine.TryResolveElement(workspace, "Conflicting::Unit", out var element, out var candidates);

        // Assert: not found, with both competing elements reported
        Assert.False(found);
        Assert.Null(element);
        Assert.Equal(["OtherPkg::Unit", "UnitPkg::Unit"], candidates.OrderBy(name => name, StringComparer.Ordinal));
    }
    /// <summary>
    ///     A private import does not make the imported element's name resolvable through the
    ///     importing package, so the lookup fails exactly as for any unknown name.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_PrivateImport_DoesNotResolve()
    {
        // Arrange: a package that only privately imports the unit package
        var workspace = await LoadAsync(FacadeFixture);

        // Act: look the unit up through the private importer
        var found = QueryEngine.TryResolveElement(workspace, "Hidden::Unit", out var element);

        // Assert: the name is not found
        Assert.False(found);
        Assert.Null(element);
    }

    /// <summary>
    ///     A name that exists nowhere is not found, as before.
    /// </summary>
    [Fact]
    public async Task QueryEngine_TryResolveElement_UnknownName_IsNotFound()
    {
        // Arrange: any loaded workspace
        var workspace = await LoadAsync(FacadeFixture);

        // Act: look up names that resolve neither exactly nor through name resolution
        var missingMember = QueryEngine.TryResolveElement(workspace, "Facade::DoesNotExist", out var first);
        var missingPackage = QueryEngine.TryResolveElement(workspace, "NoSuchPkg::Unit", out var second);

        // Assert: neither is found
        Assert.False(missingMember);
        Assert.Null(first);
        Assert.False(missingPackage);
        Assert.Null(second);
    }

    /// <summary>
    ///     An owning qualified name is found as before and carries no lookup name, so the output
    ///     of every pre-existing query is unchanged.
    /// </summary>
    [Fact]
    public async Task QueryEngine_Execute_OwningQualifiedName_HasNoLookupName()
    {
        // Arrange: a workspace and the unit's owning qualified name
        var workspace = await LoadAsync(FacadeFixture);

        // Act: describe the element under its owning name and render both formats
        var result = Describe(workspace, "UnitPkg::Unit");
        var markdown = QueryResultRenderer.RenderMarkdown(result);
        using var json = JsonDocument.Parse(QueryResultRenderer.RenderJson(result));

        // Assert: no lookup name is reported in the result, the markdown, or the JSON
        Assert.Equal("UnitPkg::Unit", result.Element);
        Assert.Null(result.LookupName);
        Assert.DoesNotContain(markdown, line => line.Contains("Looked up as"));
        Assert.False(json.RootElement.TryGetProperty("LookupName", out _));
    }

    /// <summary>
    ///     Markdown output shows the lookup name as a summary bullet, and the heading shows the
    ///     owning qualified name.
    /// </summary>
    [Fact]
    public async Task QueryResultRenderer_RenderMarkdown_FacadeLookup_ShowsOwningNameAndLookupName()
    {
        // Arrange: a result found through a facade name
        var workspace = await LoadAsync(FacadeFixture);
        var result = Describe(workspace, "Facade::Unit");

        // Act: render as Markdown
        var markdown = QueryResultRenderer.RenderMarkdown(result);

        // Assert: heading carries the owning name; a bullet carries the lookup name
        Assert.Equal("# query describe: UnitPkg::Unit", markdown[0]);
        Assert.Contains("- Looked up as: Facade::Unit", markdown);
    }

    /// <summary>
    ///     JSON output carries the lookup name in a <c>LookupName</c> property alongside the
    ///     owning qualified name in <c>element</c>.
    /// </summary>
    [Fact]
    public async Task QueryResultRenderer_RenderJson_FacadeLookup_IncludesLookupName()
    {
        // Arrange: a result found through a facade name
        var workspace = await LoadAsync(FacadeFixture);
        var result = Describe(workspace, "Facade::Unit");

        // Act: render as JSON and parse it
        using var json = JsonDocument.Parse(QueryResultRenderer.RenderJson(result));

        // Assert: both names are present
        Assert.Equal("UnitPkg::Unit", json.RootElement.GetProperty("Element").GetString());
        Assert.Equal("Facade::Unit", json.RootElement.GetProperty("LookupName").GetString());
    }
}
