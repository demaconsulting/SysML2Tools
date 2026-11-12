// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests for <see cref="NameResolver"/> (KerML 8.2.3.5 name resolution).
/// </summary>
public sealed class NameResolverTests
{
    /// <summary>
    ///     Loads <paramref name="sysml"/> (with the standard library) and returns a resolver over it.
    /// </summary>
    private static async Task<NameResolver> LoadAsync(
        string sysml, Func<SysmlImportNode, string, bool>? importFilter = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return new NameResolver(result.Workspace.Declarations, importFilter);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A simple name resolves in the owning package before a same-named element in another package.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_SameNameInSiblingPackages_ResolvesOwningPackageFirst()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package PkgA { part def Checker; part def SystemA; }
            package PkgB { part def Checker; part def SystemB; }
            """);

        // Act / Assert
        Assert.Equal("PkgA::Checker", resolver.Resolve("Checker", "PkgA::SystemA"));
        Assert.Equal("PkgB::Checker", resolver.Resolve("Checker", "PkgB::SystemB"));
    }

    /// <summary>
    ///     A name declared in a supertype resolves through the subtype's inherited memberships.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_InheritedMember_ResolvesThroughSupertype()
    {
        // Arrange: Inner is owned by Base and inherited by Derived; Other declares its own Inner
        var resolver = await LoadAsync("""
            package Other { part def Inner; }
            package P {
                part def Base { part def Inner; }
                part def Derived :> Base;
            }
            """);

        // Act / Assert
        Assert.Equal("P::Base::Inner", resolver.Resolve("Inner", "P::Derived"));
        Assert.Equal("P::Base::Inner", resolver.Resolve("P::Derived::Inner", null));
    }

    /// <summary>
    ///     Aliases and short names are local resolutions of their memberships.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_AliasAndShortName_ResolveToTheirElement()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package Q {
                part def <k> Kind;
                alias KindAlias for Kind;
            }
            """);

        // Act / Assert
        Assert.Equal("Q::Kind", resolver.Resolve("k", "Q"));
        Assert.Equal("Q::Kind", resolver.Resolve("KindAlias", "Q"));
        Assert.Equal("Q::Kind", resolver.Resolve("Q::KindAlias", null));
        Assert.Equal("Q::Kind", resolver.Resolve("Q::k", null));
    }

    /// <summary>
    ///     A private member resolves inside its namespace but not through a qualified name from outside.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_PrivateMemberViaQualifiedName_DoesNotResolve()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P { private part def Hidden; part def Shown; }
            package Other { part def User; }
            """);

        // Act / Assert
        Assert.Equal("P::Hidden", resolver.Resolve("Hidden", "P"));
        Assert.Null(resolver.Resolve("P::Hidden", "Other::User"));
        Assert.Equal("P::Shown", resolver.Resolve("P::Shown", "Other::User"));
    }

    /// <summary>
    ///     Imported members resolve locally; a private import is not visible through the importer.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_ImportedMember_HonorsImportVisibility()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package A { part def X; }
            package B { private import A::*; part def Y; }
            package C { public import A::*; }
            """);

        // Act / Assert
        Assert.Equal("A::X", resolver.Resolve("X", "B::Y"));
        Assert.Null(resolver.Resolve("B::X", null));
        Assert.Equal("A::X", resolver.Resolve("C::X", null));
    }

    /// <summary>
    ///     A redefined feature resolves against the general types of the redefining feature's owner.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveRedefinedFeature_ResolvesAgainstGeneralTypes()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def Motor { attribute maxThrust; }
                part def RacingMotor :> Motor { attribute :>> maxThrust; }
            }
            """);

        // Act / Assert
        Assert.Equal("P::Motor::maxThrust", resolver.ResolveRedefinedFeature("maxThrust", "P::RacingMotor"));
    }

    /// <summary>
    ///     A name that does not resolve by KerML rules is matched only by the flagged simple-name
    ///     fallback, which lists every candidate when the match is ambiguous.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveWithFallback_AmbiguousSimpleName_IsFlaggedWithCandidates()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package PkgA { part def Checker; }
            package PkgB { part def Checker; }
            package Other { part def User; }
            """);

        // Act
        var resolution = resolver.ResolveWithFallback("Checker", "Other::User");

        // Assert
        Assert.True(resolution.IsSimpleNameFallback);
        Assert.NotNull(resolution.QualifiedName);
        Assert.Contains("PkgA::Checker", resolution.Candidates);
        Assert.Contains("PkgB::Checker", resolution.Candidates);
        Assert.False(resolver.ResolveWithFallback("Checker", "PkgA").IsSimpleNameFallback);
    }

    /// <summary>
    ///     A filtered recursive import whose first same-named nested candidate fails the filter
    ///     keeps searching and resolves a later candidate that satisfies it.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_FilteredRecursiveImport_SkipsRejectedCandidate()
    {
        // Arrange: the filter rejects P::A::Engine but admits P::B::Engine
        var resolver = await LoadAsync(
            """
            package P { package A { part def Engine; } package B { part def Engine; } }
            package Q { private import P::**[@Safety]; part def User; }
            """,
            (_, candidate) => candidate != "P::A::Engine");

        // Act / Assert
        Assert.Equal("P::B::Engine", resolver.Resolve("Engine", "Q::User"));
    }

    /// <summary>
    ///     A type's general types merge its resolved supertype edges with raw resolution of any
    ///     supertype reference that has no resolved edge.
    /// </summary>
    [Fact]
    public void NameResolver_GeneralTypes_PartiallyResolvedSupertypes_IncludesEveryGeneralType()
    {
        // Arrange: Derived :> A, B with only the A edge resolved
        var derived = new SysmlDefinitionNode
        {
            Name = "Derived",
            QualifiedName = "P::Derived",
            DefinitionKeyword = "part def",
            SupertypeNames = ["A", "B"],
            ResolvedEdges = [new SysmlEdge("P::Derived", "P::A", SysmlEdgeKind.Supertype)],
        };
        var declarations = new Dictionary<string, SysmlNode>
        {
            ["P"] = new SysmlPackageNode { Name = "P", QualifiedName = "P" },
            ["P::A"] = new SysmlDefinitionNode { Name = "A", QualifiedName = "P::A", DefinitionKeyword = "part def" },
            ["P::B"] = new SysmlDefinitionNode { Name = "B", QualifiedName = "P::B", DefinitionKeyword = "part def" },
            ["P::Derived"] = derived,
        };

        // Act
        var generals = new NameResolver(declarations).GeneralTypes("P::Derived");

        // Assert
        Assert.Equal(["P::A", "P::B"], generals);
    }

    /// <summary>
    ///     A membership import of an alias brings in the alias's short name as well as its name,
    ///     both resolving to the aliased element.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_MembershipImportedAlias_ResolvesItsShortName()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package A { part def Thing; alias <s> Al for Thing; }
            package Q { private import A::Al; part def User; }
            """);

        // Act / Assert
        Assert.Equal("A::Thing", resolver.Resolve("s", "Q::User"));
        Assert.Equal("A::Thing", resolver.Resolve("Al", "Q::User"));
    }

    /// <summary>
    ///     Members of KerML type, action, state, and requirement bodies carry their declared
    ///     visibility and short name: each resolves by its short name inside its owner, and a
    ///     private one does not resolve through a qualified name from outside.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_SpecializedBodyMembers_HonorVisibilityAndShortName()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                class Cls { private class <c1> Nested; }
                action def Act { private action <a1> step1; }
                state def St { private state <s1> idle; }
                requirement def Req { private attribute <r1> limit; }
            }
            """);

        // Act / Assert
        foreach (var (owner, shortName, member) in new[]
                 {
                     ("P::Cls", "c1", "Nested"), ("P::Act", "a1", "step1"),
                     ("P::St", "s1", "idle"), ("P::Req", "r1", "limit"),
                 })
        {
            Assert.Equal($"{owner}::{member}", resolver.Resolve(shortName, owner));
            Assert.Null(resolver.Resolve($"{owner}::{member}", "P"));
        }
    }

    /// <summary>
    ///     Re-checking a semantic target keeps it when KerML resolution agrees or finds nothing,
    ///     and replaces it when KerML resolution, using only the referencing namespace's imports,
    ///     finds a different element.
    /// </summary>
    [Fact]
    public async Task NameResolver_Recheck_DifferentKerMLResolution_ReplacesSemanticTarget()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package A { part def X; }
            package B { part def X; }
            package PkgB { private import B::*; part def User; }
            """);

        // Act / Assert
        Assert.Equal("B::X", resolver.Recheck("A::X", "X", "PkgB::User"));
        Assert.Equal("B::X", resolver.Recheck("B::X", "X", "PkgB::User"));
        Assert.Equal("A::X", resolver.Recheck("A::X", "Missing", "PkgB::User"));
    }
}
