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
    ///     A private member of a direct general type is not inherited, so it cannot be a redefined
    ///     feature; a protected member still is (KerML 8.2.3.5.1 with 7.2.5.3 visibility).
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveRedefinedFeature_PrivateMemberOfDirectGeneral_DoesNotResolve()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def Base { private attribute secret; protected attribute guarded; attribute open; }
                part def D :> Base { attribute :>> secret; attribute :>> guarded; attribute :>> open; }
            }
            """);

        // Act / Assert
        Assert.Null(resolver.ResolveRedefinedFeature("secret", "P::D"));
        Assert.Equal("P::Base::guarded", resolver.ResolveRedefinedFeature("guarded", "P::D"));
        Assert.Equal("P::Base::open", resolver.ResolveRedefinedFeature("open", "P::D"));
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
    ///     In resolving mode general types come only from the raw references: a (wrong) resolved
    ///     edge is ignored, and stripping every edge changes nothing.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolvingMode_GeneralTypes_IgnoresResolvedEdges()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Base { part def Inner; }
                part def Wrong { part def Inner; }
                part def Derived :> Base;
            }
            """);
        var derived = workspace.Declarations["P::Derived"];
        derived.ResolvedEdges = [new SysmlEdge("P::Derived", "P::Wrong", SysmlEdgeKind.Supertype)];

        // Act
        var publicMode = new NameResolver(workspace.Declarations);
        var resolvingMode = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Assert: the public mode trusts the edge, the resolving mode the raw reference
        Assert.Equal("P::Wrong::Inner", publicMode.Resolve("Inner", "P::Derived"));
        Assert.Equal("P::Base::Inner", resolvingMode.Resolve("Inner", "P::Derived"));
        Assert.Equal("P::Base", Assert.Single(resolvingMode.GeneralTypes("P::Derived")));
    }

    /// <summary>
    ///     A resolving-mode answer does not depend on the order lookups are made in, even when the
    ///     inherited member's supertype is declared after the type that uses it.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolvingMode_Resolve_SupertypeDeclaredLater_IsOrderIndependent()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Derived :> Base;
                part def Base { part def Inner; }
            }
            """);
        foreach (var node in workspace.Declarations.Values)
        {
            node.ResolvedEdges = [];
        }

        // Act
        var forward = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);
        var backward = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);
        var first = forward.Resolve("Inner", "P::Derived");
        _ = backward.Resolve("Base", "P");
        var second = backward.Resolve("Inner", "P::Derived");

        // Assert
        Assert.Equal("P::Base::Inner", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Specialization cycles terminate in resolving mode.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolvingMode_GeneralTypes_SpecializationCycle_Terminates()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def A :> B;
                part def B :> A;
            }
            """);
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act / Assert
        Assert.Equal("P::B", Assert.Single(resolver.GeneralTypes("P::A")));
        Assert.Null(resolver.Resolve("Missing", "P::A"));
    }

    /// <summary>
    ///     Imports written at the root of a file apply to that file's top-level declarations.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolvingMode_Resolve_FileRootImport_AppliesToRegisteredDeclarations()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package Lib { part def Y; }
            package P { part def A; }
            package Other { part def B; }
            """);
        var rootImport = new SysmlImportNode { ImportedNamespace = "Lib", IsWildcard = true };
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);
        resolver.RegisterRootImports(["P", "P::A"], [rootImport]);

        // Act / Assert: only P's declarations (registered ones) see the import
        Assert.Equal("Lib::Y", resolver.Resolve("Y", "P::A"));
        Assert.Equal("Lib::Y", resolver.Resolve("Y", null, [rootImport]));
        Assert.Null(resolver.Resolve("Y", "Other::B"));
    }

    /// <summary>
    ///     The file-root imports a caller supplies for the referencing file take precedence over
    ///     those registered against a declaration of the same name from another file.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolvingMode_Resolve_SuppliedFileRootImports_OverrideRegisteredByName()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package LibA { part def T; }
            package LibB { part def T; }
            package P { part def A; }
            """);
        var importA = new SysmlImportNode { ImportedNamespace = "LibA", IsWildcard = true };
        var importB = new SysmlImportNode { ImportedNamespace = "LibB", IsWildcard = true };
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);
        resolver.RegisterRootImports(["P"], [importA]);

        // Act / Assert: the referencing file's own imports decide, not the by-name registration
        Assert.Equal("LibB::T", resolver.Resolve("T", "P", [importB]));
        Assert.Equal("LibB::T", resolver.Resolve("T", "P::A", [importB]));
        Assert.Equal("LibA::T", resolver.Resolve("T", "P"));
    }

    /// <summary>
    ///     A redefinition never resolves to the redefining feature itself, and falls back to
    ///     ordinary resolution for a qualified target.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveRedefinition_ExcludesRedefiningFeatureAndFallsBack()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Base { attribute mass; }
                part def D :> Base { attribute :>> mass; attribute :>> P::Base::mass; }
                part def Alone { attribute :>> nothing; }
            }
            """);
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act / Assert
        Assert.Equal("P::Base::mass", resolver.ResolveRedefinition("mass", "P::D", "P::D::mass", null));
        Assert.Equal("P::Base::mass", resolver.ResolveRedefinition("P::Base::mass", "P::Alone", "P::Alone::x", null));
        Assert.Null(resolver.ResolveRedefinition("nothing", "P::Alone", "P::Alone::nothing", null));
    }

    /// <summary>
    ///     Loads <paramref name="sysml"/> (with the standard library) and returns the workspace.
    /// </summary>
    private static async Task<SysmlWorkspace> LoadWorkspaceAsync(string sysml)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return result.Workspace;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
