// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Tests for <see cref="WorkspaceLoader"/>.
/// </summary>
public sealed class WorkspaceLoaderTests
{
    // Level 1: Empty file returns non-null workspace without errors
    /// <summary>
    ///     An empty SysML file should produce a non-null workspace with no errors.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_EmptyFile_ReturnsNonNullWorkspace()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, string.Empty, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.False(result.HasErrors);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 2: Single package registers declaration
    /// <summary>
    ///     A SysML file with a single package should register the package in the declarations.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SinglePackage_RegistersDeclaration()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, "package Foo {}", TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace.Declarations.ContainsKey("Foo"),
                "Expected 'Foo' in declarations");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 3: Nested packages register qualified names
    /// <summary>
    ///     Nested packages should register both the parent and child qualified names.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NestedPackages_RegistersQualifiedNames()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, "package A { package B {} }", TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace.Declarations.ContainsKey("A"), "Expected 'A'");
            Assert.True(result.Workspace.Declarations.ContainsKey("A::B"), "Expected 'A::B'");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 4: Part definition registers declaration
    /// <summary>
    ///     A part def inside a package should register its qualified name.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PartDef_RegistersDefinition()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, "package P { part def W {} }", TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace.Declarations.ContainsKey("P::W"),
                "Expected 'P::W' in declarations");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 5: No-files load returns non-null workspace (stdlib only)
    /// <summary>
    ///     Loading with no user files should still return a non-null workspace with stdlib declarations.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NoFiles_ReturnsNonNullWorkspace()
    {
        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        // Stdlib has many declarations
        Assert.NotEmpty(result.Workspace.Declarations);
    }

    // Level 6: Stdlib declarations are registered
    /// <summary>
    ///     The stdlib should contribute declarations to the workspace without errors.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered()
    {
        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        // Stdlib should register at least some declarations
        Assert.True(result.Workspace.Declarations.Count > 0,
            "Expected stdlib declarations to be registered");
        // No errors from stdlib loading
        Assert.False(result.HasErrors);
    }

    // Level 7: Specializes chain resolves
    /// <summary>
    ///     A derived part def that specializes a base def in the same package should resolve without warnings.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SpecializesChain_Registered()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Base {}
                    part def Derived specializes P::Base {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.True(result.Workspace.Declarations.ContainsKey("P::Base"), "Expected 'P::Base'");
            Assert.True(result.Workspace.Declarations.ContainsKey("P::Derived"), "Expected 'P::Derived'");
            // Supertype should resolve — no unresolved warning for P::Base
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("P::Base"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 8: Unresolved reference produces Warning diagnostic
    /// <summary>
    ///     A part def that specializes a non-existent type should produce a Warning diagnostic.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedReference_ProducesWarning()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def X specializes NonExistentType {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("NonExistentType"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 9: Circular import produces Warning and does not loop infinitely
    /// <summary>
    ///     Packages that import each other should produce a Warning and complete in finite time.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CircularImport_ProducesWarningNoInfiniteLoop()
    {
        // Arrange — two files that declare packages importing each other by name
        var tempFile1 = Path.GetTempFileName() + ".sysml";
        var tempFile2 = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile1, "package A { import B::*; }", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(tempFile2, "package B { import A::*; }", TestContext.Current.CancellationToken);

            // Act — cycle detection must terminate (not loop forever).
            // Use xUnit's per-test cancellation token rather than a hard 30-second
            // limit; stdlib loading on a cold Linux CI runner can take longer than 30s.
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile1, tempFile2], stdlibTable)
                .WaitAsync(TestContext.Current.CancellationToken);

            // Assert — circular import warning present
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }

    // Level 10: Unreadable file produces Error diagnostic
    /// <summary>
    ///     A path to a file that cannot be read (non-existent) should produce an Error diagnostic.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnreadableFile_ProducesErrorDiagnostic()
    {
        // Arrange — path to a file that does not exist
        var nonExistentPath = Path.Combine(
            Path.GetTempPath(),
            $"nonexistent_{Guid.NewGuid():N}.sysml");

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([nonExistentPath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.True(result.HasErrors, "Expected HasErrors to be true for an unreadable file");
        Assert.Contains(result.Diagnostics,
            d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Error &&
                 d.FilePath == nonExistentPath);
    }

    /// <summary>
    ///     Validates that a cyclic specialization chain (A specializes B, B specializes A)
    ///     produces a Warning diagnostic and completes in finite time.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CyclicSpecialization_ProducesWarning()
    {
        // Arrange — A specializes B, B specializes A (cyclic)
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def A specializes P::B {}
                    part def B specializes P::A {}
                }
                """, TestContext.Current.CancellationToken);

            // Act — cycle detection must terminate (not loop forever).
            // Use xUnit's per-test cancellation token rather than a hard 30-second
            // limit; stdlib loading on a cold Linux CI runner can take longer than 30s.
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable)
                .WaitAsync(TestContext.Current.CancellationToken);

            // Assert — cyclic specialization warning present
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("Cyclic specialization"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 11: Unqualified name in same package resolves without warning
    /// <summary>
    ///     A part def that specializes a sibling defined in the same package using its short
    ///     (unqualified) name should resolve correctly and produce no "Unresolved reference"
    ///     warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnqualifiedNameSamePackage_ResolvesWithoutWarning()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package A {
                    part def Foo {}
                    part def Baz specializes Foo {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — unqualified "Foo" should resolve to A::Foo via namespace scope
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("Foo"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 12: Unqualified name resolves via wildcard import
    /// <summary>
    ///     A part def that specializes a type using only its short name, where that type is
    ///     brought into scope by a wildcard import, should resolve without an "Unresolved
    ///     reference" warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnqualifiedNameViaWildcardImport_ResolvesWithoutWarning()
    {
        // Arrange — Bar is defined in Pkg; Other imports Pkg::* and references Bar by short name
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package Pkg { part def Bar {} }
                package Other {
                    import Pkg::*;
                    part def Foo specializes Bar {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — "Bar" resolves via Pkg::Bar through the wildcard import
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("Bar"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 12b: Unqualified name resolves via recursive wildcard import
    /// <summary>
    ///     A recursive wildcard import (<c>import X::*::**;</c>) must reach members declared in
    ///     namespaces nested at any depth under <c>X</c>, not just <c>X</c>'s own direct members.
    ///     This mirrors the official OMG conformance construct in
    ///     <c>test/SysMLModels/OMG/examples/SimpleTests/ImportTest.sysml</c> (<c>private import
    ///     Pkg211::*::**;</c>), which previously parsed without error but never actually resolved
    ///     nested-namespace references — the OMG corpus sweep only checks for parse errors, never
    ///     resolution correctness, so this gap went uncaught.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnqualifiedNameViaRecursiveWildcardImport_ResolvesWithoutWarning()
    {
        // Arrange — Foo and Bar each live two namespace levels below package A, in the
        // nested Sub1 and Sub2 sub-packages respectively. Consumer uses a recursive
        // wildcard import targeting A and references both by short name.
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package TestNs {
                    package A {
                        package Sub1 {
                            part def Foo;
                        }
                        package Sub2 {
                            part def Bar;
                        }
                    }
                }
                package Consumer {
                    import TestNs::A::*::**;

                    part def UsesBoth {
                        part f : Foo;
                        part b : Bar;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — "Foo" and "Bar" both resolve via the recursive wildcard import, even
            // though they're nested two levels below the imported namespace.
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     (d.Message.Contains("Foo") || d.Message.Contains("Bar")));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 12d: Ambiguous recursive wildcard match resolves to the shallowest namespace depth,
    // not the shortest qualified-name string
    /// <summary>
    ///     When two namespaces at different nesting depths under a recursive wildcard import's
    ///     target both declare a same-named member, resolution must prefer the genuinely
    ///     shallowest (least-nested) match — measured by the number of intermediate namespace
    ///     segments, not by comparing raw qualified-name string length. This uses deliberately
    ///     long package names at the shallow depth and short ones at the deep depth so a
    ///     length-based tie-break (an easy mistake) would pick the wrong one.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RecursiveWildcardImport_AmbiguousMatch_PrefersShallowestDepth()
    {
        // Arrange — Foo is declared twice under A: once one level down in a namespace with a
        // deliberately long name (shallow, but long string), and once three levels down through
        // short-named namespaces (deep, but short string). The shallow one must win.
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package TestNs {
                    package A {
                        package AVeryLongIntermediateNamespaceName {
                            part def Foo;
                        }
                        package B {
                            package C {
                                package D {
                                    part def Foo {}
                                }
                            }
                        }
                    }
                }
                package Consumer {
                    import TestNs::A::*::**;

                    part def Uses {
                        part f : Foo;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var index = result.Workspace.Index;

            // Assert — "Foo" resolves as a Typing edge to the shallower (one-level-deep) match,
            // not the deeper-but-shorter-string one.
            Assert.Contains(index.GetOutgoingEdges("Consumer::Uses::f"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "TestNs::A::AVeryLongIntermediateNamespaceName::Foo");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 12e: Unqualified name resolves via recursive membership import, including the
    // imported member's own name
    /// <summary>
    ///     A recursive <em>membership</em> import (<c>import X::Y::**;</c>, as distinct from the
    ///     recursive namespace-import form covered by the previous test) must bring both the
    ///     explicitly named member <c>Y</c> itself into scope by its own short name, and every
    ///     name declared in a namespace nested under <c>Y</c> at any depth. This is a distinct
    ///     grammar shape from <c>import X::*::**;</c>: there, <c>X</c> is a containing namespace
    ///     being wildcard-searched and is never itself a name being imported, whereas here <c>Y</c>
    ///     is the explicit membership-import target and must resolve by its own name too.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnqualifiedNameViaRecursiveMembershipImport_ResolvesWithoutWarning()
    {
        // Arrange — A is a nested package directly under TestNs; Foo lives one level further
        // down, inside A's own Sub1 sub-package. Consumer uses a recursive membership import
        // targeting A itself and references both A and Foo by short name.
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package TestNs {
                    package A {
                        package Sub1 {
                            part def Foo;
                        }
                    }
                }
                package Consumer {
                    import TestNs::A::**;

                    part def UsesA {
                        part a : A;
                        part f : Foo;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — both "A" (the membership-import target itself) and "Foo" (A's nested
            // descendant) resolve without warning.
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     (d.Message.Contains("'A'") || d.Message.Contains("Foo")));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 12c: Non-recursive wildcard import must NOT reach nested-namespace members
    /// <summary>
    ///     A plain (non-recursive) wildcard import (<c>import X::*;</c>) must only bring X's own
    ///     direct members into scope — it must NOT reach members declared in namespaces nested
    ///     under X. This is the control case proving the recursive-descent fallback added for
    ///     <c>import X::*::**;</c> is correctly gated on <c>IsRecursive</c> and doesn't leak into
    ///     the plain wildcard-import path.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnqualifiedNameViaPlainWildcardImport_DoesNotReachNestedNamespace()
    {
        // Arrange — Foo lives in the nested Sub1 sub-package, but Consumer only uses a
        // plain (non-recursive) wildcard import targeting A.
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package TestNs {
                    package A {
                        package Sub1 {
                            part def Foo;
                        }
                    }
                }
                package Consumer {
                    import TestNs::A::*;

                    part def Uses {
                        part f : Foo;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — "Foo" must NOT resolve; a non-recursive wildcard import doesn't reach
            // into nested namespaces, so this must still produce an Unresolved reference warning.
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("Foo"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // Level 13: Explicit named import resolves short name
    /// <summary>
    ///     A part def that specializes a type using only its short name, where that type is
    ///     brought into scope by an explicit named import, should resolve without an "Unresolved
    ///     reference" warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ExplicitImportedName_ResolvesWithoutWarning()
    {
        // Arrange — Bar is defined in Pkg; Other imports Pkg::Bar by full name and references Bar
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package Pkg { part def Bar {} }
                package Other {
                    import Pkg::Bar;
                    part def Foo specializes Bar {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — "Bar" resolves via explicit import Pkg::Bar
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("Bar"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A model declaring several definition kinds registers each with the correct definition
    ///     keyword, confirming the AST builder visits all definition rule variants.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MixedDefinitionKinds_RegistersKeywords()
    {
        // Arrange: a package declaring part, port, interface, requirement, and enum definitions
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Demo {
                    part def Vehicle;
                    port def FuelPort;
                    interface def FuelInterface;
                    requirement def MassReq;
                    enum def Gear;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: each definition is registered with its expected keyword
            Assert.NotNull(result.Workspace);
            AssertKeyword(result.Workspace, "Demo::Vehicle", "part def");
            AssertKeyword(result.Workspace, "Demo::FuelPort", "port def");
            AssertKeyword(result.Workspace, "Demo::FuelInterface", "interface def");
            AssertKeyword(result.Workspace, "Demo::MassReq", "requirement def");
            AssertKeyword(result.Workspace, "Demo::Gear", "enum def");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loading with a stdlib seed populates the workspace's <see cref="SysmlWorkspace.StdlibNames"/>
    ///     set with the seed's qualified names while excluding user declarations.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PopulatesStdlibNamesFromSeed()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, "package UserPkg { part def UserPart; }", TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: stdlib names are recorded and the user declaration is not among them
            Assert.NotNull(result.Workspace);
            Assert.NotEmpty(result.Workspace.StdlibNames);
            Assert.DoesNotContain("UserPkg::UserPart", result.Workspace.StdlibNames);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A definition owning usages registers them as feature children carrying the usage keyword,
    ///     declared name, and feature typing (including the type held by the <c>typed by</c> clause).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DefinitionUsages_CaptureKeywordAndTyping()
    {
        // Arrange: a part def owning an attribute, a port, and a multiplicity-bearing part usage
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Demo {
                    part def Engine;
                    port def FuelPort;
                    part def Vehicle {
                        attribute mass : Real;
                        port fuelInlet : FuelPort;
                        part engine : Engine;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: the Vehicle definition owns three feature children with the expected typing
            Assert.NotNull(result.Workspace);
            var vehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Demo::Vehicle"]);
            var features = vehicle.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .ToList();

            AssertFeature(features, "mass", "attribute", "Real");
            AssertFeature(features, "fuelInlet", "port", "FuelPort");
            AssertFeature(features, "engine", "part", "Engine");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A part definition with connection usages captures each connection's two endpoints.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionUsages_CaptureEndpoints()
    {
        // Arrange: a part def with two parts and a connection between them
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Demo {
                    part def Engine;
                    part def Gearbox;
                    part def Drivetrain {
                        part engine : Engine;
                        part gearbox : Gearbox;
                        connection link connect engine to gearbox;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: the Drivetrain owns a connection node referencing both parts
            Assert.NotNull(result.Workspace);
            var drivetrain = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Demo::Drivetrain"]);
            var connection = drivetrain.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlConnectionNode>()
                .Single();
            Assert.Equal("engine", connection.EndpointA);
            Assert.Equal("gearbox", connection.EndpointB);
            Assert.Null(connection.EndpointAName);
            Assert.Null(connection.EndpointAMultiplicity);
            Assert.Null(connection.EndpointBName);
            Assert.Null(connection.EndpointBMultiplicity);
            Assert.Null(connection.PayloadText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loads the SysML source and returns the connection nodes owned by the part definition
    ///     <c>Demo::Rig</c>, in declaration order.
    /// </summary>
    private static async Task<IReadOnlyList<DemaConsulting.SysML2Tools.Semantic.Model.SysmlConnectionNode>> LoadRigConnectionsAsync(string source)
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, source, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            var rig = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Demo::Rig"]);
            return rig.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlConnectionNode>().ToList();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A connection whose ends declare a role name (<c>name ::&gt; reference</c>) and a cross
    ///     multiplicity captures both beside the endpoint reference, and an end without either keeps
    ///     them null.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionEnds_CaptureRoleNamesAndMultiplicities()
    {
        // Arrange / Act: the first end has a role name and a multiplicity, the second only a role name
        var connections = await LoadRigConnectionsAsync(
            """
            package Demo {
                part def Source { port out1; }
                part def Sink { port in1; }
                part def Rig {
                    part src : Source;
                    part snk : Sink;
                    connection feed connect [2] supply ::> src.out1 to drain ::> snk.in1;
                    connection plain connect src.out1 to snk.in1;
                }
            }
            """);

        // Assert
        var feed = connections.Single(c => c.Name == "feed");
        Assert.Equal("src.out1", feed.EndpointA);
        Assert.Equal("supply", feed.EndpointAName);
        Assert.Equal("[2]", feed.EndpointAMultiplicity);
        Assert.Equal("snk.in1", feed.EndpointB);
        Assert.Equal("drain", feed.EndpointBName);
        Assert.Null(feed.EndpointBMultiplicity);
        var plain = connections.Single(c => c.Name == "plain");
        Assert.Null(plain.EndpointAName);
        Assert.Null(plain.EndpointAMultiplicity);
        Assert.Null(plain.EndpointBName);
        Assert.Null(plain.EndpointBMultiplicity);
    }

    /// <summary>
    ///     A binding connector (<c>bind A = B</c>) with a role name and a multiplicity on its ends
    ///     captures them the same way a connection does.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BindingEnds_CaptureRoleNames()
    {
        // Arrange / Act
        var connections = await LoadRigConnectionsAsync(
            """
            package Demo {
                part def Rig {
                    part a { port p; }
                    part b { port q; }
                    bind left ::> a.p = [1..*] b.q;
                }
            }
            """);

        // Assert
        var binding = Assert.Single(connections);
        Assert.Equal("binding", binding.ConnectionKeyword);
        Assert.Equal("a.p", binding.EndpointA);
        Assert.Equal("left", binding.EndpointAName);
        Assert.Equal("b.q", binding.EndpointB);
        Assert.Equal("[1..*]", binding.EndpointBMultiplicity);
    }

    /// <summary>
    ///     An interface usage with <c>connect a.p to b.q</c> becomes a connection node with the
    ///     <c>"interface"</c> keyword, its name, and both ends (with role names).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_InterfaceUsage_BuildsInterfaceConnectionNode()
    {
        // Arrange / Act
        var connections = await LoadRigConnectionsAsync(
            """
            package Demo {
                part def Rig {
                    part a { port p; }
                    part b { port q; }
                    interface bus connect left ::> a.p to b.q;
                }
            }
            """);

        // Assert
        var bus = Assert.Single(connections);
        Assert.Equal("interface", bus.ConnectionKeyword);
        Assert.Equal("bus", bus.Name);
        Assert.Equal("a.p", bus.EndpointA);
        Assert.Equal("left", bus.EndpointAName);
        Assert.Equal("b.q", bus.EndpointB);
        Assert.Null(bus.PayloadText);
    }

    /// <summary>
    ///     A flow usage captures its two ends and the payload clause after <c>of</c>; a flow
    ///     without a payload leaves <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlConnectionNode.PayloadText"/> null.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FlowUsage_BuildsFlowConnectionNodeWithPayload()
    {
        // Arrange / Act
        var connections = await LoadRigConnectionsAsync(
            """
            package Demo {
                item def Torque;
                part def Rig {
                    part a { out port shaft; }
                    part b { in port input; }
                    flow of Torque from a.shaft to b.input;
                    flow from a.shaft to b.input;
                }
            }
            """);

        // Assert
        Assert.Equal(2, connections.Count);
        Assert.All(connections, c => Assert.Equal("flow", c.ConnectionKeyword));
        Assert.Equal("a.shaft", connections[0].EndpointA);
        Assert.Equal("b.input", connections[0].EndpointB);
        Assert.Equal("Torque", connections[0].PayloadText);
        Assert.Null(connections[1].PayloadText);
        Assert.Equal("a.shaft", connections[1].EndpointA);
    }

    /// <summary>
    ///     A message with a payload (<c>message m of Payload from a to b</c>) captures the payload
    ///     text beside its two event references.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MessageWithPayload_CapturesPayloadText()
    {
        // Arrange / Act
        var connections = await LoadRigConnectionsAsync(
            """
            package Demo {
                item def Token;
                part def Rig {
                    part a;
                    part b;
                    message grant of Token from a to b;
                    message ping from b to a;
                }
            }
            """);

        // Assert
        var grant = connections.Single(c => c.Name == "grant");
        Assert.Equal("message", grant.ConnectionKeyword);
        Assert.Equal("a", grant.EndpointA);
        Assert.Equal("b", grant.EndpointB);
        Assert.Equal("Token", grant.PayloadText);
        Assert.Null(connections.Single(c => c.Name == "ping").PayloadText);
    }

    /// <summary>
    ///     A part's <c>event occurrence</c> usages (with or without a leading <c>then</c>) are captured
    ///     as children with the keyword <c>event occurrence</c>, in declaration order; the referencing
    ///     form <c>event someFeature;</c> declares nothing and is not captured.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_EventOccurrences_AreCapturedInDeclarationOrder()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Demo {
                    part def Rig {
                        part a {
                            event occurrence alpha;
                            then event occurrence beta : Occurrence;
                            then event gamma;
                        }
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var rig = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Demo::Rig"]);
            var part = Assert.Single(rig.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>());
            var events = part.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>().ToList();
            Assert.Equal(["alpha", "beta"], events.Select(e => e.Name));
            Assert.All(events, e => Assert.Equal("event occurrence", e.FeatureKeyword));
            Assert.Equal("Demo::Rig::a::alpha", events[0].QualifiedName);
            Assert.Equal("Occurrence", events[1].FeatureTyping);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Interface and flow usages add no resolver diagnostics: a model whose ports are connected
    ///     by them loads without errors.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_InterfaceAndFlowUsages_ProduceNoDiagnostics()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Demo {
                    item def Torque;
                    part def Rig {
                        part a { out port shaft; }
                        part b { in port input; }
                        interface bus connect a.shaft to b.input;
                        flow of Torque from a.shaft to b.input;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A state definition captures its declared state usages and transitions, recording each
    ///     transition's source, target, and guard.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StateDefinition_CapturesStatesAndTransitions()
    {
        // Arrange: a state def with three states and guarded transitions
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package SM {
                    state def Light {
                        state stop;
                        state go;
                        transition first stop if t then go;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: the state def owns two state features and one transition
            Assert.NotNull(result.Workspace);
            var light = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["SM::Light"]);
            var states = light.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .Where(f => f.FeatureKeyword == "state")
                .ToList();
            Assert.Equal(2, states.Count);

            var transition = light.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Single();
            Assert.Equal("stop", transition.Source);
            Assert.Equal("go", transition.Target);
            Assert.Equal("t", transition.Guard);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An action definition captures its action usages and successions (as transition nodes).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ActionDefinition_CapturesActionsAndSuccessions()
    {
        // Arrange: an action def with two actions and a succession between them
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package AF {
                    action def Flow {
                        action stepA;
                        action stepB;
                        first stepA then stepB;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: the action def owns two action features and one succession
            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var actions = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .Count(f => f.FeatureKeyword == "action");
            Assert.Equal(2, actions);

            var succession = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Single();
            Assert.Equal("stepA", succession.Source);
            Assert.Equal("stepB", succession.Target);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A definition with message usages captures each message's name and from/to endpoints.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_Messages_CaptureEndpoints()
    {
        // Arrange: a part def with two parts (each with an event) and a message between them
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Seq {
                    part def Protocol {
                        part client { event occurrence s; }
                        part server { event occurrence r; }
                        message request from client.s to server.r;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: the protocol owns a message connection with the expected endpoints
            Assert.NotNull(result.Workspace);
            var protocol = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Seq::Protocol"]);
            var message = protocol.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlConnectionNode>()
                .Single(c => c.ConnectionKeyword == "message");
            Assert.Equal("request", message.Name);
            Assert.Equal("client.s", message.EndpointA);
            Assert.Equal("server.r", message.EndpointB);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A resolved supertype reference should be recorded as a <c>Supertype</c> edge in the
    ///     workspace's <see cref="SysmlWorkspace.Index"/>, queryable from both directions.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ResolvedSupertype_RecordsSupertypeEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package A {
                    part def Ancestor {}
                    part def Child specializes Ancestor {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var outgoing = result.Workspace.Index.GetOutgoingEdges("A::Child");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype &&
                     e.TargetQualifiedName == "A::Ancestor");

            var incoming = result.Workspace.Index.GetIncomingEdges("A::Ancestor");
            Assert.Contains(incoming,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype &&
                     e.SourceQualifiedName == "A::Child");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A resolved feature typing reference should be recorded as a <c>Typing</c> edge in the
    ///     workspace's <see cref="SysmlWorkspace.Index"/>, queryable from both directions.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ResolvedFeatureTyping_RecordsTypingEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Engine {}
                part def Car {
                    part engine : Engine;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var outgoing = result.Workspace.Index.GetOutgoingEdges("Car::engine");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "Engine");

            var incoming = result.Workspace.Index.GetIncomingEdges("Engine");
            Assert.Contains(incoming,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.SourceQualifiedName == "Car::engine");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A feature typed by a non-existent type should produce a Warning diagnostic (same
    ///     message format as unresolved supertype references) and must not produce a
    ///     <c>Typing</c> edge for that reference.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedFeatureTyping_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def X {
                        part y : NonExistentType;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("NonExistentType"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "NonExistentType");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A feature using the <c>redefines</c> keyword form should capture the raw redefined-feature
    ///     reference text in <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode.RedefinedFeatureName"/>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RedefinesKeyword_CapturesRedefinedFeatureName()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Vehicle {
                    attribute eng : Real;
                }
                part def SmallVehicle :> Vehicle {
                    attribute smallEng : Real redefines eng;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var smallVehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["SmallVehicle"]);
            var smallEng = smallVehicle.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .First(f => f.Name == "smallEng");

            Assert.Equal("eng", smallEng.RedefinedFeatureName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A feature using the <c>:&gt;&gt;</c> operator form should capture the same raw
    ///     redefined-feature reference text as the <c>redefines</c> keyword form.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ColonGtGtOperator_CapturesRedefinedFeatureName()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Vehicle {
                    attribute eng : Real;
                }
                part def SmallVehicle :> Vehicle {
                    attribute smallEng : Real :>> eng;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var smallVehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["SmallVehicle"]);
            var smallEng = smallVehicle.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .First(f => f.Name == "smallEng");

            Assert.Equal("eng", smallEng.RedefinedFeatureName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A qualified redefinition reference (<c>Owner::feature</c>) should capture the raw
    ///     qualified text verbatim, without any resolution applied at the AST-building stage.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_QualifiedRedefinition_CapturesRawText()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Vehicle {
                    attribute mass : Real;
                }
                part def Car :> Vehicle {
                    attribute carMass : Real redefines Vehicle::mass;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var car = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Car"]);
            var carMass = car.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .First(f => f.Name == "carMass");

            Assert.Equal("Vehicle::mass", carMass.RedefinedFeatureName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A feature declaring no redefinition should leave
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode.RedefinedFeatureName"/>
    ///     null.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NoRedefinition_RedefinedFeatureNameIsNull()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Vehicle {
                    attribute mass : Real;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var vehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Vehicle"]);
            var mass = vehicle.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .First(f => f.Name == "mass");

            Assert.Null(mass.RedefinedFeatureName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An implicitly-named redefining usage whose <c>redefines</c> reference is a dot-chained
    ///     feature path (e.g. <c>tank.fuelTankPort</c>, grammatically distinct from a <c>::</c>-qualified
    ///     name — <c>ownedRedefinition</c> is <c>qualifiedName ( DOT qualifiedName )*</c>) must still
    ///     take only the trailing simple-name segment (<c>fuelTankPort</c>), not the whole dotted
    ///     reference text, as its implicit name.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ImplicitNameFromDottedRedefinitionChain_UsesTrailingSegment()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Vehicle {
                    part tank {
                        port fuelTankPort;
                    }
                    port redefines tank.fuelTankPort {
                        item item1;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var vehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Vehicle"]);
            var implicitlyNamedPort = vehicle.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .First(f => f.RedefinedFeatureName == "tank.fuelTankPort");

            Assert.Equal("fuelTankPort", implicitlyNamedPort.Name);
            Assert.Equal("Vehicle::fuelTankPort", implicitlyNamedPort.QualifiedName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A resolved redefined-feature reference should be recorded as a <c>Redefinition</c> edge
    ///     in the workspace's <see cref="SysmlWorkspace.Index"/>, queryable from both directions.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ResolvedRedefinition_RecordsRedefinitionEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                attribute eng : Real;
                part def SmallVehicle {
                    attribute smallEng : Real redefines eng;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var outgoing = result.Workspace.Index.GetOutgoingEdges("SmallVehicle::smallEng");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Redefinition &&
                     e.TargetQualifiedName == "eng");

            var incoming = result.Workspace.Index.GetIncomingEdges("eng");
            Assert.Contains(incoming,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Redefinition &&
                     e.SourceQualifiedName == "SmallVehicle::smallEng");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A feature redefining a non-existent feature should produce a Warning diagnostic (same
    ///     message format as unresolved supertype/typing references) and must not produce a
    ///     <c>Redefinition</c> edge for that reference.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedRedefinition_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def X {
                        attribute y : Real redefines NonExistentFeature;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("NonExistentFeature"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Redefinition &&
                     e.TargetQualifiedName == "NonExistentFeature");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A bare-name <c>redefines eng</c> where <c>eng</c> is a member declared only on the
    ///     redefining feature's owner's <em>supertype</em> (not on the owner itself, and not
    ///     imported) — the dominant real-world shape per the SysML v2 spec — should still
    ///     resolve to a <c>Redefinition</c> edge, and must not produce a false
    ///     "Unresolved reference" Warning diagnostic. Mirrors
    ///     <c>05.Redefinition/RedefinitionExample.sysml</c>'s <c>SmallVehicle::smallEng
    ///     redefines eng</c> shape exactly.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BareRedefinitionOfInheritedFeature_RecordsRedefinitionEdgeNoWarning()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Engine;
                part def SmallEngine :> Engine;

                part def Vehicle {
                    part eng : Engine;
                }

                part def SmallVehicle :> Vehicle {
                    part smallEng : SmallEngine redefines eng;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Redefinition &&
                     e.SourceQualifiedName == "SmallVehicle::smallEng" &&
                     e.TargetQualifiedName == "Vehicle::eng");
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("eng"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loading the OMG <c>05.Redefinition/RedefinitionExample.sysml</c> training fixture
    ///     should not produce any false "Unresolved reference" Warning diagnostics — regression
    ///     coverage for the bare-name <c>redefines eng</c>/<c>redefines cyl</c> inherited-member
    ///     forms that previously failed to resolve.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RedefinitionExampleFixture_NoUnresolvedReferenceWarnings()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(modelsRoot, "OMG", "training", "05.Redefinition", "RedefinitionExample.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                 d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Loading the OMG <c>1c-PartsTreeRedefinition.sysml</c> validation fixture should not
    ///     produce any false "Unresolved reference" Warning diagnostics for the redefined/
    ///     subsetting feature names in this fixture — regression coverage for the nested
    ///     bare-name <c>redefines frontAxleAssembly</c>/<c>redefines frontAxle</c>/<c>redefines
    ///     rearAxleAssembly</c>/<c>redefines rearAxle</c> forms (where the innermost owner, e.g.
    ///     <c>frontAxleAssembly_c1</c>, has no supertype of its own and the inherited member is
    ///     only reachable via the owner's own <c>Redefinition</c> edge), plus the sibling
    ///     bare-name <c>subsets frontWheel</c>/<c>subsets rearWheel</c> forms. Pre-existing,
    ///     unrelated stdlib-coverage gaps in this fixture (<c>SI::kg</c>, <c>ISQ::mass</c>) are
    ///     intentionally excluded from this assertion — this test targets only the
    ///     redefinition-resolution regression, not full-fixture zero-warning coverage.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_1cPartsTreeRedefinitionFixture_NoUnresolvedReferenceWarnings()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "validation", "01-PartsTree", "1c-PartsTreeRedefinition.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        string[] previouslyFalseUnresolvedNames =
        [
            "frontAxleAssembly", "frontAxle", "rearAxleAssembly", "rearAxle", "frontWheel", "rearWheel",
        ];
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                 d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal) &&
                 previouslyFalseUnresolvedNames.Any(name => d.Message.Contains($"'{name}'", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     A bare-name <c>redefines feat</c> where the ancestor declaring <c>feat</c> is two
    ///     supertype hops away (<c>Mid :> Parent :> GrandParent</c>), and the whole chain is
    ///     declared <em>out of document order</em> (<c>Mid</c> first, then <c>Parent</c>, then
    ///     <c>GrandParent</c>) — reproducing the reported single-pass hazard exactly: under the
    ///     old inline-fallback implementation, <c>Mid</c>'s bare-name walk ran before
    ///     <c>Parent</c>/<c>GrandParent</c> had been visited by the same DFS pass, so their
    ///     <c>ResolvedEdges</c> were still empty and the walk silently failed, producing a false
    ///     "Unresolved reference" warning even though the reference is semantically valid. The
    ///     corrected two-pass resolution must still produce the <c>Redefinition</c> edge and no
    ///     warning regardless of this declaration order.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_OutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Mid :> Parent {
                    attribute value : Real redefines feat;
                }
                part def Parent :> GrandParent;
                part def GrandParent {
                    attribute feat : Real;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Redefinition &&
                     e.SourceQualifiedName == "Mid::value" &&
                     e.TargetQualifiedName == "GrandParent::feat");
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("feat"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The same out-of-order ancestor-chain shape as
    ///     <see cref="WorkspaceLoader_LoadAsync_OutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning"/>,
    ///     but split across two files, with the file containing the redefining feature listed
    ///     <em>before</em> the file containing its ancestors in <see cref="WorkspaceLoader.LoadAsync(IEnumerable{string}, SymbolTable?)"/>'s
    ///     file-path array — exercising the cross-file variant of the same document-order hazard,
    ///     since pass 1 iterates file roots in call order.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CrossFileOutOfOrderRedefinitionChain_RecordsRedefinitionEdgeNoWarning()
    {
        // Arrange
        var descendantFile = Path.GetTempFileName() + ".sysml";
        var ancestorsFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(descendantFile, """
                part def Mid :> Parent {
                    attribute value : Real redefines feat;
                }
                """, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(ancestorsFile, """
                part def Parent :> GrandParent;
                part def GrandParent {
                    attribute feat : Real;
                }
                """, TestContext.Current.CancellationToken);

            // Act — descendant file listed first, ancestors file listed second
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([descendantFile, ancestorsFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Redefinition &&
                     e.SourceQualifiedName == "Mid::value" &&
                     e.TargetQualifiedName == "GrandParent::feat");
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("feat"));
        }
        finally
        {
            File.Delete(descendantFile);
            File.Delete(ancestorsFile);
        }
    }

    /// <summary>
    ///     A usage/feature node's usage-level <c>subsets</c>/<c>:&gt;</c> specialization (as
    ///     opposed to a definition-level <c>part def X :> Y</c> supertype) should directly
    ///     populate that feature node's <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlNode.SupertypeNames"/>
    ///     with the expected target name, and produce a resolved <c>Supertype</c> edge — a direct
    ///     assertion of the usage-level capture behavior, independent of any redefinition context
    ///     (previously only indirectly covered via the absence of a false warning in the OMG
    ///     fixture regression tests above).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UsageLevelSubsetting_PopulatesSupertypeNames()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def Thing {
                    part y : Thing;
                    part x : Thing subsets y;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var thingNode = result.Workspace.Declarations["Thing"];
            var xNode = Assert.Single(thingNode.Children, c => c.Name == "x");
            Assert.Contains("y", xNode.SupertypeNames);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype &&
                     e.SourceQualifiedName == "Thing::x" &&
                     e.TargetQualifiedName == "Thing::y");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A wildcard import (<c>import Other::*;</c>) should be recorded as an <c>Import</c>
    ///     edge whose target is the imported namespace, queryable via
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SemanticIndex"/>'s
    ///     incoming-edge lookup.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_WildcardImport_RecordsImportEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package Other { part def Thing {} }
                package Consumer {
                    import Other::*;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var incoming = result.Workspace.Index.GetIncomingEdges("Other");
            Assert.Contains(incoming, e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Import);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A named import (<c>import Other::Thing;</c>) should be recorded as an <c>Import</c>
    ///     edge whose target is the fully-qualified imported member.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NamedImport_RecordsImportEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package Other { part def Thing {} }
                package Consumer {
                    import Other::Thing;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var incoming = result.Workspace.Index.GetIncomingEdges("Other::Thing");
            Assert.Contains(incoming, e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Import);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A supertype referenced by its short (unqualified) name, resolved via an enclosing
    ///     namespace scope, should produce a <c>Supertype</c> edge whose target is the
    ///     fully-qualified name, not the raw short name.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SupertypeAcrossEnclosingNamespace_RecordsResolvedTargetName()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package A {
                    part def Foo {}
                    part def Baz specializes Foo {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — the edge's target must be the fully-qualified "A::Foo", not raw "Foo"
            Assert.NotNull(result.Workspace);
            var outgoing = result.Workspace.Index.GetOutgoingEdges("A::Baz");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype &&
                     e.TargetQualifiedName == "A::Foo");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An import referencing a nonexistent namespace should produce a Warning diagnostic and
    ///     must not crash <see cref="WorkspaceLoader.LoadAsync(IEnumerable{string}, SymbolTable?)"/>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedImport_ProducesWarningNoCrash()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    import NonExistentNs::Thing;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("NonExistentNs"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A small fixture model combining a package hierarchy, a specialization, a typed
    ///     feature, and a wildcard import should let the workspace's
    ///     <see cref="SysmlWorkspace.Index"/> answer both incoming and outgoing edge queries
    ///     correctly for each node kind.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MultiKindFixtureModel_IndexAnswersIncomingAndOutgoingQueries()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package Lib {
                    part def Ancestor {}
                    part def Widget {}
                }
                package App {
                    import Lib::*;
                    part def Gadget specializes Ancestor {
                        part core : Widget;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var index = result.Workspace.Index;

            // Supertype edge, both directions
            Assert.Contains(index.GetOutgoingEdges("App::Gadget"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype &&
                     e.TargetQualifiedName == "Lib::Ancestor");
            Assert.Contains(index.GetIncomingEdges("Lib::Ancestor"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype &&
                     e.SourceQualifiedName == "App::Gadget");

            // Typing edge, both directions
            Assert.Contains(index.GetOutgoingEdges("App::Gadget::core"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "Lib::Widget");
            Assert.Contains(index.GetIncomingEdges("Lib::Widget"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.SourceQualifiedName == "App::Gadget::core");

            // Import edge, incoming direction (anonymous import node has no source)
            Assert.Contains(index.GetIncomingEdges("Lib"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Import &&
                     e.SourceQualifiedName == null);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>satisfy X by Y;</c> usage with both the requirement and subject resolvable should
    ///     be recorded as a <c>Satisfy</c> edge from the resolved subject to the resolved
    ///     requirement.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SatisfyByName_RecordsSatisfyEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement req : R;
                    part def Q {}
                    part subj : Q;
                    satisfy req by subj;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy &&
                     e.SourceQualifiedName == "P::subj" &&
                     e.TargetQualifiedName == "P::req");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>satisfy</c> usage whose subject cannot be resolved should produce a Warning
    ///     diagnostic and must not produce a <c>Satisfy</c> edge (no partial edge).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SatisfyUnresolvedSubject_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement req : R;
                    satisfy req by nonExistentSubject;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("nonExistentSubject"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>satisfy</c> usage whose requirement cannot be resolved should produce a Warning
    ///     diagnostic and must not produce a <c>Satisfy</c> edge (no partial edge).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SatisfyUnresolvedRequirement_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part subj : Q;
                    satisfy nonExistentReq by subj;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("nonExistentReq"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>satisfy</c> usage whose subject is a dotted feature chain (e.g. <c>a.b</c>) should
    ///     gracefully fail to resolve (no crash, Warning diagnostic, no edge) rather than crashing —
    ///     dotted feature-chain resolution is out of scope for this unit.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SatisfyFeatureChainSubject_GracefullyUnresolved()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement req : R;
                    part def Q {
                        part sub;
                    }
                    part container : Q;
                    satisfy req by container.sub;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert — graceful: no crash, a Warning diagnostic, and no Satisfy edge
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("container.sub"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>verify &lt;ref&gt;;</c> member (the redefine/reference form,
    ///     <c>ownedReferenceSubsetting</c>) nested directly in a requirement usage's body should be
    ///     recorded as a <c>Verify</c> edge from the owning requirement usage to the resolved
    ///     referenced requirement.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_VerifyOwnedReferenceSubsetting_RecordsVerifyEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement req : R;
                    requirement outer {
                        verify req;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify &&
                     e.SourceQualifiedName == "P::outer" &&
                     e.TargetQualifiedName == "P::req");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>verify requirement &lt;name&gt; : &lt;Type&gt;;</c> member (the typed-placeholder
    ///     form) nested directly in a requirement usage's body should be recorded as a
    ///     <c>Verify</c> edge to the resolved type.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_VerifyTypedRequirementPlaceholder_RecordsVerifyEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement outer {
                        verify requirement placeholder : R;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify &&
                     e.SourceQualifiedName == "P::outer" &&
                     e.TargetQualifiedName == "P::R");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>verify</c> member nested inside an <c>objective</c> member of a <c>case def</c>
    ///     (the real-world <c>verification def MassTest { objective ... { verify ... } }</c>
    ///     pattern used by OMG's <c>9-Verification-simplified.sysml</c> fixture) should still be
    ///     found and recorded as a <c>Verify</c> edge, exercising the narrow recursive
    ///     verification-member finder rather than the direct requirement-usage-body path.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_VerifyNestedInObjectiveMember_RecordsVerifyEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    case def C {
                        objective obj {
                            verify requirement placeholder : R;
                        }
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify &&
                     e.SourceQualifiedName == "P::C" &&
                     e.TargetQualifiedName == "P::R");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>verify</c> member whose referenced requirement cannot be resolved should produce a
    ///     Warning diagnostic and must not produce a <c>Verify</c> edge (no partial edge), mirroring
    ///     the equivalent unresolved-reference tests for <c>satisfy</c>/<c>allocate</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_VerifyUnresolvedReference_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement outer {
                        verify nonExistentReq;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("nonExistentReq"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An <c>allocate A to B;</c> usage with both ends resolvable should be recorded as an
    ///     <c>Allocate</c> edge from the resolved first end to the resolved second end, reusing
    ///     the connector-part endpoint extraction shared with <c>connectionUsage</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AllocateBinaryEnds_RecordsAllocateEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    part b : Q;
                    allocate a to b;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Allocate &&
                     e.SourceQualifiedName == "P::a" &&
                     e.TargetQualifiedName == "P::b");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An <c>allocate</c> usage with one unresolvable end should produce a Warning diagnostic
    ///     and must not produce an <c>Allocate</c> edge (no partial edge, no crash).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AllocateUnresolvedEnd_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    allocate a to nonExistentEnd;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("nonExistentEnd"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Allocate);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>dependency A to B;</c> declaration with both ends resolvable should be recorded as
    ///     a <c>Dependency</c> edge from the resolved client to the resolved supplier.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DependencyBinaryEnds_RecordsDependencyEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    part b : Q;
                    dependency a to b;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Dependency &&
                     e.SourceQualifiedName == "P::a" &&
                     e.TargetQualifiedName == "P::b");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>dependency</c> declaration with comma-separated client and supplier lists should
    ///     produce one <c>Dependency</c> edge per resolved (client, supplier) pair (cross product).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DependencyCommaLists_RecordsCrossProductEdges()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    part b : Q;
                    part c : Q;
                    part d : Q;
                    dependency a, b to c, d;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var edges = result.Workspace.Index.AllEdges
                .Where(e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Dependency)
                .Select(e => (e.SourceQualifiedName, e.TargetQualifiedName))
                .ToList();
            Assert.Equal(4, edges.Count);
            Assert.Contains(("P::a", "P::c"), edges);
            Assert.Contains(("P::a", "P::d"), edges);
            Assert.Contains(("P::b", "P::c"), edges);
            Assert.Contains(("P::b", "P::d"), edges);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>dependency</c> declaration with an unresolvable end should produce a Warning
    ///     diagnostic and must not produce a <c>Dependency</c> edge (no partial edge, no crash).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DependencyUnresolvedEnd_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    dependency a to nonExistentEnd;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("nonExistentEnd"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Dependency);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>bind a.x = b.y;</c> binding connector usage with a resolvable dotted feature chain
    ///     on both sides should be recorded as a <c>Binding</c> edge between the resolved features,
    ///     reusing the same dotted-feature-chain walk as <c>connect</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BindingDottedChain_RecordsBindingEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Sensor {
                        port x;
                    }
                    part def Display {
                        port y;
                    }
                    part def Q {
                        part a : Sensor;
                        part b : Display;
                        bind a.x = b.y;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Binding &&
                     e.SourceQualifiedName == "P::Q::a::x" &&
                     e.TargetQualifiedName == "P::Q::b::y");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>bind</c> endpoint that names an implicitly-named usage (a nested
    ///     <c>port redefines fuelTankPort { ... }</c> with no name token of its own) should still
    ///     resolve and produce a <c>Binding</c> edge, because such a usage's implicit name is the
    ///     name of the feature it redefines (SysML v2 semantics) — <c>AstBuilder.BuildUsageNode</c>'s
    ///     <c>effectiveName</c> fallback derives it from <c>RedefinedFeatureName</c> rather than
    ///     leaving the usage permanently unnamed and unresolvable. Mirrors the real-world OMG
    ///     corpus fixture shape (<c>BindingConnectorsExample-1.sysml</c> / <c>PortExample.sysml</c>)
    ///     in isolation, independent of the external corpus.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BindingViaImplicitlyNamedRedefinedUsage_RecordsBindingEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    port def FuelOutPort {
                        item fuelSupply;
                    }
                    part def Tank {
                        port fuelTankPort : FuelOutPort;
                    }
                    part def Pump {
                        item pumpOut;
                    }
                    part def Vehicle {
                        part tank : Tank {
                            port redefines fuelTankPort {
                                item redefines fuelSupply;
                            }
                        }
                        part pump : Pump;
                        bind tank.fuelTankPort.fuelSupply = pump.pumpOut;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Binding &&
                     e.SourceQualifiedName == "P::Vehicle::tank::fuelTankPort::fuelSupply" &&
                     e.TargetQualifiedName == "P::Vehicle::pump::pumpOut");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>bind</c> usage referencing an unresolvable feature chain end should produce a
    ///     Warning diagnostic and must not produce a <c>Binding</c> edge (no partial edge, no
    ///     crash).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BindingUnresolvedEnd_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Sensor {
                        port x;
                    }
                    part def Q {
                        part a : Sensor;
                        bind a.x = nonExistentEnd.y;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("nonExistentEnd"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Binding);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A fixture combining <c>satisfy</c>, <c>verify</c>, and <c>allocate</c> should let the
    ///     workspace's <see cref="SysmlWorkspace.Index"/> answer both incoming and outgoing edge
    ///     queries correctly for all three new edge kinds, mirroring unit 1's reverse-index test.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TraceEdges_ReverseIndexAnswersIncomingOutgoing()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    requirement def R;
                    requirement req : R;
                    part def Q {}
                    part subj : Q;
                    satisfy req by subj;

                    case def C {
                        objective obj {
                            verify requirement placeholder : R;
                        }
                    }

                    part a : Q;
                    part b : Q;
                    allocate a to b;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var index = result.Workspace.Index;

            // Satisfy edge, both directions
            Assert.Contains(index.GetOutgoingEdges("P::subj"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy &&
                     e.TargetQualifiedName == "P::req");
            Assert.Contains(index.GetIncomingEdges("P::req"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy &&
                     e.SourceQualifiedName == "P::subj");

            // Verify edge, both directions
            Assert.Contains(index.GetOutgoingEdges("P::C"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify &&
                     e.TargetQualifiedName == "P::R");
            Assert.Contains(index.GetIncomingEdges("P::R"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify &&
                     e.SourceQualifiedName == "P::C");

            // Allocate edge, both directions
            Assert.Contains(index.GetOutgoingEdges("P::a"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Allocate &&
                     e.TargetQualifiedName == "P::b");
            Assert.Contains(index.GetIncomingEdges("P::b"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Allocate &&
                     e.SourceQualifiedName == "P::a");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>connect A to B;</c> usage whose endpoints are both plain (single-segment) feature
    ///     references should be recorded as a <c>Connect</c> edge from the resolved first endpoint
    ///     to the resolved second endpoint.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionSingleSegmentEndpoints_RecordsConnectEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    part b : Q;
                    connect a to b;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::a" &&
                     e.TargetQualifiedName == "P::b");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>connect</c> usage with a 2-segment endpoint chain (e.g. <c>engine.fuelPort</c>)
    ///     where <c>fuelPort</c> is a direct (inline) child of <c>engine</c> should resolve via
    ///     the direct-child lookup path, into a <c>Connect</c> edge targeting the port's
    ///     qualified name.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionTwoSegmentChain_ResolvesViaDirectChild()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part vehicle {
                        part engine {
                            port fuelPort;
                        }
                        part transmission {
                            port input;
                        }
                        connect engine.fuelPort to transmission.input;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::vehicle::engine::fuelPort" &&
                     e.TargetQualifiedName == "P::vehicle::transmission::input");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>connect</c> usage with a 2-segment endpoint chain where the first segment's
    ///     usage has no inline body (only a <c>Typing</c> reference) should resolve the second
    ///     segment via the typing-fallback path (the referenced definition's direct child).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionTwoSegmentChain_ResolvesViaTypingFallback()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Engine {
                        port fuelCmdPort;
                    }
                    part def Transmission {
                        port input;
                    }
                    part vehicle {
                        part engine : Engine;
                        part transmission : Transmission;
                        connect engine.fuelCmdPort to transmission.input;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::vehicle::engine::fuelCmdPort" &&
                     e.TargetQualifiedName == "P::vehicle::transmission::input");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A 3-segment endpoint chain mixing a direct-child first hop with a typing-fallback
    ///     second hop (e.g. <c>rearAxle.leftHalfAxle.axleToWheelPort</c>, mirroring the
    ///     <c>2a-PartsInterconnection.sysml</c> fixture shape) should resolve end to end.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionThreeSegmentChain_MixesDirectChildAndTypingFallback()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def HalfAxle {
                        port axleToWheelPort;
                    }
                    part def Wheel {
                        port wheelToAxlePort;
                    }
                    part rearAxle {
                        part leftHalfAxle : HalfAxle;
                    }
                    part leftWheel : Wheel;
                    connect rearAxle.leftHalfAxle.axleToWheelPort to leftWheel.wheelToAxlePort;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::rearAxle::leftHalfAxle::axleToWheelPort" &&
                     e.TargetQualifiedName == "P::leftWheel::wheelToAxlePort");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A chain segment that is only reachable via an inherited (<c>Supertype</c>-chain)
    ///     feature should resolve by walking the supertype chain, not just the immediate type's
    ///     own direct children.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionChain_ResolvesInheritedFeatureViaSupertype()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def AxleAssembly {
                        port shaftPort;
                    }
                    part def RearAxleAssembly :> AxleAssembly {}
                    part def Wheel {
                        port wheelToAxlePort;
                    }
                    part rearAxleAssembly : RearAxleAssembly;
                    part leftWheel : Wheel;
                    connect rearAxleAssembly.shaftPort to leftWheel.wheelToAxlePort;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::rearAxleAssembly::shaftPort" &&
                     e.TargetQualifiedName == "P::leftWheel::wheelToAxlePort");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The mirror image of <see cref="WorkspaceLoader_LoadAsync_ConnectionThreeSegmentChain_MixesDirectChildAndTypingFallback"/>:
    ///     a 3-segment chain where the *first* hop resolves via typing fallback (<c>rearAxle</c> has
    ///     no own <c>leftHalfAxle</c> usage, so it is found on <c>Axle</c>'s own hierarchy) and the
    ///     *second* hop is then a direct child of that type-declared node (<c>Axle::leftHalfAxle</c>
    ///     has its own inline <c>axleToWheelPort</c> nested directly beneath it). Once a chain has
    ///     entered type-fallback territory, every remaining segment is still only reachable relative
    ///     to the type, not the instance — even one that is itself a "direct child" match — so the
    ///     final qualified name must remain instance-relative (<c>P::rearAxle::leftHalfAxle::axleToWheelPort</c>)
    ///     rather than collapsing back to the type's own declared path
    ///     (<c>P::Axle::leftHalfAxle::axleToWheelPort</c>) once the direct-child hop occurs.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionThreeSegmentChain_DirectChildAfterTypingFallbackStaysInstanceRelative()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def HalfAxle {
                        port axleToWheelPort;
                    }
                    part def Axle {
                        part leftHalfAxle : HalfAxle {
                            port axleToWheelPort;
                        }
                    }
                    part def Wheel {
                        port wheelToAxlePort;
                    }
                    part rearAxle : Axle;
                    part leftWheel : Wheel;
                    connect rearAxle.leftHalfAxle.axleToWheelPort to leftWheel.wheelToAxlePort;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::rearAxle::leftHalfAxle::axleToWheelPort" &&
                     e.TargetQualifiedName == "P::leftWheel::wheelToAxlePort");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The dominant real-world <c>connect</c> shape: two sibling features (ports) declared
    ///     directly in their owning <c>part def</c>s, referenced from an enclosing part via bare
    ///     <c>part</c> usages with no per-instance nested redeclaration. Both endpoints resolve via
    ///     the typing-fallback branch, and each must produce a distinct, instance-relative qualified
    ///     name (<c>Drone::controller::power</c>, <c>Drone::battery::output</c>) rather than
    ///     collapsing to the shared port type's own declared path — the root-cause regression this
    ///     fix addresses.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionDominantShape_ResolvesDistinctInstancePaths()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def PowerPort;
                    part def FlightController {
                        port power : PowerPort;
                    }
                    part def Battery {
                        port output : PowerPort;
                    }
                    part def Drone {
                        part controller : FlightController;
                        part battery : Battery;
                        connect controller.power to battery.output;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::Drone::controller::power" &&
                     e.TargetQualifiedName == "P::Drone::battery::output");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>connect</c> usage whose second endpoint's last chain segment does not exist on
    ///     either the immediate type or any supertype should produce a Warning diagnostic and no
    ///     <c>Connect</c> edge (graceful failure, no crash).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionUnresolvedEndpoint_ProducesWarningNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Engine {
                        port fuelCmdPort;
                    }
                    part def Transmission {}
                    part engine : Engine;
                    part transmission : Transmission;
                    connect engine.fuelCmdPort to transmission.nonExistentPort;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Diagnostics,
                d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning &&
                     d.Message.Contains("transmission.nonExistentPort"));
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>message</c> (<c>ConnectionKeyword == "message"</c>) usage whose from/to events
    ///     both resolve should be recorded as a <c>Connect</c> edge, the same edge kind used for
    ///     <c>connection</c> endpoints.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MessageEndpoints_RecordsConnectEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Q {}
                    part a : Q;
                    part b : Q;
                    message msg from a to b;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect &&
                     e.SourceQualifiedName == "P::a" &&
                     e.TargetQualifiedName == "P::b");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A transition with both a <c>Source</c> and <c>Target</c> that resolve to sibling
    ///     states should be recorded as a <c>Transition</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TransitionSourceAndTarget_RecordsTransitionEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    state def Behavior {
                        state start;
                        state off;
                        first start then off;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::start" &&
                     e.TargetQualifiedName == "P::Behavior::off");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A previously-broken attached-transition shape: <c>state off; accept Signal via
    ///     requestPort then off;</c> is the <c>stateBodyItem: (sourceSuccessionMember)?
    ///     behaviorUsageMember (targetTransitionUsageMember)*</c> grammar alternative, whose
    ///     transition's <c>Source</c> is implicitly the immediately preceding <c>state off;</c>
    ///     usage. Before the attached-transition fix this silently dropped both the state and the
    ///     transition (ANTLR's default <c>VisitChildren</c> keeps only the last child); now both
    ///     are preserved and the transition resolves to a genuine self-loop edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AttachedTransitionAfterState_ResolvesSelfLoopEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    item def Signal;
                    state def Behavior {
                        port requestPort;
                        state off;
                        accept Signal via requestPort
                            then off;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::off" &&
                     e.TargetQualifiedName == "P::Behavior::off");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A transition attached after a genuinely unnamed/anonymous preceding usage (an anonymous
    ///     <c>action;</c>, which <c>VisitActionUsage</c> intentionally returns <see langword="null"/>
    ///     for) has no name to serve as the attached transition's implicit <c>Source</c>. This is
    ///     the one remaining documented limitation: no crash, no partial edge, just nothing
    ///     recorded for that body item.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TransitionImpliedSource_ProducesNoEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    item def Signal;
                    state def Behavior {
                        port requestPort;
                        action;
                        accept Signal via requestPort
                            then off;
                        state off;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Two attached transitions after the same preceding state usage (repeated
    ///     <c>targetTransitionUsageMember</c>) should both be captured, both with the preceding
    ///     usage's name as their implicit <c>Source</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MultipleAttachedTransitionsAfterState_CapturesAll()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    item def Sig1;
                    item def Sig2;
                    state def Behavior {
                        state a;
                        accept Sig1 then b;
                        accept Sig2 then c;
                        state b;
                        state c;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::a" &&
                     e.TargetQualifiedName == "P::Behavior::b");
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::a" &&
                     e.TargetQualifiedName == "P::Behavior::c");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The <c>entryActionMember (entryTransitionMember)*</c> attached-transition shape (e.g.
    ///     <c>entry action initial; then off;</c>) should capture both the named entry-action
    ///     feature and its attached transition, whose implicit <c>Source</c> is the entry action's
    ///     declared name.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_EntryActionWithAttachedTransition_CapturesEntryFeatureAndTransition()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    state def Behavior {
                        entry action initial;
                        then off;
                        state off;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::initial" &&
                     e.TargetQualifiedName == "P::Behavior::off");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The OMG spec's Annex A.7-preferred style — a named entry action declared once, then
    ///     referenced from a separate explicit <c>transition</c> statement (rather than an
    ///     attached <c>entryTransitionMember</c>) — should register a resolvable feature: no
    ///     "Unresolved reference: 'initial'" diagnostic should be produced.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NamedEntryAction_RegistersResolvableFeature()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    state def Behavior {
                        entry action initial;
                        state off;
                        transition initial then off;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Message.Contains("Unresolved reference: 'initial'", StringComparison.Ordinal));
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::initial" &&
                     e.TargetQualifiedName == "P::Behavior::off");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The unnamed reference-subsetting form of an entry action (e.g.
    ///     <c>entry performSelfTest;</c>, which subsets/references an existing behavior rather
    ///     than declaring a new named feature) should still register a feature node (with
    ///     <c>Name</c> null) and must not throw.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnnamedEntryActionReferenceForm_NoNameNoCrash()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    action performSelfTest;
                    state def Behavior {
                        state on {
                            entry performSelfTest;
                        }
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     <c>VisitStateUsage</c> must record a <c>Typing</c> edge for a state usage's explicit
    ///     feature typing (e.g. <c>state usage : X { ... }</c>) — previously dropped entirely,
    ///     unlike every other usage kind's <c>BuildUsageNode</c> handling.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StateUsageWithExplicitTyping_RecordsTypingEdge()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    state def X;
                    state usage : X {
                        first start then y;
                        state y;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing &&
                     e.SourceQualifiedName == "P::usage" &&
                     e.TargetQualifiedName == "P::X");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A transition's implicit initial-pseudostate <c>Source</c> (<c>first start then y;</c>)
    ///     must resolve <c>start</c> to the real stdlib member every state definition/usage
    ///     inherits from <c>Actions::Action</c> (<c>action start: Action :&gt;&gt; startShot</c>),
    ///     via <c>ReferenceResolver.TryResolveInheritedActionMember</c>'s narrow fallback — even
    ///     though the user's own <c>state def X;</c> declares no explicit supertype at all (this
    ///     codebase implements no general implicit-generalization/default-supertype inference).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TransitionSourceStartFeature_ResolvesToStdlibActionMember()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    state def X;
                    state usage : X {
                        first start then y;
                        state y;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Diagnostics,
                d => d.Message.Contains("Unresolved reference: 'start'", StringComparison.Ordinal));
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "Actions::Action::start" &&
                     e.TargetQualifiedName == "P::usage::y");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A pathological, self-referential supertype cycle (<c>A :&gt; B :&gt; A</c>) must not
    ///     cause <c>FindMemberInTypeHierarchy</c>'s recursive supertype walk to hang or stack
    ///     overflow; the cycle guard should simply terminate the walk with no match found (no
    ///     edge, no crash).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionChain_SupertypeCycleTerminatesGracefully()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def A :> B;
                    part def B :> A;
                    part def Q {}
                    part x : A;
                    part y : Q;
                    connect x.nonExistentMember to y;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var cancellationToken = TestContext.Current.CancellationToken;
            var loadTask = WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            var completed = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));

            // Assert — the load must complete (not hang), and must not crash or produce an edge
            Assert.Same(loadTask, completed);
            var result = await loadTask;
            Assert.NotNull(result.Workspace);
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loading the OMG <c>09.Connections/ConnectionsExample.sysml</c> training fixture should
    ///     resolve its 3-segment <c>connect [0..1] lugBoltJoints to [1] wheel.w.mountingHoles;</c>
    ///     chain (all direct-child hops) into a <c>Connect</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConnectionsExampleFixture_RecordsConnectEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(modelsRoot, "OMG", "training", "09.Connections", "ConnectionsExample.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect);
    }

    /// <summary>
    ///     Loading the OMG <c>2a-PartsInterconnection.sysml</c> validation fixture should resolve
    ///     multiple real-world connection chains (direct-child, typing-fallback, and mixed
    ///     3-segment forms such as <c>rearAxle.leftHalfAxle.axleToWheelPort</c>) into <c>Connect</c>
    ///     edges.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_2aPartsInterconnectionFixture_RecordsConnectEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "validation", "02-PartsInterconnection", "2a-PartsInterconnection.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect);
    }

    /// <summary>
    ///     Loading the OMG <c>2c-PartsInterconnection-MultipleDecompositions.sysml</c> validation
    ///     fixture should resolve at least one of its <c>connect</c> chains (e.g. <c>connect
    ///     c1.pa to c2.pc;</c>, both direct children of <c>b11</c>) into a <c>Connect</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_2cPartsInterconnectionFixture_RecordsConnectEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot,
            "OMG", "validation", "02-PartsInterconnection", "2c-PartsInterconnection-MultipleDecompositions.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Connect);
    }

    /// <summary>
    ///     Loading the OMG <c>23.StateDefinitions/StateDefinitionExample-1.sysml</c> training
    ///     fixture should resolve its named <c>transition off_to_starting first off ... then
    ///     starting;</c> (both a sibling-state source and target) into a <c>Transition</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StateDefinitionExampleFixture_RecordsTransitionEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "training", "23.StateDefinitions", "StateDefinitionExample-1.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition);
    }

    /// <summary>
    ///     Loading the full OMG <c>32.Requirements</c> training fixture set (which spans multiple
    ///     files linked by wildcard imports) should resolve the <c>satisfy</c> usages in
    ///     <c>RequirementSatisfaction.sysml</c> into at least one <c>Satisfy</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RequirementSatisfactionFixture_RecordsSatisfyEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixtureDir = Path.Combine(modelsRoot, "OMG", "training", "32.Requirements");
        if (!Directory.Exists(fixtureDir))
        {
            return;
        }

        var fixtureFiles = Directory.GetFiles(fixtureDir, "*.sysml");

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(fixtureFiles, stdlibTable);

        // Assert — smoke test: at least one Satisfy edge is present (exact resolved names are not
        // pinned, since the fixture spans multiple files and packages)
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy);
    }

    /// <summary>
    ///     Loading the OMG <c>8-Requirements.sysml</c> validation fixture should resolve the named
    ///     <c>satisfy 'vehicle1-c1 Specification' by vehicle1_c1;</c> usage (a named requirement
    ///     <em>usage</em> target, exercising the new minimal requirement-usage visitor) into at
    ///     least one <c>Satisfy</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_8RequirementsFixture_RecordsSatisfyEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(modelsRoot, "OMG", "validation", "08-Requirements", "8-Requirements.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Satisfy);
    }

    /// <summary>
    ///     Loading the OMG <c>12b-Allocation.sysml</c> validation fixture should resolve the
    ///     top-level <c>allocate torqueGenerator to powerTrain { ... }</c> usage into at least one
    ///     <c>Allocate</c> edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_12bAllocationFixture_RecordsAllocateEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "validation", "12-DependencyRelationships", "12b-Allocation.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Allocate);
    }

    /// <summary>
    ///     Loading the OMG <c>9-Verification-simplified.sysml</c> validation fixture should
    ///     resolve the <c>verify requirement massRequirement : MassRequirement;</c> member nested
    ///     inside <c>verification def MassTest</c>'s <c>objective</c> into at least one
    ///     <c>Verify</c> edge. (Not <c>34.Verification/VerificationCaseUsageExample.sysml</c> —
    ///     that fixture contains no <c>verify</c> keyword at all, only a case <em>usage</em>.)
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_9VerificationSimplifiedFixture_RecordsVerifyEdge()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "validation", "09-Verification", "9-Verification-simplified.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Verify);
    }

    /// <summary>
    ///     An element with a single <c>comment</c> member and no <c>doc</c> captures one
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Comment"/>
    ///     annotation and no others.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CommentOnly_CapturesCommentAnnotation()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    comment /* a note about P */
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var package = result.Workspace.Declarations["P"];
            var annotation = Assert.Single(package.Annotations);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Comment, annotation.Kind);
            Assert.Equal(" a note about P ", annotation.Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An element with a single <c>doc</c> member and no <c>comment</c> captures one
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation"/>
    ///     annotation and no others.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DocumentationOnly_CapturesDocumentationAnnotation()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    doc /* documentation about P */
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var package = result.Workspace.Declarations["P"];
            var annotation = Assert.Single(package.Annotations);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation, annotation.Kind);
            Assert.Equal(" documentation about P ", annotation.Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An element with both a <c>comment</c> and a <c>doc</c> member captures both
    ///     annotations, in source order.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CommentAndDocumentation_CapturesBothInSourceOrder()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    comment /* first: a comment */
                    doc /* second: a doc */
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var package = result.Workspace.Declarations["P"];
            Assert.Equal(2, package.Annotations.Count);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Comment, package.Annotations[0].Kind);
            Assert.Equal(" first: a comment ", package.Annotations[0].Text);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation, package.Annotations[1].Kind);
            Assert.Equal(" second: a doc ", package.Annotations[1].Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An element with no <c>comment</c>/<c>doc</c> members has an empty (never null)
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlNode.Annotations"/> list.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NoAnnotations_AnnotationsIsEmptyNotNull()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, "package P {}", TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var package = result.Workspace.Declarations["P"];
            Assert.NotNull(package.Annotations);
            Assert.Empty(package.Annotations);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Multi-line comment/documentation free text is preserved verbatim, including interior
    ///     newlines and leading <c>*</c> bullet characters, with only the delimiters removed.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MultiLineAnnotation_PreservesTextVerbatim()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                "package P {\n" +
                "    doc /* line one\n" +
                "     * line two\n" +
                "     */\n" +
                "}\n", TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var package = result.Workspace.Declarations["P"];
            var annotation = Assert.Single(package.Annotations);
            Assert.Equal(" line one\n     * line two\n     ", annotation.Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loading the OMG <c>DocumentationExample.sysml</c> training fixture captures the
    ///     package-level and part-def-level <c>doc</c> annotation text verbatim on the
    ///     corresponding nodes, exercising the real ANTLR grammar/lexer path end-to-end.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DocumentationExampleFixture_CapturesExpectedDocText()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(modelsRoot, "OMG", "training", "01.Packages", "DocumentationExample.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert — package-level doc
        Assert.NotNull(result.Workspace);
        var package = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlPackageNode>(
            result.Workspace.Declarations["'Documentation Example'"]);
        var packageDoc = Assert.Single(package.Annotations);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation, packageDoc.Kind);
        Assert.Contains("This is documentation of the owning", packageDoc.Text);
        Assert.Contains("package.", packageDoc.Text);

        // Assert — part-def-level named doc
        var automobile = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
            result.Workspace.Declarations["'Documentation Example'::Automobile"]);
        var automobileDoc = Assert.Single(automobile.Annotations);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation, automobileDoc.Kind);
        Assert.Equal(" This documentation of Automobile. ", automobileDoc.Text);
    }

    /// <summary>
    ///     A <c>view</c> usage and a <c>view def</c> each record their declaring file path and the
    ///     one-based line and one-based column of their declaration, so view-level diagnostics
    ///     can be located in the source.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_Views_CaptureSourceLocation()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    view def VD {}
                    view V {
                        expose P;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var viewDef = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::VD"]);
            Assert.Equal(tempFile, viewDef.SourceFilePath);
            Assert.Equal(2, viewDef.SourceLine);
            Assert.Equal(5, viewDef.SourceColumn);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Equal(tempFile, view.SourceFilePath);
            Assert.Equal(3, view.SourceLine);
            Assert.Equal(5, view.SourceColumn);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     AstBuilder records each membership's declared visibility (public by default; imports
    ///     private by default), short name, and alias memberships with their target text.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_VisibilityShortNameAndAlias_Captured()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    private part def <h> Hidden;
                    part def Shown;
                    import Other::*;
                    public import Other::Thing;
                    alias ShownAlias for Shown;
                    private alias HiddenAlias for Shown;
                }
                package Other { part def Thing; }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var hidden = result.Workspace.Declarations["P::Hidden"];
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlVisibility.Private, hidden.Visibility);
            Assert.Equal("h", hidden.ShortName);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlVisibility.Public, result.Workspace.Declarations["P::Shown"].Visibility);
            var imports = result.Workspace.Declarations["P"].Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlImportNode>().ToList();
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlVisibility.Private, imports[0].Visibility);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlVisibility.Public, imports[1].Visibility);
            var aliases = result.Workspace.Declarations["P"].Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlAliasNode>().ToList();
            Assert.Equal(["ShownAlias", "HiddenAlias"], aliases.Select(a => a.Name));
            Assert.Equal("Shown", aliases[0].TargetName);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlVisibility.Public, aliases[0].Visibility);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlVisibility.Private, aliases[1].Visibility);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A simple name that is a local member's short name or a local alias resolves to that
    ///     member (KerML 8.2.3.5.2 local resolution) rather than to a same-named top-level element.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_LocalShortNameAndAlias_WinOverGlobalNames()
    {
        // Arrange: top-level k and K2 would win if local short names and aliases were ignored
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                part def k;
                part def K2;
                package P {
                    part def <k> Kind;
                    alias K2 for Kind;
                    part def A :> k;
                    part def B :> K2;
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            string SupertypeOf(string name) => Assert.Single(
                result.Workspace.Declarations[name].ResolvedEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype).TargetQualifiedName;
            Assert.Equal("P::Kind", SupertypeOf("P::A"));
            Assert.Equal("P::Kind", SupertypeOf("P::B"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A simple name declared in an enclosing namespace resolves there before a same-named
    ///     top-level element of another root namespace (here the standard library's <c>Base</c>
    ///     package), per KerML 8.2.3.5.4 full resolution.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_LocalNameShadowsGlobalLibraryName_ResolvesLocal()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P { part def Base; part def Derived :> Base; }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var edge = Assert.Single(
                result.Workspace.Declarations["P::Derived"].ResolvedEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Supertype);
            Assert.Equal("P::Base", edge.TargetQualifiedName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     When sibling packages each define and use a part def with the same simple name, each
    ///     usage's typing resolves to the definition in its own owning package (SysML v2 lexical
    ///     scoping), not to the first same-named definition in the workspace.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SameNamedDefsInSiblingPackages_ResolveToOwningPackage()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package PkgA { part def Checker; part def SystemA { part checker : Checker; } }
                package PkgB { part def Checker; part def SystemB { part checker : Checker; } }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert: each usage's Typing edge targets its own package's Checker
            Assert.NotNull(result.Workspace);
            string TypeOf(string usage) => Assert.Single(
                result.Workspace.Declarations[usage].ResolvedEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing).TargetQualifiedName;
            Assert.Equal("PkgA::Checker", TypeOf("PkgA::SystemA::checker"));
            Assert.Equal("PkgB::Checker", TypeOf("PkgB::SystemB::checker"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>view def</c> with a <c>render &lt;target&gt;;</c> member naming a rendering that the
    ///     view's namespace can see (<c>Views::*</c> is imported) produces zero diagnostics and zero
    ///     edges sourced from the view, while
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.RenderTargetName"/> is
    ///     still captured verbatim. The target is resolved (see
    ///     <see cref="WorkspaceLoader_LoadAsync_UnresolvedReference_ReportsSourcePosition"/> for the
    ///     unresolved case) but records no edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawResolvedWithoutEdge()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    private import Views::*;
                    view def V {
                        render asTreeDiagram;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Equal("asTreeDiagram", view.RenderTargetName);
            Assert.Empty(result.Diagnostics);
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.SourceQualifiedName == "P::V");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>view def</c> with a <c>filter [&lt;expr&gt;];</c> member should capture the raw
    ///     expression source text verbatim on <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.FilterExpressionText"/>
    ///     without evaluating it, producing no diagnostic and no edge (per binding decision: filter
    ///     expression evaluation is explicitly deferred future work).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewFilterExpression_CapturesTextVerbatimNoEdge()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    view def V {
                        filter @SysML::PartUsage;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Equal("@SysML::PartUsage", view.FilterExpressionText);
            Assert.Empty(result.Diagnostics);
            Assert.DoesNotContain(result.Workspace.Index.AllEdges,
                e => e.SourceQualifiedName == "P::V");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A named <c>view</c> usage (not a <c>view def</c>) with an <c>expose &lt;ns&gt;;</c>
    ///     member should build a <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode"/>
    ///     via <c>AstBuilder.VisitViewUsage</c> (the first test to exercise that visitor) and
    ///     resolve the exposed name into an <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose"/>
    ///     edge.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewUsageWithExpose_RecordsExposeEdge()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Exposed {}
                    view V {
                        expose Exposed;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Contains("Exposed", view.GetExposedNames());
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose &&
                     e.SourceQualifiedName == "P::V" &&
                     e.TargetQualifiedName == "P::Exposed");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A view with the bracketed-filter <c>expose &lt;ns&gt;::**[&lt;filterExpr&gt;];</c> form —
    ///     the dominant <c>expose</c> shape in the real OMG corpus (e.g.
    ///     <c>expose vehicle::**[@Safety];</c> in <c>11b-SafetyAndSecurityFeatureViews.sysml</c>) —
    ///     must resolve the exposed name into an
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose"/> edge. This
    ///     grammar form nests the qualified name two levels deeper than the plain form
    ///     (<c>namespaceImport -&gt; filterPackage -&gt; filterPackageImportDeclaration -&gt;
    ///     membershipImport</c>), which <c>AstBuilder.ExtractImportTarget</c> previously did not
    ///     descend into, silently dropping the exposed name with no diagnostic.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewUsageWithBracketedFilterExpose_RecordsExposeEdge()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    metadata def Safety;
                    part def Exposed {}
                    view V {
                        expose Exposed::**[@Safety];
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Contains("Exposed", view.GetExposedNames());
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose &&
                     e.SourceQualifiedName == "P::V" &&
                     e.TargetQualifiedName == "P::Exposed");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Regression guard for the plain (non-bracketed) wildcard <c>expose &lt;ns&gt;::*::**;</c>
    ///     form — the sibling grammar shape to the bracketed-filter form above — confirming it
    ///     still resolves correctly after the <c>ExtractImportTarget</c> fix that added support for
    ///     descending into <c>filterPackage()</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewUsageWithPlainWildcardExpose_RecordsExposeEdge()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def Exposed {}
                    view V {
                        expose Exposed::*::**;
                    }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Contains("Exposed", view.GetExposedNames());
            Assert.Contains(result.Workspace.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose &&
                     e.SourceQualifiedName == "P::V" &&
                     e.TargetQualifiedName == "P::Exposed");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Loading the real OMG corpus fixture
    ///     <c>11b-SafetyAndSecurityFeatureViews.sysml</c> must resolve
    ///     <c>vehicleMandatorySafetyFeatureViewStandalone</c>'s bracketed-filter
    ///     <c>expose vehicle::**[@Safety and (as Safety).isMandatory];</c> member into a non-empty
    ///     <c>GetExposedNames()</c> list, a resolved <c>Expose</c> edge to <c>vehicle</c>, and (per
    ///     Phase 2a) a paired <see cref="DemaConsulting.SysML2Tools.Semantic.Model.ExposeMember"/>
    ///     entry carrying the bracket filter's raw expression text — the exact scenario confirmed
    ///     broken (empty <c>GetExposedNames()</c>, zero edges, no diagnostic) before the
    ///     <c>ExtractImportTarget</c> fix.
    /// </summary>
    // cspell:ignore Feaure -- typo present verbatim in the real OMG corpus fixture's package name
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_OmgSafetyFeatureViewsFixture_ResolvesBracketedExpose()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var fixturePath = Path.Combine(
            modelsRoot, "OMG", "validation", "11-ViewAndViewpoint", "11b-SafetyAndSecurityFeatureViews.sysml");
        if (!File.Exists(fixturePath))
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync([fixturePath], stdlibTable);

        // Assert
        Assert.NotNull(result.Workspace);
        var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
            result.Workspace.Declarations["'11b-Safety and Security Feaure Views'::Views::vehicleMandatorySafetyFeatureViewStandalone"]);
        Assert.NotEmpty(view.GetExposedNames());
        Assert.Contains(result.Workspace.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose &&
                 e.SourceQualifiedName == "'11b-Safety and Security Feaure Views'::Views::vehicleMandatorySafetyFeatureViewStandalone" &&
                 e.TargetQualifiedName == "'11b-Safety and Security Feaure Views'::PartsTree::vehicle");
        var member = Assert.Single(view.ExposeMembers);
        Assert.Equal("@Safety and (as Safety).isMandatory", member.BracketFilterExpressionText);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.ExposeRecursionKind.NamespaceRecursive, member.RecursionKind);
    }

    /// <summary>
    ///     A <c>view def</c> with an empty body should leave <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.RenderTargetName"/>
    ///     and <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.FilterExpressionText"/>
    ///     null and <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.ExposeMembers"/>
    ///     empty — a regression guard for the "no render statement → render everything" fallback.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewEmptyBody_AllNewFieldsNullOrEmpty()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    view def V {}
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(
                result.Workspace.Declarations["P::V"]);
            Assert.Null(view.RenderTargetName);
            Assert.Null(view.FilterExpressionText);
            Assert.Empty(view.GetExposedNames());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     The compact <c>action a1; then a2;</c> idiom (the
    ///     <c>(sourceSuccessionMember)? actionBehaviorMember (actionTargetSuccessionMember)*</c>
    ///     <c>actionBodyItem</c> alternative) resolves both the action and its attached succession.
    ///     Before this fix, ANTLR's default <c>VisitChildren</c> aggregation silently dropped the
    ///     action, keeping only the succession (or vice versa).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CompactActionThenIdiom_ResolvesBothNodes()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        action a1;
                        then a2;
                        action a2;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var actions = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .Count(f => f.FeatureKeyword == "action");
            Assert.Equal(2, actions);

            var succession = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Single();
            Assert.Equal("a1", succession.Source);
            Assert.Equal("a2", succession.Target);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Multiple <c>actionTargetSuccessionMember</c>s attached after a single
    ///     <c>actionBehaviorMember</c> (e.g. a fork's three outgoing branches) are all captured,
    ///     each sharing the preceding node's name as their implicit <c>Source</c>. This also
    ///     verifies the fork's own leading <c>then</c> (<c>sourceSuccessionMember</c>) synthesizes
    ///     the implicit *incoming* succession from the immediately preceding sibling (<c>a</c>),
    ///     since the grammar's leading marker itself carries no name of its own.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_MultipleActionTargetSuccessions_CapturesAll()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        action a;
                        then fork f;
                        then b1;
                        then b2;
                        action b1;
                        action b2;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var successions = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Where(t => t.Source == "f")
                .ToList();
            Assert.Equal(2, successions.Count);
            Assert.Contains(successions, t => t.Target == "b1");
            Assert.Contains(successions, t => t.Target == "b2");

            var incoming = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Single(t => t.Target == "f");
            Assert.Equal("a", incoming.Source);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A bare <c>first start;</c> (no attached target succession) produces no succession node
    ///     — unchanged from today, since <c>ActionFlowViewLayoutStrategy</c> infers its start/done
    ///     markers purely from succession topology, not a declarative initial marker.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BareInitialNodeMember_ProducesNoSuccession()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        action a;
                        first a;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            Assert.Empty(flow.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     <c>first start then off;</c> (an <c>initialNodeMember</c> with an attached target
    ///     succession) synthesizes a <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode"/>
    ///     whose <c>Source</c> is the referenced qualified name and whose <c>Target</c> is the
    ///     attached succession's target.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_InitialNodeMemberWithAttachedSuccession_SynthesizesTransition()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        first start then off;
                        action off;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var succession = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Single();
            Assert.Equal("start", succession.Source);
            Assert.Equal("off", succession.Target);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Anonymous <c>fork</c>/<c>decide</c>/<c>join</c>/<c>merge</c>/<c>accept</c>/<c>send</c>
    ///     control nodes each register a <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode"/>
    ///     with the correct <c>FeatureKeyword</c> and a non-null synthetic <c>$</c>-prefixed
    ///     <c>Name</c> (rather than the <see langword="null"/> name left by a genuinely anonymous
    ///     plain action), since fork/decide/send are the dominant real-world idiom for anonymous
    ///     control nodes per the OMG training corpus.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AnonymousControlNodes_SynthesizeNames()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        action a;
                        then fork;
                        then b1;
                        then b2;
                        action b1;
                        action b2;
                        then join;
                        then decide;
                        if true then b1;
                        else b2;
                        then merge;
                        accept sig;
                        then send;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var features = flow.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>().ToList();

            foreach (var keyword in new[] { "fork", "join", "decide", "merge" })
            {
                var feature = features.Single(f => f.FeatureKeyword == keyword);
                Assert.NotNull(feature.Name);
                Assert.StartsWith("$", feature.Name, StringComparison.Ordinal);
                Assert.Null(feature.QualifiedName);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Named control nodes (e.g. <c>fork f;</c>, <c>join j;</c>) keep their declared name
    ///     instead of a synthesized one, and — unlike anonymous control nodes — are given a
    ///     non-null, fully-qualified <c>QualifiedName</c> (mirroring <c>BuildStateActionFeatureNode</c>'s
    ///     handling of named entry/do/exit actions), so they are correctly registered in the
    ///     symbol table and subject to expose-scope filtering.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NamedControlNodes_KeepDeclaredName()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        action a;
                        then fork f;
                        then b1;
                        then b2;
                        action b1;
                        action b2;
                        then join j;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var features = flow.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>().ToList();

            var fork = Assert.Single(features, f => f.FeatureKeyword == "fork" && f.Name == "f");
            Assert.Equal("AF::Flow::f", fork.QualifiedName);

            var join = Assert.Single(features, f => f.FeatureKeyword == "join" && f.Name == "j");
            Assert.Equal("AF::Flow::j", join.QualifiedName);

            // The named control nodes must actually be registered in the symbol table (this is
            // the crux of the fix: previously QualifiedName was always null, so RegisterAll never
            // registered named fork/join/merge/decide/accept/send nodes).
            Assert.True(result.Workspace.Declarations.TryGetValue("AF::Flow::f", out var forkSymbol));
            Assert.Same(fork, forkSymbol);
            Assert.True(result.Workspace.Declarations.TryGetValue("AF::Flow::j", out var joinSymbol));
            Assert.Same(join, joinSymbol);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Both the guarded (<c>if ... then ...</c>) and default (<c>else ...</c>)
    ///     <c>actionTargetSuccession</c> variants extract a target; only the guarded form captures
    ///     a guard expression (the grammar provides none for the <c>else</c> alternative).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_GuardedAndDefaultActionTargetSuccession_ExtractTargets()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package AF {
                    action def Flow {
                        action monitor;
                        then decide d;
                        if true then addCharge;
                        else endCharging;
                        action addCharge;
                        action endCharging;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["AF::Flow"]);
            var successions = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlTransitionNode>()
                .Where(t => t.Source == "d")
                .ToList();
            Assert.Equal(2, successions.Count);

            var guarded = successions.Single(t => t.Target == "addCharge");
            Assert.Equal("true", guarded.Guard);

            var defaulted = successions.Single(t => t.Target == "endCharging");
            Assert.Null(defaulted.Guard);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An <c>enum def</c> with bare literal values captures each as an "enum value" feature,
    ///     with an implicit source-order-preserving <c>enumerationBody</c> traversal.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_EnumDefinition_BareLiterals_CapturesEnumValues()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package E {
                    enum def TrafficLightColor {
                        enum green;
                        enum yellow;
                        enum red;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var color = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["E::TrafficLightColor"]);
            var values = color.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .Where(f => f.FeatureKeyword == "enum value")
                .Select(f => f.Name)
                .ToList();
            Assert.Equal(["green", "yellow", "red"], values);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An <c>enum def</c> with the value-assignment form (<c>A = 4.0;</c>) still captures each
    ///     literal's name (the assigned value expression is not parsed, an accepted minimal-
    ///     capture gap).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_EnumDefinition_ValueAssignmentForm_CapturesNames()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package E {
                    private import ScalarValues::Real;
                    enum def GradePoints :> Real {
                        A = 4.0;
                        B = 3.0;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var grades = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["E::GradePoints"]);
            var values = grades.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .Where(f => f.FeatureKeyword == "enum value")
                .Select(f => f.Name)
                .ToList();
            Assert.Equal(["A", "B"], values);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A requirement definition captures its <c>doc</c>, <c>subject</c>,
    ///     <c>require constraint</c>, and <c>assume constraint</c> members as Children/Annotations.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RequirementDefinition_CapturesSubjectAndConstraints()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package R {
                    private import ScalarValues::Real;

                    part def Vehicle {
                        attribute dryMass : Real;
                    }

                    requirement def MassLimitationRequirement {
                        doc /* The actual mass shall be less than or equal to the required mass. */
                        attribute massActual : Real;
                        attribute massReqd : Real;
                        require constraint { massActual <= massReqd }
                    }

                    requirement def VehicleMassLimitationRequirement :> MassLimitationRequirement {
                        subject vehicle : Vehicle;
                        assume constraint { vehicle.dryMass > 0 }
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);

            var massLimitation = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["R::MassLimitationRequirement"]);
            Assert.Contains(massLimitation.Annotations,
                a => a.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation);
            var requireConstraint = Assert.Single(massLimitation.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "require constraint");
            Assert.Contains("massActual", requireConstraint.ExpressionText);
            Assert.Contains("massReqd", requireConstraint.ExpressionText);

            var vehicleRequirement = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["R::VehicleMassLimitationRequirement"]);
            var subject = Assert.Single(vehicleRequirement.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "subject");
            Assert.Equal("vehicle", subject.Name);
            Assert.Equal("Vehicle", subject.FeatureTyping);

            var assumeConstraint = Assert.Single(vehicleRequirement.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "assume constraint");
            Assert.Contains("dryMass", assumeConstraint.ExpressionText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A requirement usage (not only a requirement definition) that specializes a requirement
    ///     def and supplies its own <c>subject</c>/<c>assume constraint</c> body also captures
    ///     those members as Children — the dominant real-corpus idiom.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RequirementUsage_CapturesSubjectAndConstraint()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package R {
                    private import ScalarValues::Real;

                    part def Vehicle {
                        attribute fuelMass : Real;
                    }

                    requirement def VehicleMassLimitationRequirement;

                    requirement fullVehicleMassLimit : VehicleMassLimitationRequirement {
                        subject vehicle : Vehicle;
                        assume constraint {
                            vehicle.fuelMass == 0
                        }
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var usage = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(
                result.Workspace.Declarations["R::fullVehicleMassLimit"]);

            var subject = Assert.Single(usage.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "subject");
            Assert.Equal("vehicle", subject.Name);

            var assumeConstraint = Assert.Single(usage.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "assume constraint");
            Assert.Contains("fuelMass", assumeConstraint.ExpressionText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>concern def</c> and a <c>concern</c> usage that specializes it (mirroring the
    ///     requirement def/usage pattern verified above, since <c>concernBody</c> reuses the same
    ///     <c>requirementBody</c> grammar shape) also capture their <c>subject</c> member as a
    ///     Children entry — exercising <see cref="DemaConsulting.SysML2Tools.Semantic.Model.AstBuilder.VisitConcernDefinition"/>
    ///     and <see cref="DemaConsulting.SysML2Tools.Semantic.Model.AstBuilder.VisitConcernUsage"/>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConcernDefinitionAndUsage_CapturesSubject()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package Cn {
                    part def Vehicle;

                    concern def SafetyConcern {
                        subject vehicle : Vehicle;
                    }

                    concern vehicleSafety : SafetyConcern {
                        subject vehicle : Vehicle;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);

            var concernDef = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["Cn::SafetyConcern"]);
            Assert.Equal("concern def", concernDef.DefinitionKeyword);
            var defSubject = Assert.Single(concernDef.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "subject");
            Assert.Equal("vehicle", defSubject.Name);

            var concernUsage = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(
                result.Workspace.Declarations["Cn::vehicleSafety"]);
            Assert.Equal("concern", concernUsage.FeatureKeyword);
            var usageSubject = Assert.Single(concernUsage.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.FeatureKeyword == "subject");
            Assert.Equal("vehicle", usageSubject.Name);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A top-level <c>constraint { expr }</c> usage (not nested in a requirement) is captured
    ///     with its raw expression text — previously entirely absent from the AST.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConstraintUsage_CapturesExpressionText()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package C {
                    constraint massCheck {
                        1 == 1
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var constraint = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(
                result.Workspace.Declarations["C::massCheck"]);
            Assert.Equal("constraint", constraint.FeatureKeyword);
            Assert.Contains("1==1", constraint.ExpressionText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>constraint def</c> synthesizes one child feature node capturing its own
    ///     calculation-body expression text, reusing the same <c>Children</c>-driven compartment
    ///     mechanism as a nested requirement constraint.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConstraintDefinition_SynthesizesExpressionChild()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package C {
                    constraint def MassCheck {
                        1 == 1
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var def = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["C::MassCheck"]);
            var expressionChild = Assert.Single(def.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>());
            Assert.Equal("constraint", expressionChild.FeatureKeyword);
            Assert.Contains("1==1", expressionChild.ExpressionText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Actor and stakeholder usages nested in a requirement definition's body are captured,
    ///     mirroring the subject capture pattern.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RequirementDefinition_CapturesActorAndStakeholder()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package R {
                    part def Driver;
                    part def Owner;

                    requirement def DriveRequirement {
                        actor driver : Driver;
                        stakeholder owner : Owner;
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var req = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["R::DriveRequirement"]);
            var features = req.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>().ToList();

            var actor = Assert.Single(features, f => f.FeatureKeyword == "actor");
            Assert.Equal("driver", actor.Name);
            Assert.Equal("Driver", actor.FeatureTyping);

            var stakeholder = Assert.Single(features, f => f.FeatureKeyword == "stakeholder");
            Assert.Equal("owner", stakeholder.Name);
            Assert.Equal("Owner", stakeholder.FeatureTyping);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A <c>verify</c> member's nested content (its own <c>requirementBody</c>) is not
    ///     spuriously hoisted onto the enclosing requirement definition's <c>Children</c> — a
    ///     regression guard for the null-suppression safeguard in
    ///     <c>VisitRequirementVerificationMember</c>. Its target is still captured separately via
    ///     <c>VerifiedRequirementNames</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RequirementVerifyMember_DoesNotHoistNestedContent()
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile,
                """
                package R {
                    part def Vehicle;

                    requirement def TargetRequirement {
                        subject shouldNotAppear : Vehicle;
                    }

                    requirement def CheckMass {
                        subject checkSubject : Vehicle;
                        verify requirement notHoisted : TargetRequirement {
                            subject alsoShouldNotAppear : Vehicle;
                        }
                    }
                }
                """, TestContext.Current.CancellationToken);

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            Assert.NotNull(result.Workspace);
            var checkMass = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace.Declarations["R::CheckMass"]);

            // The verify member's own nested subject must not appear anywhere in CheckMass's
            // Children — neither as a direct child nor spuriously flattened into one. The
            // enclosing requirement's own legitimate "checkSubject" subject must still be present.
            var subjects = checkMass.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>()
                .Where(f => f.FeatureKeyword == "subject")
                .ToList();
            var subject = Assert.Single(subjects);
            Assert.Equal("checkSubject", subject.Name);
            Assert.DoesNotContain(checkMass.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>(),
                f => f.Name is "notHoisted" or "alsoShouldNotAppear");

            // The verify target is still captured via VerifiedRequirementNames.
            Assert.Contains("TargetRequirement", checkMass.VerifiedRequirementNames);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Finds the test/SysMLModels directory relative to the test assembly.
    /// </summary>
    private static string? FindSysMLModelsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "test", "SysMLModels");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    /// <summary>Asserts that a feature with the given name has the expected keyword and typing.</summary>
    private static void AssertFeature(
        IEnumerable<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode> features,
        string name,
        string keyword,
        string typing)
    {
        var feature = features.FirstOrDefault(f => f.Name == name);
        Assert.NotNull(feature);
        Assert.Equal(keyword, feature.FeatureKeyword);
        Assert.Equal(typing, feature.FeatureTyping);
    }

    /// <summary>Asserts that the named declaration exists and is a definition with the given keyword.</summary>
    private static void AssertKeyword(SysmlWorkspace workspace, string qualifiedName, string expectedKeyword)
    {
        Assert.True(workspace.Declarations.TryGetValue(qualifiedName, out var node), $"Missing {qualifiedName}");
        var def = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(node);
        Assert.Equal(expectedKeyword, def.DefinitionKeyword);
    }

    /// <summary>
    ///     A view captures every owned <c>filter</c> view condition in source order, and a view
    ///     usage's view definition (<c>view V : Def</c>) is captured and resolved into a
    ///     <c>Typing</c> edge (SysML 7.26.2).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewConditionsAndViewDefinition_Captured()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
                    part def A;
                    view def Def { filter @SysML::PartUsage; filter not @SysML::Documentation; }
                    view V : Def { expose A; }
                }
                """, TestContext.Current.CancellationToken);

            // Act
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);

            // Assert
            Assert.NotNull(result.Workspace);
            var def = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(result.Workspace.Declarations["P::Def"]);
            Assert.Equal(["@SysML::PartUsage", "not @SysML::Documentation"], def.FilterExpressionTexts);
            var view = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode>(result.Workspace.Declarations["P::V"]);
            Assert.Equal("Def", view.ViewDefinitionName);
            Assert.Contains(view.ResolvedEdges, e =>
                e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Typing && e.TargetQualifiedName == "P::Def");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     By default (no strict mode) every kind of unresolved reference written inside an
    ///     expression (guard, trigger, accept payload, constraint, render target) is a Warning at the
    ///     reference token's own position.
    /// </summary>
    /// <param name="source">The model source, containing the reference once.</param>
    /// <param name="reference">The unresolved reference text.</param>
    [Theory]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a if ghostGuard then b;\n    }\n}\n", "ghostGuard")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a accept GhostTrigger then b;\n    }\n}\n", "GhostTrigger")]
    [InlineData("package P {\n    action def A {\n        action a accept GhostPayload;\n    }\n}\n", "GhostPayload")]
    [InlineData("package P {\n    part def A {\n        attribute x;\n        constraint { x <= ghostConstraint }\n    }\n}\n", "ghostConstraint")]
    [InlineData("package P {\n    view v {\n        render ghostRender;\n    }\n}\n", "ghostRender")]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedExpressionReference_ProducesWarningAtToken(string source, string reference)
    {
        // Arrange
        var index = source.IndexOf(reference, StringComparison.Ordinal);
        var expectedLine = 1 + source[..index].Count(c => c == '\n');
        var expectedColumn = index - (source.LastIndexOf('\n', index) + 1) + 1;

        // Act
        var result = await LoadModelsAsync(source);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Message == $"Unresolved reference: '{reference}'");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((expectedLine, expectedColumn), (diagnostic.Line, diagnostic.Column));
        Assert.False(result.HasErrors);
    }

    /// <summary>
    ///     In strict mode an unresolved reference written inside an expression (guard, trigger,
    ///     accept payload, constraint, render target) is an Error at the reference token's own
    ///     position.
    /// </summary>
    /// <param name="source">The model source, containing the reference once.</param>
    /// <param name="reference">The unresolved reference text.</param>
    [Theory]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a if ghostGuard then b;\n    }\n}\n", "ghostGuard")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a accept GhostTrigger then b;\n    }\n}\n", "GhostTrigger")]
    [InlineData("package P {\n    action def A {\n        action a accept GhostPayload;\n    }\n}\n", "GhostPayload")]
    [InlineData("package P {\n    part def A {\n        attribute x;\n        constraint { x <= ghostConstraint }\n    }\n}\n", "ghostConstraint")]
    [InlineData("package P {\n    view v {\n        render ghostRender;\n    }\n}\n", "ghostRender")]
    public async Task WorkspaceLoader_LoadAsync_StrictUnresolved_UnresolvedExpressionReference_ProducesErrorAtToken(string source, string reference)
    {
        // Arrange
        var index = source.IndexOf(reference, StringComparison.Ordinal);
        var expectedLine = 1 + source[..index].Count(c => c == '\n');
        var expectedColumn = index - (source.LastIndexOf('\n', index) + 1) + 1;

        // Act
        var result = await LoadModelsWithOptionsAsync(true, [source]);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Message == $"Unresolved reference: '{reference}'");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((expectedLine, expectedColumn), (diagnostic.Line, diagnostic.Column));
        Assert.True(result.HasErrors);
    }

    /// <summary>
    ///     In strict mode every kind of unresolved reference is an Error with the unchanged
    ///     "Unresolved reference:" message, and still records no edge.
    /// </summary>
    /// <param name="source">The model source, containing one unresolved reference.</param>
    /// <param name="reference">The unresolved reference text.</param>
    [Theory]
    [InlineData("package P {\n    part def A :> GhostSupertype;\n}\n", "GhostSupertype")]
    [InlineData("package P {\n    part def A {\n        part p : GhostTyping;\n    }\n}\n", "GhostTyping")]
    [InlineData("package P {\n    part def A {\n        part p :>> GhostRedefine;\n    }\n}\n", "GhostRedefine")]
    [InlineData("package P {\n    import GhostImport::*;\n}\n", "GhostImport")]
    [InlineData("package P {\n    view V {\n        expose GhostExpose::*;\n    }\n}\n", "GhostExpose")]
    [InlineData("package P {\n    requirement def R;\n    satisfy R by GhostSatisfy;\n}\n", "GhostSatisfy")]
    [InlineData("package P {\n    verification def VC {\n        objective {\n            verify GhostVerify;\n        }\n    }\n}\n", "GhostVerify")]
    [InlineData("package P {\n    part def A;\n    allocate A to GhostAllocate;\n}\n", "GhostAllocate")]
    [InlineData("package P {\n    part def A;\n    dependency from A to GhostDependency;\n}\n", "GhostDependency")]
    [InlineData("package P {\n    part def A {\n        part a;\n        bind a = ghostBind.feat;\n    }\n}\n", "ghostBind.feat")]
    [InlineData("package P {\n    part def A {\n        part a;\n        connect a to ghostConnect.feat;\n    }\n}\n", "ghostConnect.feat")]
    [InlineData("package P {\n    part def A {\n        @GhostMetadata;\n    }\n}\n", "GhostMetadata")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a if ghostGuard then b;\n    }\n}\n", "ghostGuard")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a accept GhostTrigger then b;\n    }\n}\n", "GhostTrigger")]
    [InlineData("package P {\n    action def A {\n        action a accept GhostPayload;\n    }\n}\n", "GhostPayload")]
    [InlineData("package P {\n    part def A {\n        attribute x;\n        constraint { x <= ghostConstraint }\n    }\n}\n", "ghostConstraint")]
    [InlineData("package P {\n    view v {\n        render ghostRender;\n    }\n}\n", "ghostRender")]
    public async Task WorkspaceLoader_LoadAsync_StrictUnresolved_UnresolvedReferenceKinds_ProduceErrors(string source, string reference)
    {
        // Arrange / Act
        var result = await LoadModelsWithOptionsAsync(true, [source]);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Message == $"Unresolved reference: '{reference}'");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.StartsWith("Unresolved reference:", diagnostic.Message, StringComparison.Ordinal);
        Assert.True(result.HasErrors);
    }

    /// <summary>
    ///     A circular import and a cyclic specialization stay Warnings whether or not strict
    ///     unresolved-reference mode is on.
    /// </summary>
    /// <param name="strict">Whether strict unresolved-reference mode is enabled.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspaceLoader_LoadAsync_CircularImportAndCyclicSpecialization_RemainWarnings(bool strict)
    {
        // Arrange / Act
        var result = await LoadModelsWithOptionsAsync(
            strict,
            ["package A { import B::*; }\npackage B { import A::*; }\npackage C { part def X :> C::Y; part def Y :> C::X; }\n"]);

        // Assert
        Assert.Contains(result.Diagnostics, d => d.Message.StartsWith("Circular import detected", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.StartsWith("Cyclic specialization detected", StringComparison.Ordinal));
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.False(result.HasErrors);
    }

    /// <summary>
    ///     Valid standard-library references (types, imports, views) produce no diagnostics at all,
    ///     whether or not strict unresolved-reference mode is on.
    /// </summary>
    /// <param name="strict">Whether strict unresolved-reference mode is enabled.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspaceLoader_LoadAsync_StdlibReferences_ProduceNoDiagnostics(bool strict)
    {
        // Arrange / Act
        var result = await LoadModelsWithOptionsAsync(
            strict,
            ["package P {\n    private import ScalarValues::*;\n    part def V { attribute mass : Real; attribute n : ScalarValues::Integer; }\n}\n"]);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     The default severity of an unresolved reference in a user file is Warning.
    /// </summary>
    [Fact]
    public void WorkspaceLoader_DefaultUnresolvedSeverity_IsWarning()
    {
        // Arrange / Act / Assert
        Assert.Equal(DiagnosticSeverity.Warning, WorkspaceLoader.DefaultUserUnresolvedSeverity);
    }

    /// <summary>
    ///     The resolver constructed with Warning severity (as the standard-library generator does,
    ///     because standard-library-origin diagnostics stay Warnings) reports unresolved references
    ///     as Warnings.
    /// </summary>
    [Fact]
    public void ReferenceResolver_ResolveAll_WarningSeverity_UnresolvedReferenceIsWarning()
    {
        // Arrange
        var diagnostics = new List<SysmlDiagnostic>();
        var cst = DemaConsulting.SysML2Tools.Parser.WorkspaceParser.ParseSourceToCst(
            "lib.sysml", "package P { part def X :> Missing; }", diagnostics);
        var root = new AstBuilder("lib.sysml").Build(cst);
        var table = new SymbolTable();
        table.RegisterAll(root);
        var resolver = new ReferenceResolver(table, diagnostics, DiagnosticSeverity.Warning);

        // Act
        resolver.ResolveAll([("lib.sysml", root)]);

        // Assert
        var diagnostic = Assert.Single(diagnostics, d => d.Message == "Unresolved reference: 'Missing'");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    /// <summary>
    ///     A SysML2Tools extension render selector produces one portability Warning at the token,
    ///     naming the standard view definition to use instead, and no unresolved-reference
    ///     diagnostic.
    /// </summary>
    /// <param name="token">The extension selector.</param>
    /// <param name="definition">The standard view definition suggested instead.</param>
    [Theory]
    [InlineData("asGeneralDiagram", "GeneralView")]
    [InlineData("asStateTransitionDiagram", "StateTransitionView")]
    [InlineData("asActionFlowDiagram", "ActionFlowView")]
    [InlineData("asSequenceDiagram", "SequenceView")]
    [InlineData("asGridDiagram", "GridView")]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelector_ProducesPortabilityWarningAtToken(string token, string definition)
    {
        // Arrange
        var source = $"package P {{\n    view v {{\n        render {token};\n    }}\n}}\n";
        var index = source.IndexOf(token, StringComparison.Ordinal);
        var expectedColumn = index - (source.LastIndexOf('\n', index) + 1) + 1;

        // Act
        var result = await LoadModelsAsync(source);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((3, expectedColumn), (diagnostic.Line, diagnostic.Column));
        Assert.Equal(
            $"'{token}' is a SysML2Tools extension, not a standard SysML v2 rendering; the model will not validate in other tools. " +
            $"Use a standard view definition instead (e.g. 'view v : StandardViewDefinitions::{definition}').",
            diagnostic.Message);
        Assert.False(result.HasErrors);
    }

    /// <summary>
    ///     The standard render selectors (qualified, imported, and the element-table and textual
    ///     notations) produce no diagnostics.
    /// </summary>
    /// <param name="source">The model source.</param>
    [Theory]
    [InlineData("package P { view v { render Views::asTreeDiagram; } }")]
    [InlineData("package P { view v { render Views::asInterconnectionDiagram; } }")]
    [InlineData("package P { private import Views::*; view v { render asTreeDiagram; } }")]
    [InlineData("package P { view v { private import Views::*; render asElementTable; } }")]
    [InlineData("package P { view v { render Views::asTextualNotation; } }")]
    public async Task WorkspaceLoader_LoadAsync_StandardRenderSelectors_ProduceNoDiagnostics(string source)
    {
        // Arrange / Act
        var result = await LoadModelsAsync(source);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Strict mode does not change the portability diagnostic: still exactly one Warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelectorWithStrictUnresolved_StillOnlyPortabilityWarning()
    {
        // Arrange / Act
        var result = await LoadModelsWithOptionsAsync(true, ["package P { view v { render asGridDiagram; } }"]);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("StandardViewDefinitions::GridView", diagnostic.Message, StringComparison.Ordinal);
        Assert.False(result.HasErrors);
    }

    /// <summary>
    ///     Each occurrence of an extension selector is reported at its own position.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelectorTwice_ReportsEachOccurrence()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package P {\n    view a { render asGridDiagram; }\n    view b { render asGridDiagram; }\n}\n");

        // Assert
        Assert.Equal([2, 3], result.Diagnostics.Select(d => d.Line).ToList());
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
    }

    /// <summary>
    ///     A model that declares its own rendering with the selector's name resolves it normally:
    ///     no portability warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelectorUserDeclaredRendering_NoPortabilityWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package P {\n    rendering asGridDiagram : Views::GraphicalRendering;\n    view v { render asGridDiagram; }\n}\n");

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     A qualified extension selector (<c>Views::asGeneralDiagram</c>, which is not a
    ///     <c>Views</c> member) is an ordinary unresolved-reference Warning with no portability
    ///     warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelectorQualified_ReportsUnresolvedNotPortability()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("package P { view v { render Views::asGeneralDiagram; } }");

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Unresolved reference: 'Views::asGeneralDiagram'", diagnostic.Message);
    }

    /// <summary>
    ///     In strict mode a qualified extension selector is an ordinary unresolved-reference Error
    ///     with no portability warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelectorQualifiedWithStrictUnresolved_ReportsUnresolvedErrorNotPortability()
    {
        // Arrange / Act
        var result = await LoadModelsWithOptionsAsync(true, ["package P { view v { render Views::asGeneralDiagram; } }"]);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Unresolved reference: 'Views::asGeneralDiagram'", diagnostic.Message);
    }

    /// <summary>
    ///     The five SysML2Tools extension render selectors resolve (no "Unresolved reference"
    ///     diagnostic) and each produces only its one portability Warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ToolRenderSelectorsDeclared_NoUnresolvedWarningOnlyPortabilityWarnings()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                view def V { render asGridDiagram; }
                view v1 { render asGeneralDiagram; }
                view v2 { render asStateTransitionDiagram; }
                view v3 { render asActionFlowDiagram; }
                view v4 { render asSequenceDiagram; }
            }
            """);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal));
        Assert.Equal(5, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
            Assert.Contains("SysML2Tools extension", d.Message, StringComparison.Ordinal);
        });
        Assert.False(result.HasErrors);
    }

    /// <summary>
    ///     Loads each of <paramref name="sources"/> as its own file (with the standard library), in order.
    /// </summary>
    private static Task<SysmlLoadResult> LoadModelsAsync(params string[] sources) =>
        LoadModelsWithOptionsAsync(false, sources);

    /// <summary>
    ///     Loads each of <paramref name="sources"/> as its own file (with the standard library), in
    ///     order, using the given <paramref name="strictUnresolved"/> option.
    /// </summary>
    private static async Task<SysmlLoadResult> LoadModelsWithOptionsAsync(bool strictUnresolved, string[] sources)
    {
        var files = new List<string>();
        try
        {
            foreach (var source in sources)
            {
                var file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
                files.Add(file);
                await File.WriteAllTextAsync(file, source, TestContext.Current.CancellationToken);
            }

            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync(files, stdlibTable, strictUnresolved);
            Assert.NotNull(result.Workspace);
            return result;
        }
        finally
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>Returns whether the load recorded the given resolved edge.</summary>
    private static bool HasEdge(SysmlLoadResult result, SysmlEdgeKind kind, string source, string target) =>
        result.Workspace!.Index.AllEdges.Any(e =>
            e.Kind == kind && e.SourceQualifiedName == source && e.TargetQualifiedName == target);

    /// <summary>Returns whether the load warned that <paramref name="name"/> did not resolve.</summary>
    private static bool HasUnresolvedWarning(SysmlLoadResult result, string name) =>
        result.Diagnostics.Any(d =>
            d.Severity == DiagnosticSeverity.Warning && d.Message == $"Unresolved reference: '{name}'");

    /// <summary>
    ///     A supertype reference that does not resolve, listed before an alias of the same target,
    ///     yields exactly one <c>Supertype</c> edge — the alias's — tagged with the alias text as
    ///     its raw reference, plus the unresolved-reference warning for the unresolved one.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SupertypeUnresolvedBeforeAlias_OnlyAliasEdgeCarriesRawReference()
    {
        // Arrange / Act: X is not visible from Q; A is an alias of P::X
        var result = await LoadModelsAsync("""
            package P { part def X; }
            package Q { alias A for P::X; part def Sub :> X, A; }
            """);

        // Assert
        var sub = result.Workspace!.Declarations["Q::Sub"];
        var edge = Assert.Single(sub.ResolvedEdges, e => e.Kind == SysmlEdgeKind.Supertype);
        Assert.Equal("P::X", edge.TargetQualifiedName);
        Assert.Equal("A", edge.RawReference);
        Assert.True(HasUnresolvedWarning(result, "X"));
    }

    /// <summary>
    ///     Every single-reference edge kind records the reference text exactly as written:
    ///     qualified and bare supertypes, typing, redefinition, import, verify, expose, metadata
    ///     type, and a view usage's view definition.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ResolvedEdges_RecordRawReferenceText()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            metadata def Tag { }
            package P {
                part def X;
                requirement def R;
                view def VD;
            }
            package Q {
                private import P::*;
                import P::X;
                part def Sub1 :> P::X;
                part def Sub2 :> X;
                part def Holder { part slot : X; part spare; @Tag; }
                part def Child :> Holder { part r redefines spare; }
                case def C { objective obj { verify requirement placeholder : R; } }
                view V : VD { expose P::X; }
            }
            """);

        // Assert
        var declarations = result.Workspace!.Declarations;
        string? Raw(string qualifiedName, SysmlEdgeKind kind) =>
            Assert.Single(declarations[qualifiedName].ResolvedEdges, e => e.Kind == kind).RawReference;

        Assert.Equal("P::X", Raw("Q::Sub1", SysmlEdgeKind.Supertype));
        Assert.Equal("X", Raw("Q::Sub2", SysmlEdgeKind.Supertype));
        Assert.Equal("X", Raw("Q::Holder::slot", SysmlEdgeKind.Typing));
        Assert.Equal("spare", Raw("Q::Child::r", SysmlEdgeKind.Redefinition));
        Assert.Equal("R", Raw("Q::C", SysmlEdgeKind.Verify));
        Assert.Equal("VD", Raw("Q::V", SysmlEdgeKind.Typing));
        Assert.Equal("P::X", Raw("Q::V", SysmlEdgeKind.Expose));

        var imports = result.Workspace.Index.AllEdges.Where(e => e.Kind == SysmlEdgeKind.Import).ToList();
        Assert.NotEmpty(imports);
        Assert.All(imports, e => Assert.False(string.IsNullOrEmpty(e.RawReference)));
        Assert.Contains(imports, e => e.RawReference == "P::X");
        Assert.Contains(imports, e => e.RawReference == "P::*");

        var metadata = Assert.Single(declarations["Q::Holder"].Children.OfType<SysmlMetadataNode>());
        Assert.Equal("Tag", Assert.Single(metadata.ResolvedEdges).RawReference);
    }

    /// <summary>
    ///     An <c>import</c> edge's <see cref="SysmlEdge.RawReference"/> keeps the wildcard and
    ///     recursive markers as written (<c>P</c>, <c>P::*</c>, <c>P::**</c>, <c>P::*::**</c>) so the
    ///     forms are distinguishable, excludes any <c>[filter]</c> bracket expression, drops only
    ///     inter-token whitespace, and still resolves every form to the same target namespace.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ImportForms_RawReferenceKeepsWrittenWildcardMarkers()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            metadata def Tag { }
            package P { part def X; }
            package Q1 { import P; }
            package Q2 { import P::*; }
            package Q3 { import P::**; }
            package Q4 { import P::*::**; }
            package Q5 { import P :: *; }
            package Q6 { import P::**[@Tag]; }
            package Q7 { import P::*[@Tag]; }
            """);

        // Assert
        Assert.NotNull(result.Workspace);
        var imports = result.Workspace.Index.AllEdges.Where(e => e.Kind == SysmlEdgeKind.Import).ToList();
        string? RawFor(string package) =>
            Assert.Single(
                Assert.Single(result.Workspace.Declarations[package].Children.OfType<SysmlImportNode>()).ResolvedEdges).RawReference;

        Assert.Equal("P", RawFor("Q1"));
        Assert.Equal("P::*", RawFor("Q2"));
        Assert.Equal("P::**", RawFor("Q3"));
        Assert.Equal("P::*::**", RawFor("Q4"));
        Assert.Equal("P::*", RawFor("Q5"));
        Assert.Equal("P::**", RawFor("Q6"));
        Assert.Equal("P::*", RawFor("Q7"));

        // Resolution is unchanged: every form still targets the namespace itself.
        Assert.All(imports, e => Assert.Equal("P", e.TargetQualifiedName));
    }

    /// <summary>
    ///     An <c>expose</c> edge's <see cref="SysmlEdge.RawReference"/> keeps the wildcard and
    ///     recursive markers as written and excludes the <c>[filter]</c> bracket expression (which
    ///     stays on <see cref="ExposeMember.BracketFilterExpressionText"/>), while every form still
    ///     resolves to the same target.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ExposeForms_RawReferenceKeepsWrittenWildcardMarkers()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            metadata def Tag { }
            package P { part def X { @Tag; } }
            package Q {
                view V {
                    expose P;
                    expose P::*;
                    expose P::**;
                    expose P::*::**;
                    expose P::**[@Tag];
                }
            }
            """);

        // Assert
        Assert.NotNull(result.Workspace);
        var view = Assert.IsType<SysmlViewNode>(result.Workspace.Declarations["Q::V"]);
        var exposes = view.ResolvedEdges.Where(e => e.Kind == SysmlEdgeKind.Expose).ToList();
        Assert.Equal(["P", "P::*", "P::**", "P::*::**", "P::**"], exposes.Select(e => e.RawReference));
        Assert.All(exposes, e => Assert.Equal("P", e.TargetQualifiedName));

        // The bracket filter text is carried separately and the resolved member name is unstripped.
        Assert.Equal("@Tag", view.ExposeMembers[4].BracketFilterExpressionText);
        Assert.All(view.ExposeMembers, m => Assert.Equal("P", m.QualifiedName));
    }

    /// <summary>
    ///     Hand-built (or legacy-deserialized) import nodes and expose members without captured
    ///     reference text fall back to the stripped name for the edge's raw reference.
    /// </summary>
    [Fact]
    public void ReferenceResolver_ImportAndExposeWithoutRawText_FallBackToName()
    {
        // Arrange
        var table = new SymbolTable();
        var target = new SysmlPackageNode { Name = "P", QualifiedName = "P" };
        var import = new SysmlImportNode { ImportedNamespace = "P", ImportedNames = ["P"] };
        var view = new SysmlViewNode
        {
            Name = "V",
            QualifiedName = "Q::V",
            ExposeMembers = [new ExposeMember("P", null)],
        };
        var package = new SysmlPackageNode { Name = "Q", QualifiedName = "Q", Children = [import, view] };
        table.RegisterAll(target);
        table.RegisterAll(package);
        var diagnostics = new List<SysmlDiagnostic>();

        // Act
        new ReferenceResolver(table, diagnostics, DiagnosticSeverity.Warning).ResolveAll([("mem.sysml", package), ("target.sysml", target)]);

        // Assert
        Assert.Empty(diagnostics);
        Assert.Equal("P", Assert.Single(import.ResolvedEdges).RawReference);
        Assert.Equal("P", Assert.Single(view.ResolvedEdges, e => e.Kind == SysmlEdgeKind.Expose).RawReference);
    }

    /// <summary>
    ///     The two-ended edge kinds (satisfy, allocate, dependency, connect, binding, transition)
    ///     carry no single reference text, so their raw reference is <see langword="null"/>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TwoEndedEdges_HaveNoRawReference()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                requirement def R;
                requirement req : R;
                part def Sensor { port x; }
                part def Display { port y; }
                part subj : Display;
                satisfy req by subj;
                part a : Display;
                part b : Display;
                allocate a to b;
                dependency a to b;
                part def Q {
                    part s : Sensor;
                    part d : Display;
                    connect s.x to d.y;
                    bind s.x = d.y;
                }
                state def Light {
                    state stop;
                    state go;
                    transition first stop then go;
                }
            }
            """);

        // Assert: each two-ended kind is present and untagged
        var edges = result.Workspace!.Index.AllEdges;
        foreach (var kind in new[]
        {
            SysmlEdgeKind.Satisfy,
            SysmlEdgeKind.Allocate,
            SysmlEdgeKind.Dependency,
            SysmlEdgeKind.Connect,
            SysmlEdgeKind.Binding,
            SysmlEdgeKind.Transition,
        })
        {
            var ofKind = edges.Where(e => e.Kind == kind).ToList();
            Assert.NotEmpty(ofKind);
            Assert.All(ofKind, e => Assert.Null(e.RawReference));
        }
    }

    /// <summary>
    ///     A private member referenced through a qualified name from another package gets no
    ///     edge and an "Unresolved reference" warning (KerML 8.2.3.5 visible resolution), while a
    ///     public member of the same package still resolves.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PrivateMemberQualifiedFromOtherPackage_NoEdgeAndWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P { private part def Hidden; part def Shown; }
            package Q {
                part a : P::Hidden;
                part def C :> P::Hidden;
                part b : P::Shown;
            }
            """);

        // Assert
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Q::a", "P::Hidden"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Supertype, "Q::C", "P::Hidden"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::b", "P::Shown"));
        Assert.True(HasUnresolvedWarning(result, "P::Hidden"));
    }

    /// <summary>
    ///     The short-name and alias paths honor member visibility too: a private short name or
    ///     alias is not reachable from another package, a public one is.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PrivateShortNameAndAlias_NotResolvableFromOtherPackage()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                private part def <hid> HiddenShort;
                part def <pub> PublicShort;
                private alias HiddenAlias for PublicShort;
                alias PublicAlias for PublicShort;
            }
            package Q {
                part a : P::hid;
                part b : P::pub;
                part c : P::HiddenAlias;
                part d : P::PublicAlias;
            }
            """);

        // Assert
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Q::a", "P::HiddenShort"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::b", "P::PublicShort"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Q::c", "P::PublicShort"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::d", "P::PublicShort"));
        Assert.True(HasUnresolvedWarning(result, "P::hid"));
        Assert.True(HasUnresolvedWarning(result, "P::HiddenAlias"));
    }

    /// <summary>
    ///     A protected member is visible to a specialization of its owner but not to an
    ///     unrelated qualified reference.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ProtectedMember_VisibleToSpecializationOnly()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Base { protected part def Prot; }
                part def Derived :> Base { part x : Prot; }
                part y : Base::Prot;
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "P::Derived::x", "P::Base::Prot"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "P::y", "P::Base::Prot"));
        Assert.True(HasUnresolvedWarning(result, "Base::Prot"));
    }

    /// <summary>
    ///     <c>import all</c> brings in private members; an unqualified reference to a private
    ///     member inside its own package resolves too.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ImportAllAndOwnPackage_ResolvePrivateMember()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P { private part def Hidden; part own : Hidden; }
            package Q { import all P::*; part a : Hidden; }
            package R { import P::*; part b : Hidden; }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "P::own", "P::Hidden"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::a", "P::Hidden"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "R::b", "P::Hidden"));
    }

    /// <summary>
    ///     Exposing a private member of another package produces no expose edge and a warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ExposePrivateMember_NoExposeEdgeAndWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P { private part def Hidden; part def Shown; }
            package Q {
                view v { expose P::Hidden; expose P::Shown; }
            }
            """);

        // Assert
        Assert.False(HasEdge(result, SysmlEdgeKind.Expose, "Q::v", "P::Hidden"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Expose, "Q::v", "P::Shown"));
        Assert.True(HasUnresolvedWarning(result, "P::Hidden"));
    }

    /// <summary>
    ///     A filtered import (<c>import P::*[@X];</c>) admits only the annotated element: the
    ///     un-annotated candidate gets no edge and a warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FilteredImport_RejectsUnannotatedCandidate()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            metadata def X { }
            package P {
                part def Marked { @X; }
                part def Plain;
            }
            package Q {
                private import P::*[@X];
                part a : Marked;
                part b : Plain;
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::a", "P::Marked"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Q::b", "P::Plain"));
        Assert.True(HasUnresolvedWarning(result, "Plain"));
    }

    /// <summary>
    ///     A recursive filtered import skips a rejected candidate and finds the deeper match.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FilteredRecursiveImport_SkipsRejectedFindsDeeperMatch()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            metadata def X { }
            package P {
                part def Item;
                package N { part def Item { @X; } }
            }
            package Q {
                private import P::**[@X];
                part a : Item;
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::a", "P::N::Item"));
    }

    /// <summary>
    ///     The metadata a filter tests is resolved before any filtered import is evaluated, so a
    ///     candidate declared in a later file, annotated with a qualified type reference, still
    ///     matches an unqualified filter.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FilteredImportWithMetadataInLaterFile_MatchesQualifiedAnnotation()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package Q { private import P::*[@X]; part a : Marked; part b : Plain; }",
            """
            package M { metadata def X { } }
            package P {
                part def Marked { @M::X; }
                part def Plain;
            }
            """);

        // Assert: Marked's annotation resolves to M::X, which the unqualified filter @X accepts
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Q::a", "P::Marked"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Q::b", "P::Plain"));
        Assert.True(HasUnresolvedWarning(result, "Plain"));
    }

    /// <summary>
    ///     A metaclass-kind filter (<c>@SysML::PartUsage</c>) admits usages and rejects definitions.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FilteredImportWithMetaclassKind_AdmitsMatchingKindOnly()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P { part pu; part def Pd; }
            package Q {
                private import P::*[@SysML::PartUsage];
                part x :> pu;
                part y : Pd;
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "Q::x", "P::pu"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Q::y", "P::Pd"));
    }

    /// <summary>
    ///     An import applies only to the namespace that declares it: a sibling package cannot
    ///     resolve a name through it.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ImportInSiblingPackage_DoesNotApplyToOtherPackage()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package A { part def X; }
            package PkgA { private import A::*; part p : X; }
            package PkgB { part q : X; }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "PkgA::p", "A::X"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "PkgB::q", "A::X"));
        Assert.True(HasUnresolvedWarning(result, "X"));
    }

    /// <summary>
    ///     A nested package sees the imports of the namespaces enclosing it.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ImportInOuterPackage_AppliesToNestedPackage()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package A { part def X; }
            package Outer {
                private import A::*;
                package Inner { part r : X; }
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Outer::Inner::r", "A::X"));
    }

    /// <summary>
    ///     An import written at the root of a file applies to the whole file, but not to another file.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FileRootImport_AppliesToItsOwnFileOnly()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            """
            package A { part def X; }
            private import A::*;
            package P { part s : X; }
            """,
            "package R { part t : X; }");

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "P::s", "A::X"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "R::t", "A::X"));
    }

    /// <summary>
    ///     Two files declaring a top-level package of the same name each resolve their references
    ///     only through their own file-root imports (the roots are keyed by file, not by name).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SameTopLevelNameInTwoFiles_EachUsesItsOwnRootImports()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package LibA { part def T; part def OnlyA; } package LibB { part def T; part def OnlyB; }",
            """
            private import LibA::*;
            package Vehicle { part a : T; part c : OnlyB; }
            """,
            """
            private import LibB::*;
            package Vehicle { part b : T; part d : OnlyA; }
            """);

        // Assert: each file's bare name resolves through its own import only
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Vehicle::a", "LibA::T"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "Vehicle::b", "LibB::T"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Vehicle::b", "LibA::T"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Vehicle::c", "LibB::OnlyB"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "Vehicle::d", "LibA::OnlyA"));
        Assert.True(HasUnresolvedWarning(result, "OnlyB"));
        Assert.True(HasUnresolvedWarning(result, "OnlyA"));
    }

    /// <summary>
    ///     With a top-level package split across two files, each file's definitions derive their
    ///     general types (and so the inherited members they redefine) from that file's own
    ///     root imports.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SameTopLevelNameInTwoFiles_GeneralTypesUseOwnRootImports()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package LibA { part def T; } package LibB { part def T { attribute m; } }",
            """
            private import LibA::*;
            package Vehicle { part def Car1 :> T; }
            """,
            """
            private import LibB::*;
            package Vehicle { part def Car2 :> T { attribute :>> m; } }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "Vehicle::Car1", "LibA::T"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "Vehicle::Car2", "LibB::T"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "Vehicle::Car2::m", "LibB::T::m"));
        Assert.False(HasUnresolvedWarning(result, "m"));
    }

    /// <summary>
    ///     A top-level definition whose supertype is reachable only through a file-root import still
    ///     has that supertype among its general types, so a bare redefinition or subsetting of an
    ///     inherited member resolves (the general types are resolved with the file-root imports).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TopLevelSupertypeViaRootImport_ResolvesMembers()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package Base { part def Vehicle { attribute mass; attribute len; } }",
            """
            private import Base::*;
            part def Car :> Vehicle { attribute :>> mass; attribute m2 :> len; }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "Car", "Base::Vehicle"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "Car::mass", "Base::Vehicle::mass"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Supertype, "Car::m2", "Base::Vehicle::len"));
        Assert.False(HasUnresolvedWarning(result, "mass"));
        Assert.False(HasUnresolvedWarning(result, "len"));
    }

    /// <summary>
    ///     A private import is not re-exported to a package that imports the importing package;
    ///     a public import is.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PrivateImport_IsNotReExported()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package A { part def X; part def Z; }
            package Hiding { private import A::X; }
            package Exposing { public import A::Z; }
            package N {
                private import Hiding::*;
                private import Exposing::*;
                part u : X;
                part v : Z;
            }
            """);

        // Assert
        Assert.False(HasEdge(result, SysmlEdgeKind.Typing, "N::u", "A::X"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "N::v", "A::Z"));
    }

    /// <summary>
    ///     A bare redefinition of a member inherited from a supertype resolves wherever the
    ///     supertype is declared — here in a file loaded after the redefining one.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BareRedefinitionOfInheritedFeatureInLaterFile_Resolves()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            """
            part def SmallVehicle :> Vehicle {
                part smallEng : SmallEngine redefines eng;
            }
            """,
            """
            part def Engine;
            part def SmallEngine :> Engine;
            part def Vehicle { part eng : Engine; }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "SmallVehicle::smallEng", "Vehicle::eng"));
        Assert.False(HasUnresolvedWarning(result, "eng"));
    }

    /// <summary>
    ///     A qualified redefinition target resolves even when the owner has no general types, and
    ///     a redefinition never resolves to the redefining feature itself.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RedefinitionQualifiedTargetOrOnlySelf_ResolvesLenientlyNeverToSelf()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package Lib { attribute def mass; }
            part def Foo { attribute :>> Lib::mass; attribute :>> nothing; }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "Foo::mass", "Lib::mass"));
        Assert.DoesNotContain(result.Workspace!.Index.AllEdges, e => e.SourceQualifiedName == e.TargetQualifiedName);
        Assert.True(HasUnresolvedWarning(result, "nothing"));
    }

    /// <summary>
    ///     A private member inherited from a supertype's supertype cannot be redefined.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RedefinitionOfPrivateInheritedMember_NoEdgeAndWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            part def Base { private attribute secret; attribute open; }
            part def Mid :> Base;
            part def D :> Mid { attribute :>> secret; attribute :>> open; }
            """);

        // Assert
        Assert.False(HasEdge(result, SysmlEdgeKind.Redefinition, "D::secret", "Base::secret"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "D::open", "Base::open"));
        Assert.True(HasUnresolvedWarning(result, "secret"));
    }

    /// <summary>
    ///     A private member of the <em>direct</em> supertype cannot be redefined either (it is not
    ///     inherited), while a protected member of the direct supertype still can.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RedefinitionOfPrivateDirectSupertypeMember_NoEdgeAndWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            part def Base { private attribute secret; protected attribute guarded; attribute open; }
            part def D :> Base { attribute :>> secret; attribute :>> guarded; attribute :>> open; }
            """);

        // Assert
        Assert.False(HasEdge(result, SysmlEdgeKind.Redefinition, "D::secret", "Base::secret"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "D::guarded", "Base::guarded"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Redefinition, "D::open", "Base::open"));
        Assert.True(HasUnresolvedWarning(result, "secret"));
    }

    /// <summary>
    ///     Feature-chain segments after the first resolve by visible resolution in the previous
    ///     feature's type: private and protected members, declared or inherited, are not found.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FeatureChainToNonPublicMember_NoEdgeAndWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Base { private port baseHidden; protected port baseGuarded; }
                part def Eng :> Base { private port hidden; protected port guarded; port open; }
                part engine : Eng;
                part other : Eng;
                connect engine.hidden to other.open;
                connect engine.guarded to other.open;
                connect engine.baseHidden to other.open;
                connect engine.baseGuarded to other.open;
            }
            """);

        // Assert
        Assert.DoesNotContain(result.Workspace!.Index.AllEdges, e => e.Kind == SysmlEdgeKind.Connect);
        Assert.True(HasUnresolvedWarning(result, "engine.hidden"));
        Assert.True(HasUnresolvedWarning(result, "engine.guarded"));
        Assert.True(HasUnresolvedWarning(result, "engine.baseHidden"));
        Assert.True(HasUnresolvedWarning(result, "engine.baseGuarded"));
    }

    /// <summary>
    ///     A private member declared inline in the first segment's own body is not visible through
    ///     a chain from outside that feature either.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FeatureChainToPrivateInlineMember_NoEdgeAndWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Eng { port open; }
                part engine : Eng { private port inner; }
                part other : Eng;
                connect engine.inner to other.open;
            }
            """);

        // Assert
        Assert.DoesNotContain(result.Workspace!.Index.AllEdges, e => e.Kind == SysmlEdgeKind.Connect);
        Assert.True(HasUnresolvedWarning(result, "engine.inner"));
    }

    /// <summary>
    ///     Public declared and inherited members still resolve through a chain, and a private
    ///     feature that is the <em>first</em> segment resolves locally in its own namespace.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FeatureChainToPublicMembersAndPrivateFirstSegment_RecordsConnectEdges()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Base { port baseOpen; }
                part def Eng :> Base { port open; }
                part def Sys {
                    private part inner : Eng;
                    part other : Eng;
                    connect inner.open to other.baseOpen;
                }
            }
            """);

        // Assert
        Assert.Contains(result.Workspace!.Index.AllEdges,
            e => e.Kind == SysmlEdgeKind.Connect &&
                 e.SourceQualifiedName == "P::Sys::inner::open" &&
                 e.TargetQualifiedName == "P::Sys::other::baseOpen");
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved reference"));
    }

    /// <summary>Returns the feature declared at <paramref name="qualifiedName"/>.</summary>
    private static SysmlFeatureNode FeatureAt(SysmlLoadResult result, string qualifiedName) =>
        Assert.IsType<SysmlFeatureNode>(result.Workspace!.Declarations[qualifiedName]);

    /// <summary>Returns the definition declared at <paramref name="qualifiedName"/>.</summary>
    private static SysmlDefinitionNode DefinitionAt(SysmlLoadResult result, string qualifiedName) =>
        Assert.IsType<SysmlDefinitionNode>(result.Workspace!.Declarations[qualifiedName]);

    /// <summary>
    ///     A leading <c>abstract</c> sets <c>IsAbstract</c> on definitions (SysML and KerML) and
    ///     features; a plain definition leaves it false.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AbstractModifier_SetsIsAbstract()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                abstract part def A;
                part def B;
                abstract class K;
                private abstract part def C;
                part def Holder { abstract part x; part y; }
            }
            """);

        // Assert
        Assert.True(DefinitionAt(result, "P::A").IsAbstract);
        Assert.False(DefinitionAt(result, "P::B").IsAbstract);
        Assert.True(DefinitionAt(result, "P::K").IsAbstract);
        Assert.True(DefinitionAt(result, "P::C").IsAbstract);
        Assert.True(FeatureAt(result, "P::Holder::x").IsAbstract);
        Assert.False(FeatureAt(result, "P::Holder::y").IsAbstract);
    }

    /// <summary>
    ///     <c>variation</c> definitions and usages set <c>IsVariation</c>; <c>variant</c> usages set
    ///     <c>IsVariant</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_VariationAndVariant_SetFlags()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Base;
                variation part def V { variant part v1 : Base; variant part v2 : Base; }
                part def Holder { variation part vv { variant part w : Base; } part plain; }
            }
            """);

        // Assert
        Assert.True(DefinitionAt(result, "P::V").IsVariation);
        Assert.False(DefinitionAt(result, "P::Base").IsVariation);
        Assert.True(FeatureAt(result, "P::V::v1").IsVariant);
        Assert.True(FeatureAt(result, "P::V::v2").IsVariant);
        Assert.False(FeatureAt(result, "P::V::v1").IsVariation);
        Assert.True(FeatureAt(result, "P::Holder::vv").IsVariation);
        Assert.False(FeatureAt(result, "P::Holder::vv").IsVariant);
        Assert.True(FeatureAt(result, "P::Holder::vv::w").IsVariant);
        Assert.False(FeatureAt(result, "P::Holder::plain").IsVariant);
    }

    /// <summary>
    ///     An <c>individual</c> prefix sets <c>IsIndividual</c> on definitions and usages.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_IndividualPrefix_SetsIsIndividual()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                individual part def Earth;
                part def Plain;
                part def Holder { individual part e : Earth; part p : Plain; }
            }
            """);

        // Assert
        Assert.True(DefinitionAt(result, "P::Earth").IsIndividual);
        Assert.False(DefinitionAt(result, "P::Plain").IsIndividual);
        Assert.True(FeatureAt(result, "P::Holder::e").IsIndividual);
        Assert.False(FeatureAt(result, "P::Holder::p").IsIndividual);
    }

    /// <summary>
    ///     The <c>ref</c> keyword sets <c>IsReference</c> for both <c>ref part x</c> and
    ///     <c>ref x : T</c>, and combines with other modifiers.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RefPrefix_SetsIsReference()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def T;
                part def Holder {
                    ref part r : T;
                    ref x : T;
                    part plain : T;
                    abstract ref part both : T;
                }
            }
            """);

        // Assert
        Assert.True(FeatureAt(result, "P::Holder::r").IsReference);
        Assert.True(FeatureAt(result, "P::Holder::x").IsReference);
        Assert.False(FeatureAt(result, "P::Holder::plain").IsReference);
        Assert.True(FeatureAt(result, "P::Holder::both").IsReference);
        Assert.True(FeatureAt(result, "P::Holder::both").IsAbstract);
    }

    /// <summary>
    ///     <c>in</c>/<c>out</c>/<c>inout</c> on usages that carry a direction keyword is captured as
    ///     <c>Direction</c>; usages without a direction keep it null.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FeatureDirection_Captured()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                port def Pd {
                    in item i;
                    out attribute o;
                    inout port q;
                    in abstract ref r;
                    attribute none;
                }
            }
            """);

        // Assert
        Assert.Equal(SysmlFeatureDirection.In, FeatureAt(result, "P::Pd::i").Direction);
        Assert.Equal(SysmlFeatureDirection.Out, FeatureAt(result, "P::Pd::o").Direction);
        Assert.Equal(SysmlFeatureDirection.InOut, FeatureAt(result, "P::Pd::q").Direction);
        var combined = FeatureAt(result, "P::Pd::r");
        Assert.Equal(SysmlFeatureDirection.In, combined.Direction);
        Assert.True(combined.IsAbstract);
        Assert.True(combined.IsReference);
        Assert.Null(FeatureAt(result, "P::Pd::none").Direction);
    }

    /// <summary>
    ///     A bare direction-only parameter (<c>in x : T;</c>) still produces no AST node, so no
    ///     direction is captured for it (decision D1: no new nodes).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_BareDirectionParameter_ProducesNoNode()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def T;
                action def Act { in x : T; out y : T; }
            }
            """);

        // Assert
        Assert.Empty(DefinitionAt(result, "P::Act").Children);
        Assert.DoesNotContain("P::Act::x", result.Workspace!.Declarations.Keys);
    }

    /// <summary>
    ///     The central modifier pass reaches state, action, requirement and enumeration bodies,
    ///     skips a leading <c>then</c>, and never flags the transitions synthesized from the same
    ///     body item.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ModifiersInSpecializedBodies_AppliedToUsagesOnly()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    abstract state s1;
                    state s2;
                    transition first s1 then s2;
                    abstract state s3;
                    accept sig then s2;
                }
                action def A {
                    abstract action a1;
                    then abstract action a2;
                    then action a3;
                }
                requirement def R { abstract attribute x; ref part y; }
                enum def E { enum one; }
            }
            """);

        // Assert
        Assert.True(FeatureAt(result, "P::S::s1").IsAbstract);
        Assert.False(FeatureAt(result, "P::S::s2").IsAbstract);
        Assert.True(FeatureAt(result, "P::S::s3").IsAbstract);
        Assert.Single(DefinitionAt(result, "P::S").Children.OfType<SysmlTransitionNode>(), t => t.Source == "s3");
        Assert.True(FeatureAt(result, "P::A::a1").IsAbstract);
        Assert.True(FeatureAt(result, "P::A::a2").IsAbstract);
        Assert.False(FeatureAt(result, "P::A::a3").IsAbstract);
        Assert.True(FeatureAt(result, "P::R::x").IsAbstract);
        Assert.True(FeatureAt(result, "P::R::y").IsReference);
        Assert.False(FeatureAt(result, "P::E::one").IsAbstract);
    }

    /// <summary>
    ///     <c>ordered</c> and <c>nonunique</c> are captured in either order, with or without a
    ///     <c>[..]</c> range, and are false when absent.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_OrderedNonunique_Captured()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def T;
                part def H {
                    part a : T[0..*] ordered;
                    part b : T[0..*] nonunique;
                    part c : T[0..*] ordered nonunique;
                    part d : T[0..*] nonunique ordered;
                    part e ordered;
                    part f : T[2];
                    part g;
                }
            }
            """);

        // Assert
        Assert.True(FeatureAt(result, "P::H::a").IsOrdered);
        Assert.False(FeatureAt(result, "P::H::a").IsNonunique);
        Assert.False(FeatureAt(result, "P::H::b").IsOrdered);
        Assert.True(FeatureAt(result, "P::H::b").IsNonunique);
        Assert.True(FeatureAt(result, "P::H::c").IsOrdered);
        Assert.True(FeatureAt(result, "P::H::c").IsNonunique);
        Assert.True(FeatureAt(result, "P::H::d").IsOrdered);
        Assert.True(FeatureAt(result, "P::H::d").IsNonunique);
        Assert.True(FeatureAt(result, "P::H::e").IsOrdered);
        Assert.Null(FeatureAt(result, "P::H::e").Multiplicity);
        Assert.False(FeatureAt(result, "P::H::f").IsOrdered);
        Assert.False(FeatureAt(result, "P::H::f").IsNonunique);
        Assert.False(FeatureAt(result, "P::H::g").IsOrdered);
    }

    /// <summary>
    ///     The whole value clause (operator included, whitespace preserved) is captured verbatim
    ///     as <c>ValueText</c>; usages without a value keep it null.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_FeatureValue_CapturedVerbatim()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def H {
                    attribute a = 5 [kg];
                    attribute b := 3;
                    attribute c default = 10;
                    attribute d default 7;
                    attribute e = 1 + 2 * x;
                    part f { attribute g = 4; }
                    attribute none;
                }
            }
            """);

        // Assert
        Assert.Equal("= 5 [kg]", FeatureAt(result, "P::H::a").ValueText);
        Assert.Equal(":= 3", FeatureAt(result, "P::H::b").ValueText);
        Assert.Equal("default = 10", FeatureAt(result, "P::H::c").ValueText);
        Assert.Equal("default 7", FeatureAt(result, "P::H::d").ValueText);
        Assert.Equal("= 1 + 2 * x", FeatureAt(result, "P::H::e").ValueText);
        Assert.Null(FeatureAt(result, "P::H::f").ValueText);
        Assert.Equal("= 4", FeatureAt(result, "P::H::f::g").ValueText);
        Assert.Null(FeatureAt(result, "P::H::none").ValueText);
    }

    /// <summary>
    ///     <c>::&gt;</c>/<c>references</c> targets are captured raw in
    ///     <c>ReferenceSubsettingName</c> and are not added to <c>SupertypeNames</c>, while
    ///     <c>:&gt;</c> stays in <c>SupertypeNames</c>; no supertype edge or new diagnostic results.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ReferenceSubsetting_CapturedNotInSupertypeNames()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def T;
                part def H {
                    part owner : T;
                    part viaSymbol ::> owner;
                    part viaKeyword references owner;
                    part viaColon :> owner;
                }
            }
            """);

        // Assert
        var viaSymbol = FeatureAt(result, "P::H::viaSymbol");
        Assert.Equal("owner", viaSymbol.ReferenceSubsettingName);
        Assert.Empty(viaSymbol.SupertypeNames);
        Assert.Equal("owner", FeatureAt(result, "P::H::viaKeyword").ReferenceSubsettingName);
        var subset = FeatureAt(result, "P::H::viaColon");
        Assert.Null(subset.ReferenceSubsettingName);
        Assert.Equal(["owner"], subset.SupertypeNames);
        Assert.Empty(viaSymbol.ResolvedEdges);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved reference"));
    }

    /// <summary>
    ///     A conjugated port typing (<c>~PortT</c>) is captured as <c>ConjugatedFeatureTyping</c> /
    ///     <c>IsConjugated</c> while <c>FeatureTyping</c> stays null, so no typing edge or
    ///     diagnostic is produced.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ConjugatedPortTyping_CapturesNameKeepsTypingNull()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                port def PortT;
                part def H { port conj : ~PortT; port plain : PortT; }
            }
            """);

        // Assert
        var conj = FeatureAt(result, "P::H::conj");
        Assert.True(conj.IsConjugated);
        Assert.Equal("PortT", conj.ConjugatedFeatureTyping);
        Assert.Null(conj.FeatureTyping);
        Assert.DoesNotContain(conj.ResolvedEdges, e => e.Kind == SysmlEdgeKind.Typing);
        var plain = FeatureAt(result, "P::H::plain");
        Assert.False(plain.IsConjugated);
        Assert.Null(plain.ConjugatedFeatureTyping);
        Assert.Equal("PortT", plain.FeatureTyping);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved reference"));
    }

    /// <summary>
    ///     The full transition form captures trigger, effect and (unchanged) guard text; each is
    ///     null when absent.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TransitionTriggerGuardEffect_Captured()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    state a;
                    state b;
                    transition full first a accept sig via pp if x > 5 do act then b;
                    transition timed first a accept at 5 then b;
                    transition effectOnly first a do act then b;
                    transition bare first a then b;
                }
            }
            """);

        // Assert
        var full = Assert.IsType<SysmlTransitionNode>(result.Workspace!.Declarations["P::S::full"]);
        Assert.Equal("sig via pp", full.Trigger);
        Assert.Equal("act", full.Effect);
        Assert.Equal("x>5", full.Guard);
        var timed = Assert.IsType<SysmlTransitionNode>(result.Workspace.Declarations["P::S::timed"]);
        Assert.Equal("at 5", timed.Trigger);
        Assert.Null(timed.Effect);
        Assert.Null(timed.Guard);
        var effectOnly = Assert.IsType<SysmlTransitionNode>(result.Workspace.Declarations["P::S::effectOnly"]);
        Assert.Null(effectOnly.Trigger);
        Assert.Equal("act", effectOnly.Effect);
        var bare = Assert.IsType<SysmlTransitionNode>(result.Workspace.Declarations["P::S::bare"]);
        Assert.Null(bare.Trigger);
        Assert.Null(bare.Effect);
    }

    /// <summary>
    ///     Attached transitions (<c>accept sig then a;</c>, <c>if g do e then b;</c>) capture trigger,
    ///     guard and effect.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AttachedTransition_CapturesTriggerGuardEffect()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    state a;
                    state b;
                    state d;
                    accept sig then a;
                    if g do e then b;
                }
            }
            """);

        // Assert
        var transitions = DefinitionAt(result, "P::S").Children.OfType<SysmlTransitionNode>().ToList();
        Assert.Equal(2, transitions.Count);
        var withTrigger = Assert.Single(transitions, t => t.Trigger == "sig");
        Assert.Equal("d", withTrigger.Source);
        Assert.Null(withTrigger.Effect);
        var withGuard = Assert.Single(transitions, t => t.Guard == "g");
        Assert.Equal("e", withGuard.Effect);
        Assert.Null(withGuard.Trigger);
    }

    /// <summary>
    ///     Transitions synthesized from an entry action's attached successions and from action-flow
    ///     target successions carry no trigger or effect.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SynthesizedSuccessions_TriggerEffectNull()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    entry action initial;
                    then off;
                    state off;
                }
                action def A {
                    action a1;
                    action a2;
                    first a1 if true then a2;
                }
            }
            """);

        // Assert
        var transitions = DefinitionAt(result, "P::S").Children.OfType<SysmlTransitionNode>()
            .Concat(DefinitionAt(result, "P::A").Children.OfType<SysmlTransitionNode>()).ToList();
        Assert.NotEmpty(transitions);
        Assert.All(transitions, t =>
        {
            Assert.Null(t.Trigger);
            Assert.Null(t.Effect);
        });
    }

    /// <summary>
    ///     <c>entry</c>/<c>do</c>/<c>exit</c> sub-actions capture their declaration text (body
    ///     excluded); the empty form and non-action features keep it null, and names are unchanged.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StateEntryDoExitActions_CaptureActionText()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    entry action initial;
                    do action working { }
                    exit foo;
                    entry accept sig;
                    state plain;
                }
            }
            """);

        // Assert
        var children = DefinitionAt(result, "P::S").Children.OfType<SysmlFeatureNode>().ToList();
        var entryNamed = children.Single(c => c.FeatureKeyword == "entry" && c.Name == "initial");
        Assert.Equal("action initial", entryNamed.ActionText);
        var doNamed = children.Single(c => c.FeatureKeyword == "do");
        Assert.Equal("working", doNamed.Name);
        Assert.Equal("action working", doNamed.ActionText);
        Assert.Equal("foo", children.Single(c => c.FeatureKeyword == "exit").ActionText);
        Assert.Equal("accept sig", children.Single(c => c.FeatureKeyword == "entry" && c.Name is null).ActionText);
        Assert.Null(children.Single(c => c.FeatureKeyword == "state").ActionText);
    }

    /// <summary>
    ///     An empty <c>entry;</c> action has no action text.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_EmptyStateEntryAction_ActionTextNull()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S { entry; do; }
            }
            """);

        // Assert
        var children = DefinitionAt(result, "P::S").Children.OfType<SysmlFeatureNode>().ToList();
        Assert.Equal(2, children.Count);
        Assert.All(children, c => Assert.Null(c.ActionText));
    }

    /// <summary>
    ///     A succession declared with <c>else</c> is flagged <c>IsElse</c> and carries no guard; a
    ///     guarded and an unconditional succession are not flagged.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ElseSuccession_SetsIsElse()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def A {
                    action a;
                    action b;
                    action c;
                    first a then d1;
                    decide d1;
                    if ok then b;
                    else c;
                    first b then c;
                }
            }
            """);

        // Assert
        var successions = DefinitionAt(result, "P::A").Children.OfType<SysmlTransitionNode>().ToList();
        var elseBranch = Assert.Single(successions, t => t.IsElse);
        Assert.Equal("d1", elseBranch.Source);
        Assert.Equal("c", elseBranch.Target);
        Assert.Null(elseBranch.Guard);
        var guarded = Assert.Single(successions, t => t.Guard == "ok");
        Assert.False(guarded.IsElse);
        Assert.Equal("b", guarded.Target);
        Assert.All(successions.Where(t => t != elseBranch), t => Assert.False(t.IsElse));
    }

    /// <summary>
    ///     A <c>terminate</c> node becomes a feature with the <c>terminate</c> keyword; an unnamed one
    ///     receives a synthetic name and can be the target of a succession.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TerminateNode_ProducesTerminateFeature()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def A {
                    action a;
                    then terminate;
                    action b;
                    then action halt terminate;
                }
            }
            """);

        // Assert
        var children = DefinitionAt(result, "P::A").Children;
        var terminates = children.OfType<SysmlFeatureNode>().Where(f => f.FeatureKeyword == "terminate").ToList();
        Assert.Equal(2, terminates.Count);
        Assert.Contains(terminates, t => t.Name is not null && t.Name.StartsWith("$terminate", StringComparison.Ordinal));
        Assert.Contains(terminates, t => t.Name == "halt");
        Assert.Contains(children.OfType<SysmlTransitionNode>(), t => t.Source == "a" && t.Target is not null);
    }

    /// <summary>
    ///     An action usage captures its feature typing and multiplicity.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ActionUsage_CapturesTypingAndMultiplicity()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def Step;
                action def A {
                    action single : Step;
                    action many : Step [2];
                    action plain;
                }
            }
            """);

        // Assert
        var children = DefinitionAt(result, "P::A").Children.OfType<SysmlFeatureNode>().ToList();
        Assert.Equal("Step", children.Single(f => f.Name == "single").FeatureTyping);
        var many = children.Single(f => f.Name == "many");
        Assert.Equal("Step", many.FeatureTyping);
        Assert.Equal("[2]", many.Multiplicity);
        var plain = children.Single(f => f.Name == "plain");
        Assert.Null(plain.FeatureTyping);
        Assert.Null(plain.Multiplicity);
    }

    /// <summary>
    ///     <c>accept</c> and <c>send</c> nodes keep their payload clause (original spacing) as action
    ///     text.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_AcceptSendNodes_CapturePayloadText()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def A {
                    action waitForIt accept Approval;
                    action tell send report to carrier;
                }
            }
            """);

        // Assert
        var children = DefinitionAt(result, "P::A").Children.OfType<SysmlFeatureNode>().ToList();
        Assert.Equal("accept Approval", children.Single(f => f.Name == "waitForIt").ActionText);
        Assert.Equal("send report to carrier", children.Single(f => f.Name == "tell").ActionText);
    }

    /// <summary>
    ///     <c>parallel</c> on a state definition or state usage sets <c>IsParallel</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ParallelState_SetsIsParallel()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def Par parallel { state a; state b; }
                state def Seq { state a; state b; }
                part def H {
                    state p parallel { state a; state b; }
                    state q { state a; state b; }
                }
            }
            """);

        // Assert
        Assert.True(DefinitionAt(result, "P::Par").IsParallel);
        Assert.False(DefinitionAt(result, "P::Seq").IsParallel);
        Assert.True(FeatureAt(result, "P::H::p").IsParallel);
        Assert.False(FeatureAt(result, "P::H::q").IsParallel);
    }

    /// <summary>
    ///     Action-flow control nodes (<c>decide</c>, <c>merge</c>, <c>fork</c>, <c>join</c>,
    ///     <c>accept</c>, <c>send</c>) are never parallel and carry no spurious notation data;
    ///     <c>IsParallel</c> belongs only to state definitions and state usages.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ActionControlNodes_NoParallelOrNotationData()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def Flow {
                    action work;
                    decide d;
                    merge m;
                    fork f;
                    join j;
                    accept a1;
                    send s1;
                }
            }
            """);

        // Assert
        var controlNodes = DefinitionAt(result, "P::Flow").Children.OfType<SysmlFeatureNode>().ToList();
        Assert.NotEmpty(controlNodes);
        Assert.All(controlNodes, n =>
        {
            Assert.False(n.IsParallel);
            Assert.Null(n.ValueText);
            Assert.False(n.IsOrdered);
            Assert.False(n.IsNonunique);
            Assert.Null(n.ReferenceSubsettingName);
        });
    }

    /// <summary>
    ///     Specialized usage builders (action, state, requirement, concern, constraint) capture the
    ///     same ordered/nonunique, value, and reference-subsetting data as the generic usage path.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SpecializedUsages_CaptureNotationData()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def T;
                part def H {
                    part owner : T;
                    action act [2] ordered nonunique ::> owner = 7;
                    state st [2] ordered ::> owner := 3;
                    requirement req [2] nonunique ::> owner = 4 { }
                    concern con ordered ::> owner default = 5 { }
                    constraint cst ::> owner = 6 { x > 1 }
                    action plainAct;
                    state plainSt;
                    requirement plainReq { }
                    concern plainCon { }
                    constraint plainCst { x > 1 }
                }
            }
            """);

        // Assert
        var act = FeatureAt(result, "P::H::act");
        Assert.True(act.IsOrdered);
        Assert.True(act.IsNonunique);
        Assert.Equal("= 7", act.ValueText);
        Assert.Equal("owner", act.ReferenceSubsettingName);

        var st = FeatureAt(result, "P::H::st");
        Assert.True(st.IsOrdered);
        Assert.False(st.IsNonunique);
        Assert.Equal(":= 3", st.ValueText);
        Assert.Equal("owner", st.ReferenceSubsettingName);
        Assert.False(st.IsParallel);

        var req = FeatureAt(result, "P::H::req");
        Assert.False(req.IsOrdered);
        Assert.True(req.IsNonunique);
        Assert.Equal("= 4", req.ValueText);
        Assert.Equal("owner", req.ReferenceSubsettingName);

        var con = FeatureAt(result, "P::H::con");
        Assert.True(con.IsOrdered);
        Assert.Equal("default = 5", con.ValueText);
        Assert.Equal("owner", con.ReferenceSubsettingName);

        var cst = FeatureAt(result, "P::H::cst");
        Assert.Equal("= 6", cst.ValueText);
        Assert.Equal("owner", cst.ReferenceSubsettingName);

        foreach (var plain in new[] { "plainAct", "plainSt", "plainReq", "plainCon", "plainCst" })
        {
            var feature = FeatureAt(result, $"P::H::{plain}");
            Assert.Null(feature.ValueText);
            Assert.False(feature.IsOrdered);
            Assert.False(feature.IsNonunique);
            Assert.Null(feature.ReferenceSubsettingName);
        }
    }

    /// <summary>
    ///     Leading modifiers (<c>abstract</c>, <c>variation</c>, <c>ref</c>, direction) are captured
    ///     on specialized usages (state, action, requirement) as well as on generic usages.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SpecializedUsages_CaptureLeadingModifiers()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def H {
                    abstract state st;
                    abstract action act;
                    abstract requirement req { }
                    in item inItem;
                }
            }
            """);

        // Assert
        Assert.True(FeatureAt(result, "P::H::st").IsAbstract);
        Assert.True(FeatureAt(result, "P::H::act").IsAbstract);
        Assert.True(FeatureAt(result, "P::H::req").IsAbstract);
        Assert.Equal(SysmlFeatureDirection.In, FeatureAt(result, "P::H::inItem").Direction);
    }

    /// <summary>
    ///     A succession target naming a <em>private</em> member of the enclosing action's supertype
    ///     (<c>then hidden;</c>) is not visible to ordinary name resolution and must not be rescued by
    ///     the <c>start</c>/<c>done</c> fallback: the target-side fallback is limited to exactly
    ///     those two names, so the reference still warns. The <c>start</c> in the same body resolves.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PrivateInheritedMemberAsSuccessionTarget_StillWarns()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def Base { private action hidden; }
                action def A :> Base {
                    first start;
                    then hidden;
                }
            }
            """);

        // Assert
        Assert.Contains(result.Diagnostics, d => d.Message == "Unresolved reference: 'hidden'");
        Assert.DoesNotContain(result.Diagnostics, d => d.Message == "Unresolved reference: 'start'");
    }

    /// <summary>
    ///     <c>then done</c> inside a part definition that explicitly specializes
    ///     <c>Parts::Part</c> is not an action or state body, so the pseudo-feature fallback does not
    ///     apply; it resolves only through ordinary inherited-member lookup, to the part's own
    ///     <c>Parts::Part::done</c> and never to <c>Actions::Action::done</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DoneTarget_InPartDefSpecializingPart_DoesNotUseActionFallback()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Q :> Parts::Part {
                    action step;
                    first step then done;
                }
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "P::Q::step", "Parts::Part::done"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Transition, "P::Q::step", "Actions::Action::done"));
    }

    /// <summary>
    ///     A model that uses none of the new syntax leaves every new member at its default.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_PlainModel_AllNewMembersDefault()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def T;
                part def H { part x : T[2]; attribute a : T; port p : T; }
            }
            """);

        // Assert
        var definition = DefinitionAt(result, "P::H");
        Assert.False(definition.IsAbstract);
        Assert.False(definition.IsVariation);
        Assert.False(definition.IsIndividual);
        Assert.False(definition.IsParallel);
        foreach (var feature in definition.Children.OfType<SysmlFeatureNode>())
        {
            Assert.False(feature.IsAbstract);
            Assert.False(feature.IsVariation);
            Assert.False(feature.IsIndividual);
            Assert.False(feature.IsVariant);
            Assert.False(feature.IsReference);
            Assert.Null(feature.Direction);
            Assert.False(feature.IsOrdered);
            Assert.False(feature.IsNonunique);
            Assert.Null(feature.ValueText);
            Assert.Null(feature.ReferenceSubsettingName);
            Assert.Null(feature.ConjugatedFeatureTyping);
            Assert.False(feature.IsConjugated);
            Assert.Null(feature.ActionText);
            Assert.False(feature.IsParallel);
        }
    }

    /// <summary>
    ///     Every kind of unresolved reference reports the one-based line and one-based column of
    ///     the reference text as written, not <c>(0,0)</c>. Each source contains the unresolved
    ///     <paramref name="reference"/> exactly once, so its position in the source is the
    ///     expected position of the diagnostic.
    /// </summary>
    [Theory]
    [InlineData("package P {\n    part def A :> GhostSupertype;\n}\n", "GhostSupertype")]
    [InlineData("package P {\n    part def A {\n        part p : GhostTyping;\n    }\n}\n", "GhostTyping")]
    [InlineData("package P {\n    part def A {\n        part q;\n        part p :> GhostSubset;\n    }\n}\n", "GhostSubset")]
    [InlineData("package P {\n    part def A {\n        part p :>> GhostRedefine;\n    }\n}\n", "GhostRedefine")]
    [InlineData("package P {\n    import GhostImport::*;\n}\n", "GhostImport")]
    [InlineData("package P {\n    view V {\n        expose GhostExpose::*;\n    }\n}\n", "GhostExpose")]
    [InlineData("package P {\n    view v : GhostViewDef;\n}\n", "GhostViewDef")]
    [InlineData("package P {\n    part def A {\n        part a;\n        connect a to ghostConnect.feat;\n    }\n}\n", "ghostConnect.feat")]
    [InlineData("package P {\n    part def A {\n        part a;\n        bind a = ghostBind.feat;\n    }\n}\n", "ghostBind.feat")]
    [InlineData("package P {\n    state def S {\n        state s1;\n        transition first s1 then GhostTransition;\n    }\n}\n", "GhostTransition")]
    [InlineData("package P {\n    part def A {\n        @GhostMetadata;\n    }\n}\n", "GhostMetadata")]
    [InlineData("package P {\n    part def A;\n    dependency from A to GhostDependency;\n}\n", "GhostDependency")]
    [InlineData("package P {\n    part def A;\n    allocate A to GhostAllocate;\n}\n", "GhostAllocate")]
    [InlineData("package P {\n    requirement def R;\n    satisfy R by GhostSatisfy;\n}\n", "GhostSatisfy")]
    [InlineData("package P {\n    verification def VC {\n        objective {\n            verify GhostVerify;\n        }\n    }\n}\n", "GhostVerify")]
    [InlineData("package P {\n    view def V {\n        render ghostRender;\n    }\n}\n", "ghostRender")]
    [InlineData("package P {\n    view v {\n        render ghostRenderUsage;\n    }\n}\n", "ghostRenderUsage")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a if ghostGuard then b;\n    }\n}\n", "ghostGuard")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a if 1 < ghostOperand and true then b;\n    }\n}\n", "ghostOperand")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a accept GhostTrigger then b;\n    }\n}\n", "GhostTrigger")]
    [InlineData("package P {\n    attribute def Sig;\n    state def S {\n        state a;\n        state b;\n        transition first a accept Sig via ghostVia then b;\n    }\n}\n", "ghostVia")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a accept sig : GhostSignalType then b;\n    }\n}\n", "GhostSignalType")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a do ghostEffect then b;\n    }\n}\n", "ghostEffect")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        transition first a do send ghostSendEffect to b then b;\n    }\n}\n", "ghostSendEffect")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        if ghostAttachedGuard then b;\n    }\n}\n", "ghostAttachedGuard")]
    [InlineData("package P {\n    state def S {\n        state a;\n        state b;\n        accept GhostAttachedTrigger then b;\n    }\n}\n", "GhostAttachedTrigger")]
    [InlineData("package P {\n    state def S {\n        entry ghostEntry;\n    }\n}\n", "ghostEntry")]
    [InlineData("package P {\n    state def S {\n        exit action cleanup : GhostExitType;\n    }\n}\n", "GhostExitType")]
    [InlineData("package P {\n    action def A {\n        action a accept GhostPayload;\n    }\n}\n", "GhostPayload")]
    [InlineData("package P {\n    action def A {\n        action a send ghostSendOperand to ghostReceiver;\n    }\n}\n", "ghostSendOperand")]
    [InlineData("package P {\n    action def A {\n        action a send ghostSendOperand to ghostReceiver;\n    }\n}\n", "ghostReceiver")]
    [InlineData("package P {\n    action def A {\n        action a send 1 via ghostVia to ghostTo;\n    }\n}\n", "ghostVia")]
    [InlineData("package P {\n    action def A {\n        action a;\n        then decide d;\n        if ghostDecisionGuard then a;\n        else a;\n    }\n}\n", "ghostDecisionGuard")]
    [InlineData("package P {\n    part def A {\n        attribute x;\n        constraint { x <= ghostConstraint }\n    }\n}\n", "ghostConstraint")]
    [InlineData("package P {\n    constraint def C {\n        in x : ScalarValues::Real;\n        x > ghostConstraintDef\n    }\n}\n", "ghostConstraintDef")]
    [InlineData("package P {\n    requirement def R {\n        subject s;\n        require constraint { s.size > ghostRequired }\n    }\n}\n", "ghostRequired")]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedReference_ReportsSourcePosition(string source, string reference)
    {
        // Arrange: the expected position is that of the single occurrence of the reference text
        var expectedLine = 1 + source[..source.IndexOf(reference, StringComparison.Ordinal)].Count(c => c == '\n');
        var expectedColumn = source.IndexOf(reference, StringComparison.Ordinal) - (source.LastIndexOf('\n', source.IndexOf(reference, StringComparison.Ordinal)) + 1) + 1;

        // Act
        var result = await LoadModelsAsync(source);

        // Assert: exactly one warning for the unresolved text, located at the text
        var diagnostic = Assert.Single(
            result.Diagnostics, d => d.Message == $"Unresolved reference: '{reference}'");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(expectedLine, diagnostic.Line);
        Assert.Equal(expectedColumn, diagnostic.Column);
    }

    /// <summary>
    ///     An unresolved typing on an occurrence-kind usage built outside the generic usage builder
    ///     (requirement, concern, constraint, action, state, and the case-kind and calc usages) is
    ///     reported at the typing reference's own one-based line and column, not <c>(0,0)</c>.
    /// </summary>
    /// <param name="usageKeyword">The usage keyword as written in the source.</param>
    [Theory]
    [InlineData("requirement")]
    [InlineData("concern")]
    [InlineData("constraint")]
    [InlineData("action")]
    [InlineData("state")]
    [InlineData("use case")]
    [InlineData("analysis")]
    [InlineData("verification")]
    [InlineData("case")]
    [InlineData("calc")]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedUsageTyping_ReportsSourcePosition(string usageKeyword)
    {
        // Arrange: the typing is on line 2; its column follows the keyword, name and colon
        var source = $"package P {{\n    {usageKeyword} u : GhostTyping;\n}}\n";
        var expectedColumn = source.IndexOf("GhostTyping", StringComparison.Ordinal) - (source.IndexOf('\n') + 1) + 1;

        // Act
        var result = await LoadModelsAsync(source);

        // Assert
        var diagnostic = Assert.Single(
            result.Diagnostics, d => d.Message == "Unresolved reference: 'GhostTyping'");
        Assert.Equal(2, diagnostic.Line);
        Assert.Equal(expectedColumn, diagnostic.Column);
        Assert.Equal(new SourcePosition(2, expectedColumn), FeatureAt(result, "P::u").ReferencePositions["GhostTyping"]);
        Assert.Equal(2, FeatureAt(result, "P::u").SourceLine);
    }

    /// <summary>
    ///     A reference whose text is the same as the declaring element's own name is located at the
    ///     reference token, not at the declaration's identifier: a self-specialization, a
    ///     redefinition and a typing named like the declaration, and a reference to the
    ///     declaration's own short name.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ReferenceNamedLikeDeclaration_LocatedAtReferenceToken()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def A {
                    attribute x redefines x;
                }
                part y : y;
                part def <S> N :> S;
            }
            """);

        // Assert: the second x/y/S on each line is the reference (columns are one-based)
        var redefinition = FeatureAt(result, "P::A::x");
        Assert.Equal(new SourcePosition(3, 31), redefinition.ReferencePositions["x"]);
        var typing = FeatureAt(result, "P::y");
        Assert.Equal(new SourcePosition(5, 14), typing.ReferencePositions["y"]);
        var subclass = DefinitionAt(result, "P::N");
        Assert.Equal(new SourcePosition(6, 23), subclass.ReferencePositions["S"]);
    }

    /// <summary>
    ///     A cyclic specialization of a definition by itself is located at the supertype reference
    ///     that closes the cycle, not at the declaring identifier.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SelfSpecialization_ReportsReferenceColumn()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("part def SelfA :> SelfA;\n");

        // Assert: "part def SelfA :> " is 18 characters, so the reference starts at column 19
        var diagnostic = Assert.Single(
            result.Diagnostics, d => d.Message.StartsWith("Cyclic specialization", StringComparison.Ordinal));
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(19, diagnostic.Column);
    }

    /// <summary>
    ///     A syntax error is reported with a one-based line and a one-based column, the same
    ///     convention as every other diagnostic (the first character of the file is (1,1)).
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_SyntaxError_ReportsOneBasedColumn()
    {
        // Arrange / Act: the very first character is already invalid
        var result = await LoadModelsAsync("@@@ NOT VALID SYSML @@@");

        // Assert: the earliest error is at line 1, column 1
        var first = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).MinBy(d => d.Column);
        Assert.NotNull(first);
        Assert.Equal(1, first.Line);
        Assert.Equal(1, first.Column);
    }

    /// <summary>
    ///     A reference that is written on a later line than its owner's declaration is located at
    ///     the reference text, and a second unresolved reference of a different kind in the same
    ///     file is located at its own text.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedReferences_ReportDistinctPositions()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def A
                    :> Missing1;
                part def B { part p : Missing2; }
            }
            """);

        // Assert: Missing1 is on line 3 (column 12); Missing2 is on line 4 (column 27)
        var first = Assert.Single(result.Diagnostics, d => d.Message == "Unresolved reference: 'Missing1'");
        Assert.Equal(3, first.Line);
        Assert.Equal(12, first.Column);
        var second = Assert.Single(result.Diagnostics, d => d.Message == "Unresolved reference: 'Missing2'");
        Assert.Equal(4, second.Line);
        Assert.Equal(27, second.Column);
    }

    /// <summary>
    ///     An unresolved-reference diagnostic names the file that contains the reference, so
    ///     references in different files of one load are each located in their own file.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedReferencesInTwoFiles_ReportEachFilePath()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package A {\n    part def X :> MissingInFirst;\n}\n",
            "package B {\n\n    part def Y :> MissingInSecond;\n}\n");

        // Assert
        var first = Assert.Single(result.Diagnostics, d => d.Message.Contains("MissingInFirst"));
        var second = Assert.Single(result.Diagnostics, d => d.Message.Contains("MissingInSecond"));
        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.Equal(2, first.Line);
        Assert.Equal(3, second.Line);
    }

    /// <summary>
    ///     A cyclic specialization diagnostic is located at the supertype reference that closes the
    ///     cycle and names the file that declares it, instead of an empty path and <c>(0,0)</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CyclicSpecialization_ReportsFileAndPosition()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("part def CycA :> CycB;\npart def CycB :> CycA;\n");

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Message.StartsWith("Cyclic specialization", StringComparison.Ordinal));
        Assert.EndsWith(".sysml", diagnostic.FilePath, StringComparison.Ordinal);
        Assert.True(diagnostic.Line is 1 or 2, $"Unexpected line {diagnostic.Line}");
        Assert.Equal(18, diagnostic.Column);
    }

    /// <summary>
    ///     A circular import diagnostic is located at the import statement that closes the cycle
    ///     and names the file that contains it, instead of a namespace name and <c>(0,0)</c>.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_CircularImport_ReportsFileAndPosition()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("package CircA {\n    import CircB::*;\n}\npackage CircB {\n    import CircA::*;\n}\n");

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Message.StartsWith("Circular import", StringComparison.Ordinal));
        Assert.EndsWith(".sysml", diagnostic.FilePath, StringComparison.Ordinal);
        Assert.True(diagnostic.Line is 2 or 5, $"Unexpected line {diagnostic.Line}");
    }

    /// <summary>
    ///     Every occurrence-kind usage keeps its feature typing: the AST node carries the raw typing
    ///     text and the resolver records a typing edge to the definition, with no diagnostics.
    ///     Requirement, concern, constraint, use case, analysis, verification, case and calc usages
    ///     previously lost the typing (or were not captured at all).
    /// </summary>
    /// <param name="usageKeyword">The usage keyword as written in the source.</param>
    /// <param name="definitionKeyword">The matching definition keyword as written in the source.</param>
    /// <param name="featureKeyword">The <see cref="SysmlFeatureNode.FeatureKeyword"/> the usage is captured under.</param>
    [Theory]
    [InlineData("part", "part def", "part")]
    [InlineData("item", "item def", "item")]
    [InlineData("occurrence", "occurrence def", "occurrence")]
    [InlineData("action", "action def", "action")]
    [InlineData("state", "state def", "state")]
    [InlineData("requirement", "requirement def", "requirement")]
    [InlineData("concern", "concern def", "concern")]
    [InlineData("constraint", "constraint def", "constraint")]
    [InlineData("use case", "use case def", "use case")]
    [InlineData("analysis", "analysis def", "analysis")]
    [InlineData("verification", "verification def", "verification")]
    [InlineData("case", "case def", "case")]
    [InlineData("calc", "calc def", "calc")]
    public async Task WorkspaceLoader_LoadAsync_OccurrenceKindUsage_CapturesFeatureTypingAndEdge(
        string usageKeyword,
        string definitionKeyword,
        string featureKeyword)
    {
        // Arrange / Act: one definition of the kind and one usage typed by it
        var result = await LoadModelsAsync($"package P {{ {definitionKeyword} Def; {usageKeyword} u : Def; }}");

        // Assert: the usage is captured with its keyword and typing, and the typing edge resolves
        var usage = FeatureAt(result, "P::u");
        Assert.Equal(featureKeyword, usage.FeatureKeyword);
        Assert.Equal("Def", usage.FeatureTyping);
        Assert.True(HasEdge(result, SysmlEdgeKind.Typing, "P::u", "P::Def"));
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     A requirement usage keeps its multiplicity along with its typing, like a generic usage.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RequirementUsage_CapturesTypingAndMultiplicity()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                requirement def R;
                requirement many : R [2];
            }
            """);

        // Assert
        var usage = FeatureAt(result, "P::many");
        Assert.Equal("R", usage.FeatureTyping);
        Assert.Equal("[2]", usage.Multiplicity);
    }

    /// <summary>
    ///     A succession target <c>then done;</c> in an action body, and a transition target
    ///     <c>then done</c> in a state body, resolve <c>done</c> to the standard library's
    ///     <c>Actions::Action::done</c> with no "Unresolved reference" diagnostic.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DoneTarget_ResolvesToStdlibActionMember()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def A {
                    first start;
                    then step;
                    action step;
                    then done;
                }
                state def S {
                    entry; then idle;
                    state idle;
                    transition first idle then done;
                }
            }
            """);

        // Assert: neither pseudo-feature is reported, and both completion edges reach Action::done
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved reference", StringComparison.Ordinal));
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "P::A::step", "Actions::Action::done"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "P::S::idle", "Actions::Action::done"));
    }

    /// <summary>
    ///     <c>first start then x;</c> and the bare <c>first start;</c> resolve <c>start</c> to the
    ///     standard library's <c>Actions::Action::start</c> in an action body with no diagnostic.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StartSource_InActionBody_ResolvesToStdlibActionMember()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def A {
                    first start then step;
                    action step;
                }
            }
            """);

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved reference", StringComparison.Ordinal));
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "Actions::Action::start", "P::A::step"));
    }

    /// <summary>
    ///     The <c>start</c>/<c>done</c> fallback does not hide real mistakes: a succession target
    ///     that names nothing still produces an "Unresolved reference" warning, while the
    ///     <c>done</c> in the same body does not.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnknownSuccessionTarget_StillWarnsButDoneDoesNot()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                action def A {
                    first start;
                    then missing;
                    then done;
                }
            }
            """);

        // Assert
        Assert.Contains(result.Diagnostics, d => d.Message == "Unresolved reference: 'missing'");
        Assert.DoesNotContain(result.Diagnostics, d => d.Message == "Unresolved reference: 'done'");
        Assert.DoesNotContain(result.Diagnostics, d => d.Message == "Unresolved reference: 'start'");
    }

    /// <summary>
    ///     A name <c>done</c> used as a succession target outside a state or action (here an
    ///     occurrence definition, which inherits no <c>done</c>) is not given the pseudo-feature
    ///     fallback and is still reported.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DoneTarget_OutsideActionOrState_StillWarns()
    {
        // Arrange / Act: a succession inside an occurrence def is not an action body, and
        // Occurrences::Occurrence has no 'done' member to inherit
        var result = await LoadModelsAsync("""
            package P {
                occurrence def Q {
                    action step;
                    first step then done;
                }
            }
            """);

        // Assert
        Assert.Contains(result.Diagnostics, d => d.Message == "Unresolved reference: 'done'");
    }

    /// <summary>
    ///     <c>then done</c> inside a part definition with no written specialization resolves through
    ///     ordinary inherited lookup, because a part definition implicitly specializes
    ///     <c>Parts::Part</c>; the pseudo-feature fallback (<c>Actions::Action::done</c>) is not used.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_DoneTarget_InPartDefWithImplicitPart_Resolves()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def Q {
                    action step;
                    first step then done;
                }
            }
            """);

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Message == "Unresolved reference: 'done'");
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "P::Q::step", "Parts::Part::done"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Transition, "P::Q::step", "Actions::Action::done"));
    }

    /// <summary>
    ///     A state definition's <c>start</c>/<c>done</c> resolve (through the implicit
    ///     <c>States::StateAction</c> specialization) to a library member, and the transition edge
    ///     keeps the legacy <c>Actions::Action::start</c>/<c>done</c> target — the compatibility
    ///     canonicalization that keeps exported edges stable.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_StateStartAndDone_KeepLegacyActionTargets()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    entry; then idle;
                    state idle;
                    transition first idle then done;
                }
                state s {
                    first start then run;
                    state run;
                }
            }
            """);

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unresolved reference", StringComparison.Ordinal));
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "P::S::idle", "Actions::Action::done"));
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "Actions::Action::start", "P::s::run"));
        Assert.DoesNotContain(
            result.Workspace!.Index.AllEdges,
            e => e.Kind == SysmlEdgeKind.Transition &&
                 (e.SourceQualifiedName?.StartsWith("States::", StringComparison.Ordinal) == true ||
                  e.TargetQualifiedName.StartsWith("States::", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     The legacy canonicalization applies only to library members: a state the model itself
    ///     names <c>start</c> is the transition source, untouched.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UserDeclaredStartState_IsNotCanonicalized()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                state def S {
                    state start;
                    state next;
                    transition first start then next;
                }
            }
            """);

        // Assert
        Assert.True(HasEdge(result, SysmlEdgeKind.Transition, "P::S::start", "P::S::next"));
        Assert.False(HasEdge(result, SysmlEdgeKind.Transition, "Actions::Action::start", "P::S::next"));
    }

    /// <summary>
    ///     Implicit specializations are not declared relationships, so a model made only of them
    ///     (no written supertype anywhere) produces no cyclic-specialization warning.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ImplicitSpecializationsOnly_NoCycleWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                part def A; item def I; port def Q; action def Run; state def S; requirement def R;
                part a : A; item i : I; port q : Q; action run : Run; state s : S; requirement r : R;
                part spare; attribute x;
            }
            """);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics, d => d.Message.StartsWith("Cyclic specialization", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    ///     A user package named like a standard-library package (<c>Parts</c>) contributes
    ///     user-origin members: its own members are not in <see cref="SysmlWorkspace.StdlibNames"/>
    ///     (which holds exactly the embedded seed's qualified names), while real standard-library
    ///     members of <c>Parts</c> still are.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UserPackageNamedLikeStdlibPackage_MembersAreNotStdlibOrigin()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("package Parts { part def Wheel; }");

        // Assert
        var names = result.Workspace!.StdlibNames;
        Assert.DoesNotContain("Parts::Wheel", names);
        Assert.Contains("Parts::Part", names);
        Assert.Contains("Parts::Wheel", result.Workspace.Declarations.Keys);
    }

    /// <summary>
    ///     Expression-borne references that resolve — a guard naming a declared feature or a bare
    ///     <c>in</c> parameter, a named <c>accept</c> payload used in its own guard, trigger payload
    ///     types, effect and state-action references, action-flow <c>accept</c>/<c>send</c>/decision
    ///     references, and constraint expressions — produce no "Unresolved reference" warning.
    /// </summary>
    /// <param name="source">The model source; every reference in it resolves.</param>
    [Theory]
    [InlineData("""
        package P {
            state def S {
                attribute ok : ScalarValues::Boolean;
                state a;
                state b;
                transition first a if ok then b;
            }
        }
        """)]
    [InlineData("""
        package P {
            state def S {
                in ok : ScalarValues::Boolean;
                state a;
                state b;
                transition first a if ok then b;
            }
        }
        """)]
    [InlineData("""
        package P {
            attribute def Sig { attribute level : ScalarValues::Integer; }
            state def S {
                state a;
                state b;
                transition first a accept sig : Sig if sig.level > 3 then b;
                transition first b accept Sig then a;
            }
        }
        """)]
    [InlineData("""
        package P {
            attribute def Sig;
            state def S {
                port line;
                action beep;
                state a;
                state b;
                transition first a accept Sig via line do beep then b;
                transition first b do action inline then a;
            }
        }
        """)]
    [InlineData("""
        package P {
            state def S {
                action beep;
                entry beep;
                do action regulate;
                exit action cleanup;
                state a;
                accept ScalarValues::Real then a;
            }
        }
        """)]
    [InlineData("""
        package P {
            attribute def Order;
            action def Handle {
                attribute inStock : ScalarValues::Boolean;
                attribute parcel;
                part carrier;
                action receive accept Order;
                then decide stockCheck;
                if inStock then ship;
                else reject;
                action ship send parcel to carrier;
                action reject;
            }
        }
        """)]
    [InlineData("""
        package P {
            part def Vehicle {
                attribute mass : ScalarValues::Real;
                attribute limit : ScalarValues::Real;
                constraint { mass <= limit }
            }
            constraint def Limit { in x : ScalarValues::Real; x > 0 }
            requirement def R {
                subject v : Vehicle;
                in extra : ScalarValues::Real;
                require constraint { v.mass <= extra }
            }
        }
        """)]
    [InlineData("""
        package P {
            private import Views::*;
            view def V { render asTreeDiagram; }
        }
        """)]
    [InlineData("""
        package P {
            view def V { render Views::asInterconnectionDiagram; }
            view v { render Views::asTreeDiagram; }
        }
        """)]
    [InlineData("""
        package P {
            view def V {
                private import Views::*;
                render asElementTable;
            }
        }
        """)]
    [InlineData("""
        private import Views::*;
        package P {
            view def V { render asTreeDiagram; }
        }
        """)]
    public async Task WorkspaceLoader_LoadAsync_ExpressionReferencesDeclared_NoUnresolvedWarning(string source)
    {
        // Arrange / Act
        var result = await LoadModelsAsync(source);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Operators, literals, <c>self</c>, library functions (invoked or called through
    ///     <c>-&gt;</c>), selected members, named-argument names, and the names a body declares for
    ///     itself are not references: guards and constraints built from them produce no
    ///     "Unresolved reference" warning — the guard against flagging valid models.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_LibraryFunctionsAndOperatorsInExpressions_NoUnresolvedWarning()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package P {
                private import ScalarValues::*;
                state def S {
                    in flag : Boolean;
                    in count : Integer;
                    in items : Integer[*];
                    state a;
                    state b;
                    transition first a if flag and not (count > 2) or count == 0 xor false implies true then b;
                    transition first b if items->size() > 0 and isEmpty(items) and items->forAll{in i : Integer; i > 0} then a;
                    transition first a if self != null and items.size > 1 and count in 1..5 then b;
                    transition first b if count >= 1 ? flag : true then a;
                }
                part def Vehicle {
                    attribute items : Integer[*];
                    constraint { items->size() > 0 and isEmpty(items) and items->forAll{in i : Integer; i > 0} implies true }
                    constraint { self.items->isEmpty() or not (items->size() == 0) }
                }
            }
            """);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The same undeclared name written in two places is reported at each place — each
    ///     occurrence is a separate thing to fix — at its own one-based line and column.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_UnresolvedExpressionReferenceTwice_ReportsEachOccurrence()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package P {\n    state def S {\n        state a;\n        state b;\n" +
            "        transition first a if ghost then b;\n        transition first b if ghost then a;\n    }\n}\n");

        // Assert
        var positions = result.Diagnostics
            .Where(d => d.Message == "Unresolved reference: 'ghost'")
            .Select(d => (d.Line, d.Column))
            .ToList();
        Assert.Equal([(5, 31), (6, 31)], positions);
    }

    /// <summary>
    ///     A parameter declared by a nested expression body (<c>forAll{ in i : T; ... }</c>) is
    ///     local to it: it does not hide an undeclared <c>i</c> referenced outside that body, which
    ///     is still reported — once, at the outer reference.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_NestedBodyParameter_DoesNotHideOuterReference()
    {
        // Arrange / Act: the outer `i` (line 5, col 22) is undeclared; the inner `i` is the lambda's own
        var result = await LoadModelsAsync(
            "package P {\n    private import ScalarValues::*;\n    part def V {\n        attribute items : Integer[*];\n" +
            "        constraint { i > 0 and items->forAll{in i : Integer; i > 0} }\n    }\n}\n");

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Message == "Unresolved reference: 'i'");
        Assert.Equal((5, 22), (diagnostic.Line, diagnostic.Column));
    }

    /// <summary>
    ///     A transition's references are reported in source order — trigger, then guard, then
    ///     effect — not in the order the builder happens to collect them.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TransitionTriggerGuardEffect_ReportedInSourceOrder()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package P {\n    state def S {\n        state a;\n        state b;\n" +
            "        transition first a accept Sig if missingGuard do missingEffect then b;\n    }\n}\n");

        // Assert: diagnostics in emission order
        var names = result.Diagnostics
            .Where(d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal))
            .Select(d => d.Message["Unresolved reference: '".Length..^1])
            .ToList();
        Assert.Equal(["Sig", "missingGuard", "missingEffect"], names);
    }

    /// <summary>
    ///     The references of a transition's trigger, guard and effect are captured on the node with
    ///     their token positions, in source order, and exclude operators, literals, the payload's own
    ///     name, library-function calls and member selections.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_TransitionExpressions_CaptureReferenceOccurrences()
    {
        // Arrange / Act
        var result = await LoadModelsAsync(
            "package P {\n    state def S {\n        state a;\n        state b;\n" +
            "        transition first a accept sig : Sig via line if sig.x > count and not flag do beep then b;\n    }\n}\n");

        // Assert
        var transition = Assert.Single(
            DefinitionAt(result, "P::S").Children.OfType<SysmlTransitionNode>(),
            t => t.Source == "a");
        Assert.Equal(
            [
                new ReferenceOccurrence("Sig", new SourcePosition(5, 41)),
                new ReferenceOccurrence("line", new SourcePosition(5, 49)),
                new ReferenceOccurrence("count", new SourcePosition(5, 65)),
                new ReferenceOccurrence("flag", new SourcePosition(5, 79)),
                new ReferenceOccurrence("beep", new SourcePosition(5, 87)),
            ],
            transition.ReferenceOccurrences.OrderBy(o => o.Position.Column));
    }

    /// <summary>
    ///     A <c>rendering</c> usage is a declaration: the standard library's
    ///     <c>Views::asTreeDiagram</c> exists, so a view's <c>render Views::asTreeDiagram;</c>
    ///     resolves, and a user <c>rendering</c> usage is registered with its keyword.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_RenderingUsage_IsDeclared()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("package P { rendering r : Views::GraphicalRendering; }");

        // Assert
        Assert.Contains("Views::asTreeDiagram", result.Workspace!.Declarations.Keys);
        Assert.Equal("rendering", FeatureAt(result, "P::r").FeatureKeyword);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     The elevator state-machine gallery model, as originally written, names a signal type, a
    ///     guard and an effect target that are not declared anywhere: each is reported — at the
    ///     token's line and column, matching the OMG Pilot's own unresolved-reference errors — and the
    ///     declared <c>dimLights</c>/<c>holdOpen</c>/<c>chime</c>/<c>sounder</c> actions are not.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ElevatorStateModel_ReportsUndeclaredNamesAtTokens()
    {
        // Arrange / Act
        var result = await LoadModelsAsync("""
            package ElevatorController {

                // An elevator control state machine.
                state def ElevatorStates {
                    state idle;
                    accept callReceived if doorsClear do action dimLights then doorsClosing;
                    state doorsOpening {
                        entry action openDoors;
                        do action holdOpen;
                        exit action chime;
                    }
                    state doorsOpen;
                    state doorsClosing {
                        entry action sounder;
                    }
                    state movingUp;
                    state movingDown;

                    first start then idle;
                    transition first doorsClosing if doorsClosed then movingUp;
                    transition first movingUp if atFloor then doorsOpening;
                    transition first movingDown if atFloor then doorsOpening;
                    transition first doorsOpening if doorsAreOpen then doorsOpen;
                    transition first doorsOpen accept timeout then doorsClosing;
                    transition first doorsClosing if goingDown then movingDown;
                    transition first doorsOpen accept shutdown then done;
                }

                view def ElevatorStateTransitionView {}
            }
            """);

        // Assert
        var reported = result.Diagnostics
            .Where(d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal))
            .OrderBy(d => d.Line).ThenBy(d => d.Column)
            .Select(d => (d.Message["Unresolved reference: '".Length..^1], d.Line, d.Column))
            .ToList();
        Assert.Equal(
            [
                ("callReceived", 6, 16),
                ("doorsClear", 6, 32),
                ("doorsClosed", 20, 42),
                ("atFloor", 21, 38),
                ("atFloor", 22, 40),
                ("doorsAreOpen", 23, 42),
                ("timeout", 24, 43),
                ("goingDown", 25, 42),
                ("shutdown", 26, 43),
            ],
            reported);
    }

    /// <summary>
    ///     Binary-compatibility tripwire: the original public two-parameter overload
    ///     <c>LoadAsync(IEnumerable&lt;string&gt;, SymbolTable? = null)</c> must remain in the
    ///     assembly metadata, so a consumer compiled against an earlier release does not throw
    ///     <see cref="MissingMethodException"/>.
    /// </summary>
    [Fact]
    public void WorkspaceLoader_LoadAsync_OriginalTwoParameterSignature_RemainsPublic()
    {
        // Arrange / Act
        var method = typeof(WorkspaceLoader).GetMethod(
            nameof(WorkspaceLoader.LoadAsync),
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(IEnumerable<string>), typeof(SymbolTable)],
            null);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<SysmlLoadResult>), method.ReturnType);
        var seedParameter = method.GetParameters()[1];
        Assert.True(seedParameter.IsOptional);
        Assert.True(seedParameter.HasDefaultValue);
        Assert.Null(seedParameter.DefaultValue);
    }

    /// <summary>
    ///     The original two-parameter overload, invoked through its exact metadata signature, behaves as
    ///     the three-parameter overload with <c>strictUnresolved: false</c>: an unresolved reference is
    ///     a Warning, not an Error. The shorter call forms still bind without ambiguity.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_OriginalTwoParameterOverload_ReportsUnresolvedAsWarning()
    {
        // Arrange
        var method = typeof(WorkspaceLoader).GetMethod(
            nameof(WorkspaceLoader.LoadAsync),
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(IEnumerable<string>), typeof(SymbolTable)],
            null);
        Assert.NotNull(method);
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                "package P { part def A :> MissingBase; }",
                TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();

            // Act
            var reflected = await (Task<SysmlLoadResult>)method.Invoke(null, [new[] { tempFile }, stdlibTable])!;
            var oneArgument = await WorkspaceLoader.LoadAsync([tempFile]);
            var twoArguments = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            var threeArguments = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable, strictUnresolved: false);

            // Assert
            static List<(string Message, DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity Severity)> Unresolved(
                SysmlLoadResult result) =>
                result.Diagnostics
                    .Where(d => d.Message.StartsWith("Unresolved reference:", StringComparison.Ordinal))
                    .Select(d => (d.Message, d.Severity))
                    .ToList();

            var expected = Unresolved(threeArguments);
            Assert.NotEmpty(expected);
            Assert.All(
                expected,
                d => Assert.Equal(DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Warning, d.Severity));
            Assert.Equal(expected, Unresolved(reflected));
            Assert.Equal(expected, Unresolved(twoArguments));
            Assert.NotEmpty(Unresolved(oneArgument));
            Assert.False(reflected.HasErrors);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

