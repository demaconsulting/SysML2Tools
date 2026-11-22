// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Filtering;
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
    ///     Loads <paramref name="sysml"/> (with the standard library) and returns the resolver the
    ///     workspace offers: filtered imports evaluated and file-root aliases registered.
    /// </summary>
    private static async Task<NameResolver> LoadWorkspaceResolverAsync(string sysml)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return FilterExpressionEvaluator.CreateNameResolver(result.Workspace);
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
    ///     Two-candidate ambiguity on every resolution path of <see cref="NameResolver.ResolveCandidates"/>:
    ///     each row is a model, a reference, and the two elements the reference denotes.
    /// </summary>
    public static TheoryData<string, string, string> AmbiguousPaths => new()
    {
        // Owned memberships: a name and a short name; two aliases of one name
        { "package P { part def <zz> A; part def zz; }", "P::zz", "P::A|P::zz" },
        { "package P { part def A; part def B; alias X for A; alias X for B; }", "P::X", "P::A|P::B" },

        // An alias (and a membership import) whose own target is ambiguous
        {
            """
            package A { part def X; } package B { part def X; }
            package Hub { public import A::*; public import B::*; }
            package P { alias Y for Hub::X; }
            """,
            "P::Y", "A::X|B::X"
        },
        {
            """
            package A { part def X; } package B { part def X; }
            package Hub { public import A::*; public import B::*; }
            package F { public import Hub::X; }
            """,
            "F::X", "A::X|B::X"
        },

        // Imported memberships: membership imports, namespace imports, recursive imports
        {
            "package A { part def X; } package B { part def X; } package Hub { public import A::X; public import B::X; }",
            "Hub::X", "A::X|B::X"
        },
        {
            "package A { part def X; } package B { part def X; } package Hub { public import A::*; public import B::*; }",
            "Hub::X", "A::X|B::X"
        },
        {
            "package Lib { package A { part def X; } package B { part def X; } } package Hub { public import Lib::**; }",
            "Hub::X", "Lib::A::X|Lib::B::X"
        },
        {
            "package Lib { part def X; package Inner { part def X; } } package Hub { public import Lib::**; }",
            "Hub::X", "Lib::Inner::X|Lib::X"
        },

        // Filtered imports: each candidate must pass the filter (an element without @T is rejected)
        {
            """
            metadata def T;
            package A { part def X { @T; } } package B { part def X { @T; } } package Plain { part def X; }
            package Hub { public import A::*[@T]; public import B::*[@T]; public import Plain::*[@T]; }
            """,
            "Hub::X", "A::X|B::X"
        },
        {
            """
            metadata def T;
            package Lib { package A { part def X { @T; } } package B { part def X { @T; } } package Plain { part def X; } }
            package Hub { public import Lib::**[@T]; }
            """,
            "Hub::X", "Lib::A::X|Lib::B::X"
        },

        // Inherited memberships of two general types
        {
            "package P { part def S1 { part def X; } part def S2 { part def X; } part def D :> S1, S2; }",
            "P::D::X", "P::S1::X|P::S2::X"
        },

        // The global namespace: short names, a name and a short name, root aliases, the $:: form
        { "part def <zz> A; part def <zz> B;", "zz", "A|B" },
        { "part def <zz> A; part def zz;", "zz", "A|zz" },
        { "part def <zz> A; part def <zz> B;", "$::zz", "A|B" },
        {
            "package P { part def X; } package Q { part def X; } alias <zz> One for P::X; alias <zz> Two for Q::X;",
            "zz", "P::X|Q::X"
        },
        {
            "package P { part def X; } package Q { part def X; } alias <zz> One for P; alias <zz> Two for Q;",
            "zz::X", "P::X|Q::X"
        },
    };

    /// <summary>
    ///     Every resolution path reports both elements of a two-candidate ambiguity, while model
    ///     resolution (<see cref="NameResolver.Resolve(string, string?)"/>) is unchanged and still
    ///     yields one of them.
    /// </summary>
    /// <param name="sysml">The model.</param>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="expected">The two expected candidates, separated by <c>|</c>.</param>
    [Theory]
    [MemberData(nameof(AmbiguousPaths))]
    public async Task NameResolver_ResolveCandidates_TwoCandidatesOnEveryPath_ReportsBoth(
        string sysml, string reference, string expected)
    {
        // Arrange: a model in which the reference denotes two elements, resolved the way a query
        // resolves it (import filters evaluated, file-root aliases registered)
        var resolver = await LoadWorkspaceResolverAsync(sysml);
        var expectedCandidates = expected.Split('|').OrderBy(name => name, StringComparer.Ordinal).ToList();

        // Act
        var candidates = resolver.ResolveCandidates(reference);

        // Assert: both candidates; the model resolution takes one of them
        Assert.Equal(expectedCandidates, candidates.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Contains(resolver.Resolve(reference, null), expectedCandidates);
    }

    /// <summary>
    ///     An unambiguous name has one candidate, the same element reached by two imports has one,
    ///     and an unknown name has none.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveCandidates_UnambiguousAndUnknownNames_ReportOneAndNone()
    {
        // Arrange: Same imports the same package twice
        var resolver = await LoadAsync("""
            package A { part def X; part def OnlyA; }
            package B { part def X; }
            package Facade { public import A::*; public import B::*; }
            package Same { public import A::*; public import A::OnlyA; }
            """);

        // Act / Assert
        Assert.Equal(["A::OnlyA"], resolver.ResolveCandidates("Facade::OnlyA"));
        Assert.Equal(["A::OnlyA"], resolver.ResolveCandidates("Same::OnlyA"));
        Assert.Empty(resolver.ResolveCandidates("Facade::Missing"));
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
    ///     Every definition and usage kind implicitly specializes its library type (KerML 7.3.4 /
    ///     SysML 7.x default general types): the library type is a lookup general type of the
    ///     element, but never a declared one.
    /// </summary>
    /// <param name="body">The package body declaring the element <c>P::X</c>.</param>
    /// <param name="element">The element's qualified name.</param>
    /// <param name="expected">The library qualified name the element implicitly specializes.</param>
    [Theory]
    [InlineData("part def X;", "P::X", "Parts::Part")]
    [InlineData("item def X;", "P::X", "Items::Item")]
    [InlineData("port def X;", "P::X", "Ports::Port")]
    [InlineData("attribute def X;", "P::X", "Base::DataValue")]
    [InlineData("enum def X { enum A; }", "P::X", "Base::DataValue")]
    [InlineData("action def X;", "P::X", "Actions::Action")]
    [InlineData("state def X;", "P::X", "States::StateAction")]
    [InlineData("calc def X;", "P::X", "Calculations::Calculation")]
    [InlineData("constraint def X;", "P::X", "Constraints::ConstraintCheck")]
    [InlineData("requirement def X;", "P::X", "Requirements::RequirementCheck")]
    [InlineData("concern def X;", "P::X", "Requirements::ConcernCheck")]
    [InlineData("case def X;", "P::X", "Cases::Case")]
    [InlineData("analysis def X;", "P::X", "AnalysisCases::AnalysisCase")]
    [InlineData("verification def X;", "P::X", "VerificationCases::VerificationCase")]
    [InlineData("use case def X;", "P::X", "UseCases::UseCase")]
    [InlineData("connection def X;", "P::X", "Connections::Connection")]
    [InlineData("interface def X;", "P::X", "Interfaces::Interface")]
    [InlineData("allocation def X;", "P::X", "Allocations::Allocation")]
    [InlineData("flow def X;", "P::X", "Flows::MessageAction")]
    [InlineData("occurrence def X;", "P::X", "Occurrences::Occurrence")]
    [InlineData("individual def X;", "P::X", "Occurrences::Occurrence")]
    [InlineData("metadata def X;", "P::X", "Metadata::MetadataItem")]
    [InlineData("rendering def X;", "P::X", "Views::Rendering")]
    [InlineData("view def X;", "P::X", "Views::View")]
    [InlineData("view X;", "P::X", "Views::View")]
    [InlineData("viewpoint def X;", "P::X", "Views::ViewpointCheck")]
    [InlineData("class X;", "P::X", "Occurrences::Occurrence")]
    [InlineData("struct X;", "P::X", "Objects::Object")]
    [InlineData("assoc struct X;", "P::X", "Objects::LinkObject")]
    [InlineData("assoc X;", "P::X", "Links::Link")]
    [InlineData("datatype X;", "P::X", "Base::DataValue")]
    [InlineData("classifier X;", "P::X", "Base::Anything")]
    [InlineData("type X;", "P::X", "Base::Anything")]
    [InlineData("metaclass X;", "P::X", "Metaobjects::Metaobject")]
    [InlineData("behavior X;", "P::X", "Performances::Performance")]
    [InlineData("function X;", "P::X", "Performances::Evaluation")]
    [InlineData("predicate X;", "P::X", "Performances::BooleanEvaluation")]
    [InlineData("interaction X;", "P::X", "Links::Link")]
    [InlineData("interaction X;", "P::X", "Performances::Performance")]
    [InlineData("part X;", "P::X", "Parts::parts")]
    [InlineData("requirement def R { actor X; }", "P::R::X", "Parts::parts")]
    [InlineData("requirement def R { stakeholder X; }", "P::R::X", "Parts::parts")]
    [InlineData("port X;", "P::X", "Ports::ports")]
    [InlineData("item X;", "P::X", "Items::items")]
    [InlineData("attribute X;", "P::X", "Base::dataValues")]
    [InlineData("enum def E { enum X; }", "P::E::X", "Base::dataValues")]
    [InlineData("action X;", "P::X", "Actions::actions")]
    [InlineData("state def S { entry action X; }", "P::S::X", "Actions::actions")]
    [InlineData("state def S { do action X; }", "P::S::X", "Actions::actions")]
    [InlineData("state def S { exit action X; }", "P::S::X", "Actions::actions")]
    [InlineData("action def A { merge X; }", "P::A::X", "Actions::actions")]
    [InlineData("action def A { decide X; }", "P::A::X", "Actions::actions")]
    [InlineData("action def A { join X; }", "P::A::X", "Actions::actions")]
    [InlineData("action def A { fork X; }", "P::A::X", "Actions::actions")]
    [InlineData("action def A { action X accept ScalarValues::Boolean; }", "P::A::X", "Actions::acceptActions")]
    [InlineData("action def A { attribute p; action X send p; }", "P::A::X", "Actions::sendActions")]
    [InlineData("action def A { action X terminate; }", "P::A::X", "Actions::terminateActions")]
    [InlineData("state X;", "P::X", "States::stateActions")]
    [InlineData("calc X;", "P::X", "Calculations::calculations")]
    [InlineData("constraint X;", "P::X", "Constraints::constraintChecks")]
    [InlineData("requirement def R { assume constraint X; }", "P::R::X", "Constraints::constraintChecks")]
    [InlineData("requirement def R { require constraint X; }", "P::R::X", "Constraints::constraintChecks")]
    [InlineData("requirement X;", "P::X", "Requirements::requirementChecks")]
    [InlineData("concern X;", "P::X", "Requirements::concernChecks")]
    [InlineData("case X;", "P::X", "Cases::cases")]
    [InlineData("analysis X;", "P::X", "AnalysisCases::analysisCases")]
    [InlineData("verification X;", "P::X", "VerificationCases::verificationCases")]
    [InlineData("use case X;", "P::X", "UseCases::useCases")]
    [InlineData("rendering X;", "P::X", "Views::renderings")]
    [InlineData("occurrence X;", "P::X", "Occurrences::occurrences")]
    [InlineData("part def O { event occurrence X; }", "P::O::X", "Occurrences::occurrences")]
    [InlineData("ref X;", "P::X", "Base::things")]
    [InlineData("feature X;", "P::X", "Base::things")]
    [InlineData("step X;", "P::X", "Performances::performances")]
    [InlineData("expr X;", "P::X", "Performances::evaluations")]
    [InlineData("bool X;", "P::X", "Performances::booleanEvaluations")]
    [InlineData("inv X;", "P::X", "Performances::trueEvaluations")]
    [InlineData("connector X;", "P::X", "Links::links")]
    [InlineData("feature a; feature b; binding X of a = b;", "P::X", "Links::selfLinks")]
    [InlineData("class C { feature a; feature b; succession X first a then b; }", "P::C::X", "Occurrences::happensBeforeLinks")]
    [InlineData("class C { feature a; feature b; flow X from a to b; }", "P::C::X", "Transfers::flowTransfers")]
    [InlineData("class C { feature a; feature b; succession flow X from a to b; }", "P::C::X", "Transfers::flowTransfersBefore")]
    [InlineData("part a; part b; connection X connect a to b;", "P::X", "Connections::connections")]
    [InlineData("part a; part b; interface X connect a to b;", "P::X", "Interfaces::interfaces")]
    [InlineData("part a; part b; allocation X allocate a to b;", "P::X", "Allocations::allocations")]
    [InlineData("part a; part b; message X from a to b;", "P::X", "Flows::messages")]
    [InlineData("part a; part b; flow X from a to b;", "P::X", "Flows::flows")]
    [InlineData("part a; part b; binding X bind a = b;", "P::X", "Links::selfLinks")]
    public async Task NameResolver_ImplicitSpecialization_PerKind_AddsLibraryGeneralType(
        string body, string element, string expected)
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync($"package P {{ {body} }}");
        var resolver = new NameResolver(workspace.Declarations);

        // Act
        var lookupGeneral = resolver.LookupGeneralTypes(element);

        // Assert: a lookup general type, never a declared one
        Assert.Contains(expected, lookupGeneral);
        Assert.DoesNotContain(expected, resolver.GeneralTypes(element));
    }

    /// <summary>
    ///     A member declared only on (or inherited by) an element's implicit library general type
    ///     resolves in the element's own scope with no explicit specialization written.
    /// </summary>
    /// <param name="body">The package body declaring the element <c>P::X</c>.</param>
    /// <param name="element">The element's qualified name.</param>
    /// <param name="member">The bare member name to resolve in the element's scope.</param>
    /// <param name="expected">The expected library member qualified name.</param>
    [Theory]
    [InlineData("part def X;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("part def X;", "P::X", "shape", "Items::Item::shape")]
    [InlineData("part def X;", "P::X", "localClock", "Occurrences::Occurrence::localClock")]
    [InlineData("item def X;", "P::X", "shape", "Items::Item::shape")]
    [InlineData("port def X;", "P::X", "interfacingPorts", "Ports::Port::interfacingPorts")]
    [InlineData("attribute def X;", "P::X", "self", "Base::DataValue::self")]
    [InlineData("action def X;", "P::X", "decisions", "Actions::Action::decisions")]
    [InlineData("action def X;", "P::X", "start", "Actions::Action::start")]
    [InlineData("state def X;", "P::X", "exclusiveStates", "States::StateAction::exclusiveStates")]
    [InlineData("state def X;", "P::X", "done", "States::StateAction::done")]
    [InlineData("calc def X;", "P::X", "result", "Performances::Evaluation::result")]
    [InlineData("constraint def X;", "P::X", "result", "Performances::Evaluation::result")]
    [InlineData("requirement def X;", "P::X", "assumptions", "Requirements::RequirementCheck::assumptions")]
    [InlineData("concern def X;", "P::X", "stakeholders", "Requirements::RequirementCheck::stakeholders")]
    [InlineData("case def X;", "P::X", "result", "Cases::Case::result")]
    [InlineData("analysis def X;", "P::X", "result", "Cases::Case::result")]
    [InlineData("verification def X;", "P::X", "verdict", "VerificationCases::VerificationCase::verdict")]
    [InlineData("use case def X;", "P::X", "result", "Cases::Case::result")]
    [InlineData("connection def X;", "P::X", "participant", "Links::Link::participant")]
    [InlineData("interface def X;", "P::X", "participant", "Links::Link::participant")]
    [InlineData("flow def X;", "P::X", "payload", "Flows::MessageAction::payload")]
    [InlineData("occurrence def X;", "P::X", "successors", "Occurrences::Occurrence::successors")]
    [InlineData("metadata def X;", "P::X", "annotatedElement", "Metaobjects::Metaobject::annotatedElement")]
    [InlineData("rendering def X;", "P::X", "self", "Views::Rendering::self")]
    [InlineData("view def X;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("viewpoint def X;", "P::X", "assumptions", "Requirements::RequirementCheck::assumptions")]
    [InlineData("class X;", "P::X", "successors", "Occurrences::Occurrence::successors")]
    [InlineData("struct X;", "P::X", "ownedPerformances", "Objects::Object::ownedPerformances")]
    [InlineData("assoc X;", "P::X", "participant", "Links::Link::participant")]
    [InlineData("behavior X;", "P::X", "performers", "Performances::Performance::performers")]
    [InlineData("function X;", "P::X", "result", "Performances::Evaluation::result")]
    [InlineData("part X;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("port X;", "P::X", "interfacingPorts", "Ports::Port::interfacingPorts")]
    [InlineData("item X;", "P::X", "shape", "Items::Item::shape")]
    [InlineData("attribute X;", "P::X", "self", "Base::Anything::self")]
    [InlineData("action X;", "P::X", "decisions", "Actions::Action::decisions")]
    [InlineData("state X;", "P::X", "exclusiveStates", "States::StateAction::exclusiveStates")]
    [InlineData("calc X;", "P::X", "result", "Performances::Evaluation::result")]
    [InlineData("requirement X;", "P::X", "assumptions", "Requirements::RequirementCheck::assumptions")]
    [InlineData("occurrence X;", "P::X", "successors", "Occurrences::Occurrence::successors")]
    [InlineData("rendering X;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("class C { feature a; feature b; flow X from a to b; }", "P::C::X", "payload", "Transfers::Transfer::payload")]
    [InlineData("class C { feature a; feature b; succession flow X from a to b; }", "P::C::X", "payload", "Transfers::Transfer::payload")]
    [InlineData("part a; part b; connection X connect a to b;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("part a; part b; interface X connect a to b;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("part a; part b; allocation X allocate a to b;", "P::X", "ownedPorts", "Parts::Part::ownedPorts")]
    [InlineData("part a; part b; binding X bind a = b;", "P::X", "participant", "Links::Link::participant")]
    [InlineData("part a; part b; message X from a to b;", "P::X", "payload", "Transfers::Transfer::payload")]
    [InlineData("part a; part b; flow X from a to b;", "P::X", "payload", "Transfers::Transfer::payload")]
    public async Task NameResolver_ImplicitSpecialization_PerKind_MemberResolves(
        string body, string element, string member, string expected)
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync($"package P {{ {body} }}");
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.Equal(expected, resolver.Resolve(member, element));
    }

    /// <summary>
    ///     Every library type or feature the implicit-specialization table names is a symbol of the
    ///     embedded standard library, so a library change cannot silently disable a row.
    /// </summary>
    [Fact]
    public void NameResolver_ImplicitSpecialization_TargetsExistInVendoredStdlib()
    {
        // Arrange
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();

        // Act
        var targets = NameResolver.ImplicitSpecializationTargets;

        // Assert
        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.True(
            stdlibTable.Symbols.ContainsKey(target),
            $"Implicit-specialization target '{target}' is not a standard library symbol."));
    }

    /// <summary>
    ///     Without a standard library in the workspace the implicit general types are absent, so
    ///     lookup behaves exactly as it does for declared specializations only.
    /// </summary>
    [Fact]
    public async Task NameResolver_ImplicitSpecialization_NoStdlibSeed_NoEffect()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, "package P { part def X; part x; }", TestContext.Current.CancellationToken);
            var result = await WorkspaceLoader.LoadAsync([tempFile], null);
            var resolver = new NameResolver(result.Workspace!.Declarations);

            // Act / Assert
            Assert.Empty(resolver.LookupGeneralTypes("P::X"));
            Assert.Empty(resolver.LookupGeneralTypes("P::x"));
            Assert.Null(resolver.Resolve("ownedPorts", "P::X"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A library type never implicitly specializes itself, nor does a library root (the
    ///     implicit chain ends at <c>Base::Anything</c> / <c>Base::things</c>).
    /// </summary>
    /// <param name="library">The library type's qualified name.</param>
    /// <param name="forbidden">A qualified name that must not appear among its lookup general types.</param>
    [Theory]
    [InlineData("Parts::Part", "Parts::Part")]
    [InlineData("Base::Anything", "Base::Anything")]
    [InlineData("Base::things", "Base::things")]
    [InlineData("Parts::parts", "Parts::parts")]
    public async Task NameResolver_ImplicitSpecialization_LibraryTypeDoesNotSpecializeItself(string library, string forbidden)
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("package P { part def X; }");
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.DoesNotContain(forbidden, resolver.LookupGeneralTypes(library));
    }

    /// <summary>
    ///     An explicit supertype wins a name conflict over an implicit one: the implicit general
    ///     types are consulted after the declared ones.
    /// </summary>
    [Fact]
    public async Task NameResolver_ImplicitSpecialization_ExplicitSupertypeWinsOverImplicit()
    {
        // Arrange: Base declares its own 'shape', Items::Item::shape is the implicit one
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Base { attribute shape; }
                part def D :> Base;
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.Equal("P::Base::shape", resolver.Resolve("shape", "P::D"));
        Assert.Equal("P::Base", resolver.LookupGeneralTypes("P::D")[0]);
        Assert.Contains("Parts::Part", resolver.LookupGeneralTypes("P::D"));
    }

    /// <summary>
    ///     Inherited lookup is transitive through explicit and implicit general types: a member of
    ///     an explicit grandparent, and a member of a library root reached only implicitly.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_Transitive_ThroughExplicitAndImplicitGenerals()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def A { attribute fromA; }
                part def B :> A;
                part def C :> B;
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert: explicit chain, then the implicit Part -> Item -> Object -> Occurrence chain
        Assert.Equal("P::A::fromA", resolver.Resolve("fromA", "P::C"));
        Assert.Equal("Items::Item::shape", resolver.Resolve("shape", "P::C"));
        Assert.Equal("Objects::Object::ownedPerformances", resolver.Resolve("ownedPerformances", "P::C"));
        Assert.Equal("Occurrences::Occurrence::localClock", resolver.Resolve("localClock", "P::C"));
    }

    /// <summary>
    ///     A protected member of a general type is visible to the specialization, a private one is
    ///     not, and a qualified name reaches public members only — including those inherited
    ///     implicitly from the library.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_ProtectedMemberVisibleToSpecialization_PrivateNot()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Base {
                    private attribute secret;
                    protected attribute guarded;
                    public attribute open;
                }
                part def D :> Base;
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert: bare names (local resolution) see protected and public, not private
        Assert.Equal("P::Base::guarded", resolver.Resolve("guarded", "P::D"));
        Assert.Equal("P::Base::open", resolver.Resolve("open", "P::D"));
        Assert.Null(resolver.Resolve("secret", "P::D"));

        // Assert: qualified names are visible resolution (public only), also through implicit generals
        Assert.Null(resolver.Resolve("P::D::guarded", null));
        Assert.Equal("P::Base::open", resolver.Resolve("P::D::open", null));
        Assert.Equal("Parts::Part::ownedPorts", resolver.Resolve("P::D::ownedPorts", null));
    }

    /// <summary>
    ///     A feature owned by the specialization that redefines an inherited member hides it from
    ///     inherited lookup (KerML <c>Type::removeRedefinedFeatures</c>): the name falls through
    ///     to the enclosing namespace instead of reaching the redefined member.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_RedefinitionHidesRedefinedMember()
    {
        // Arrange: Q redefines Items::Item::shape under another name; the package has its own 'shape'
        var workspace = await LoadWorkspaceAsync("""
            package P {
                attribute shape;
                item def Q { attribute myShape redefines shape; }
                item def Plain;
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.Equal("Items::Item::shape", resolver.ResolveRedefinedFeature("shape", "P::Q"));
        Assert.Equal("P::shape", resolver.Resolve("shape", "P::Q"));
        Assert.Equal("Items::Item::shape", resolver.Resolve("shape", "P::Plain"));
    }

    /// <summary>
    ///     A redefinition that keeps the redefined name resolves to the owned feature, not the
    ///     redefined one.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_RedefinitionWithSameName_ResolvesOwned()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                item def Q { attribute :>> shape; }
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.Equal("P::Q::shape", resolver.Resolve("shape", "P::Q"));
    }

    /// <summary>
    ///     An unnamed redefinition of a member of a sibling or enclosing feature resolves outward
    ///     from the redefining feature's owner, since the redefining feature takes the redefined
    ///     name as its own.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveRedefinition_UnnamedRedefinitionOfEnclosingMember_Resolves()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def A { part part1; }
                part a : A { part part2 { part :>> part1; } }
            }
            """);
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act
        var resolved = resolver.ResolveRedefinition("part1", "P::a::part2", "P::a::part2::part1", null);

        // Assert
        Assert.Equal("P::A::part1", resolved);
    }

    /// <summary>
    ///     A redefinition whose only candidate is the redefining feature itself stays unresolved.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveRedefinition_OnlySelf_StaysUnresolved()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("package P { part holder { part :>> nothing; } }");
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act / Assert
        Assert.Null(resolver.ResolveRedefinition("nothing", "P::holder", "P::holder::nothing", null));
    }

    /// <summary>
    ///     A variant usage implicitly specializes its owning variation, so the variation's members
    ///     (and those of its type) are visible to the variant's redefinitions.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_VariantSeesVariationMembers()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def EngineDef { part cyl; }
                part def Car {
                    variation part engine : EngineDef {
                        variant part small { part :>> cyl; }
                    }
                }
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.Contains("P::Car::engine", resolver.LookupGeneralTypes("P::Car::engine::small"));
        Assert.Equal("P::EngineDef::cyl", resolver.ResolveRedefinedFeature("cyl", "P::Car::engine::small"));
    }

    /// <summary>
    ///     A usage typed by a conjugated port definition sees the members of the port definition.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_ConjugatedPortTypeMembersVisible()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                port def PortT { attribute publish; }
                part def Q { port p : ~PortT { attribute :>> publish; } }
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);

        // Act / Assert
        Assert.Contains("P::PortT", resolver.LookupGeneralTypes("P::Q::p"));
        Assert.Equal("P::PortT::publish", resolver.ResolveRedefinedFeature("publish", "P::Q::p"));
        Assert.DoesNotContain("P::PortT", resolver.GeneralTypes("P::Q::p"));
    }

    /// <summary>
    ///     An unqualified redefinition target inside a nested feature of a typed usage resolves
    ///     through the enclosing usage's type.
    /// </summary>
    [Fact]
    public async Task NameResolver_InheritedLookup_EnclosingTypedUsage_TypeMembersResolveForNestedRedefinition()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Wheel { attribute radius; }
                part def Car { part wheel : Wheel { part hub { attribute :>> radius; } } }
            }
            """);
        var resolver = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act
        var resolved = resolver.ResolveRedefinition("radius", "P::Car::wheel::hub", "P::Car::wheel::hub::radius", null);

        // Assert
        Assert.Equal("P::Wheel::radius", resolved);
    }

    /// <summary>
    ///     Implicit and declared specializations terminate when a user type is named like a
    ///     library root and when two user types specialize each other.
    /// </summary>
    [Fact]
    public async Task NameResolver_ImplicitSpecialization_CyclesTerminate()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Part :> Parts::Part;
                part def A :> B;
                part def B :> A;
            }
            """);
        var resolver = new NameResolver(workspace.Declarations);
        var resolving = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act / Assert
        Assert.Null(resolver.Resolve("missing", "P::A"));
        Assert.Null(resolving.Resolve("missing", "P::A"));
        Assert.Equal("Items::Item::shape", resolving.Resolve("shape", "P::Part"));
        Assert.DoesNotContain("P::A", resolver.LookupGeneralTypes("P::A"));
    }

    /// <summary>
    ///     In resolving mode a lookup through implicit general types does not depend on the order
    ///     lookups are made in.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolvingMode_ImplicitSpecialization_IsOrderIndependent()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Derived :> Base;
                part def Base { attribute own; }
            }
            """);
        var forward = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);
        var backward = new NameResolver(workspace.Declarations, null, declaredReferencesOnly: true);

        // Act
        var first = new[] { forward.Resolve("shape", "P::Derived"), forward.Resolve("own", "P::Derived") };
        _ = backward.Resolve("shape", "P::Base");
        _ = backward.Resolve("Base", "P");
        var second = new[] { backward.Resolve("shape", "P::Derived"), backward.Resolve("own", "P::Derived") };

        // Assert
        Assert.Equal(["Items::Item::shape", "P::Base::own"], first);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Implicit specializations are lookup input only: no resolved edge of any node targets an
    ///     implicit library type unless the model wrote it, so renderers, queries and exports see
    ///     exactly the declared relationships.
    /// </summary>
    [Fact]
    public async Task NameResolver_ImplicitSpecialization_NotMaterialized_AsEdges()
    {
        // Arrange
        var workspace = await LoadWorkspaceAsync("""
            package P {
                part def Engine { port fuel : ~FuelPort; attribute mass; }
                port def FuelPort { attribute flow; }
                action def Run { action step; first start then step; then done; }
                state def Idle { state off; entry; then off; }
                requirement def R { subject s : Engine; }
                part engine : Engine;
                part spare;
            }
            """);

        // Act
        var implicitTargets = NameResolver.ImplicitSpecializationTargets.ToHashSet(StringComparer.Ordinal);
        var allEdges = workspace.Index.AllEdges
            .Concat(workspace.Declarations
                .Where(declaration => !workspace.StdlibNames.Contains(declaration.Key))
                .SelectMany(declaration => declaration.Value.ResolvedEdges))
            .ToList();

        // Assert: start/done succession ends are the one library-targeting edge kind the model writes
        Assert.NotEmpty(allEdges);
        Assert.DoesNotContain(
            allEdges,
            edge => edge.Kind != SysmlEdgeKind.Transition && implicitTargets.Contains(edge.TargetQualifiedName));
    }

    /// <summary>
    ///     A chain segment after the first resolves in the previous feature's general types, so a
    ///     usage that subsets or redefines another usage reaches the members the other one has.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_ThroughSubsettingUsage_ResolvesInheritedMember()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def T { part x { part x1; } }
                part p : T;
                part p1 :> p;
                part def Base { part x { part x1; } }
                part def Sub1 :> Base { part y :> x; }
                part def Sub2 :> Base { part z :>> x; }
                part s1 : Sub1;
                part s2 : Sub2;
            }
            """);

        // Act / Assert: subsetting a typed usage; two hops; subsetting and redefining inside a type
        Assert.Equal("P::T::x", resolver.Resolve("p1.x", "P"));
        Assert.Equal("P::T::x::x1", resolver.Resolve("p1.x.x1", "P"));
        Assert.Equal("P::Base::x::x1", resolver.Resolve("s1.y.x1", "P"));
        Assert.Equal("P::Base::x::x1", resolver.Resolve("s2.z.x1", "P"));
    }

    /// <summary>
    ///     A chain segment that names an alias resolves to the alias's target, found through the
    ///     previous feature's inheritance; a private alias is not visible through the chain.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_ThroughAlias_ResolvesAliasTarget()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def T { part dest; }
                part p2 : T { alias pd1 for dest; private alias hid for dest; }
            }
            """);

        // Act / Assert
        Assert.Equal("P::T::dest", resolver.Resolve("p2.pd1", "P"));
        Assert.Null(resolver.Resolve("p2.hid", "P"));
        Assert.Equal("P::T::dest", resolver.Resolve("hid", "P::p2"));
    }

    /// <summary>
    ///     A usage typed by a conjugated port definition exposes that port definition's members.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_ThroughConjugatedPortType_ResolvesPortMembers()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                port def PortT { attribute x; in attribute y; }
                part def A { port p : ~PortT; port q : PortT; }
                part a : A;
            }
            """);

        // Act / Assert
        Assert.Equal("P::PortT::x", resolver.Resolve("a.p.x", "P"));
        Assert.Equal("P::PortT::y", resolver.Resolve("a.p.y", "P"));
        Assert.Equal("P::PortT::x", resolver.Resolve("a.q.x", "P"));
    }

    /// <summary>
    ///     A dot inside a quoted name does not split the chain, and a quoted segment compares equal
    ///     to its declared text.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_QuotedSegments_SplitOnDotsOutsideQuotes()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def T { attribute rho; part 'a.b' { part c; } }
                part 'Solve for Pressure1' : T;
                part 'a.b' { part c; }
                part q : T;
            }
            """);

        // Act / Assert
        Assert.Equal("P::T::rho", resolver.Resolve("'Solve for Pressure1'.rho", "P"));
        Assert.Equal("P::'a.b'::c", resolver.Resolve("'a.b'.c", "P"));
        Assert.Equal("P::T::'a.b'::c", resolver.Resolve("q.'a.b'.c", "P"));
        Assert.Null(resolver.Resolve("'a.b'.rho", "P"));
    }

    /// <summary>
    ///     A feature's own inline members are found before the members of its types.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_PrefersOwnedInlineMemberOverTypeMember()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def Assembly { part element; part other; }
                part def Sys { part assembly : Assembly { part :>> element { part own; } } }
                part s : Sys;
            }
            """);

        // Act / Assert
        Assert.Equal("P::Sys::assembly::element", resolver.Resolve("s.assembly.element", "P"));
        Assert.Equal("P::Sys::assembly::element::own", resolver.Resolve("s.assembly.element.own", "P"));
        Assert.Equal("P::Assembly::other", resolver.Resolve("s.assembly.other", "P"));
    }

    /// <summary>
    ///     Private and protected members are not visible through a chain, even through a subsetting
    ///     usage or an alias, while the first segment sees its namespace's private members.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_PrivateAndProtectedThroughSubsettingUsage_NotVisible()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def T { private part hid; protected part prot; part pub; }
                part p : T;
                part p1 :> p;
                private part sec : T;
            }
            """);

        // Act / Assert
        Assert.Equal("P::T::pub", resolver.Resolve("p1.pub", "P"));
        Assert.Null(resolver.Resolve("p1.hid", "P"));
        Assert.Null(resolver.Resolve("p1.prot", "P"));
        Assert.Equal("P::T::pub", resolver.Resolve("sec.pub", "P"));
    }

    /// <summary>
    ///     A three-level chain mixes owned, typed and subsetted hops.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_ThreeLevels_MixedDirectTypeAndSpecialization()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def Wheel { port hub; }
                part def Axle { part wheel : Wheel; }
                part def Car { part axle : Axle { part inner; } }
                part car : Car;
                part car2 :> car;
            }
            """);

        // Act / Assert
        Assert.Equal("P::Wheel::hub", resolver.Resolve("car.axle.wheel.hub", "P"));
        Assert.Equal("P::Wheel::hub", resolver.Resolve("car2.axle.wheel.hub", "P"));
        Assert.Equal("P::Car::axle::inner", resolver.Resolve("car2.axle.inner", "P"));
    }

    /// <summary>
    ///     A chain whose middle segment does not resolve, or whose later segment does not exist,
    ///     does not resolve at all.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_UnresolvedSegment_DoesNotResolve()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P { part def T { part x; } part p : T; }
            """);

        // Act / Assert
        Assert.Null(resolver.Resolve("p.nope", "P"));
        Assert.Null(resolver.Resolve("nope.x", "P"));
        Assert.Null(resolver.Resolve("p.nope.x", "P"));
        Assert.Null(resolver.Resolve("p.x.nope", "P"));
        Assert.Null(resolver.Resolve("p.", "P"));
    }

    /// <summary>
    ///     Self-referential and mutually referential specializations that go through chains
    ///     terminate.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_SpecializationCycle_Terminates()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def T { part y; }
                ref x : T :> x.y;
                part a :> b;
                part b :> a;
            }
            """);

        // Act / Assert
        Assert.Equal("P::T::y", resolver.Resolve("x.y", "P"));
        Assert.Null(resolver.Resolve("a.nope", "P"));
        Assert.Null(resolver.Resolve("b.nope", "P"));
    }

    /// <summary>
    ///     A chain whose first segment is written with a qualified name or global prefix resolves
    ///     the first segment as that qualified name.
    /// </summary>
    [Fact]
    public async Task NameResolver_FeatureChain_QualifiedFirstSegment_Resolves()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P { part def T { part x; } part p : T; }
            package Q { }
            """);

        // Act / Assert
        Assert.Equal("P::T::x", resolver.Resolve("P::p.x", "Q"));
        Assert.Equal("P::T::x", resolver.Resolve("$::P::p.x", "Q"));
    }

    /// <summary>
    ///     A redefinition target written as a chain (<c>:&gt;&gt; clock.time</c>) resolves its first
    ///     segment among the general types of the owning type and the rest by visible resolution.
    /// </summary>
    [Fact]
    public async Task NameResolver_ResolveRedefinedFeature_Chain_ResolvesThroughFirstSegment()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package P {
                part def Clock { attribute time; }
                part def Base { part clock : Clock; }
                part def Derived :> Base;
            }
            """);

        // Act / Assert
        Assert.Equal("P::Clock::time", resolver.ResolveRedefinedFeature("clock.time", "P::Derived"));
        Assert.Null(resolver.ResolveRedefinedFeature("clock.nope", "P::Derived"));
    }

    /// <summary>
    ///     A quoted and an unquoted spelling of the same simple name are the same name, in
    ///     qualified names and in imports.
    /// </summary>
    [Fact]
    public async Task NameResolver_Resolve_QuotedAndUnquotedSimpleName_AreEquivalent()
    {
        // Arrange
        var resolver = await LoadAsync("""
            package PowerTrain { part def Engine; }
            package 'Quoted' { part def Gear; }
            package User { private import 'PowerTrain'::*; private import Quoted::Gear; part e : Engine; part g : Gear; }
            """);

        // Act / Assert (a quoted declaration keeps its quotes in its qualified name)
        Assert.Equal("PowerTrain::Engine", resolver.Resolve("'PowerTrain'::Engine", null));
        Assert.Equal("PowerTrain::Engine", resolver.Resolve("Engine", "User"));
        Assert.Equal("'Quoted'::Gear", resolver.Resolve("Quoted::Gear", null));
        Assert.Equal("'Quoted'::Gear", resolver.Resolve("'Quoted'::'Gear'", null));
        Assert.Equal("'Quoted'::Gear", resolver.Resolve("Gear", "User"));
        Assert.Equal("PowerTrain", resolver.Resolve("'PowerTrain'", null));
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
