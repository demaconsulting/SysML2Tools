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
    private static async Task<NameResolver> LoadAsync(string sysml)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return new NameResolver(result.Workspace.Declarations);
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
}
