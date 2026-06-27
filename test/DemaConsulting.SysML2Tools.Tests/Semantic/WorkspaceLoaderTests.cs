// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;
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
            Assert.True(result.Workspace!.Declarations.ContainsKey("Foo"),
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
            Assert.True(result.Workspace!.Declarations.ContainsKey("A"), "Expected 'A'");
            Assert.True(result.Workspace!.Declarations.ContainsKey("A::B"), "Expected 'A::B'");
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
            Assert.True(result.Workspace!.Declarations.ContainsKey("P::W"),
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
        Assert.NotEmpty(result.Workspace!.Declarations);
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
        Assert.True(result.Workspace!.Declarations.Count > 0,
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
            Assert.True(result.Workspace!.Declarations.ContainsKey("P::Base"), "Expected 'P::Base'");
            Assert.True(result.Workspace!.Declarations.ContainsKey("P::Derived"), "Expected 'P::Derived'");
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
            AssertKeyword(result.Workspace!, "Demo::Vehicle", "part def");
            AssertKeyword(result.Workspace!, "Demo::FuelPort", "port def");
            AssertKeyword(result.Workspace!, "Demo::FuelInterface", "interface def");
            AssertKeyword(result.Workspace!, "Demo::MassReq", "requirement def");
            AssertKeyword(result.Workspace!, "Demo::Gear", "enum def");
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
            Assert.NotEmpty(result.Workspace!.StdlibNames);
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
            var vehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(
                result.Workspace!.Declarations["Demo::Vehicle"]);
            var features = vehicle.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlFeatureNode>()
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
            var drivetrain = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(
                result.Workspace!.Declarations["Demo::Drivetrain"]);
            var connection = drivetrain.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlConnectionNode>()
                .Single();
            Assert.Equal("engine", connection.EndpointA);
            Assert.Equal("gearbox", connection.EndpointB);
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
            var light = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(
                result.Workspace!.Declarations["SM::Light"]);
            var states = light.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlFeatureNode>()
                .Where(f => f.FeatureKeyword == "state")
                .ToList();
            Assert.Equal(2, states.Count);

            var transition = light.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlTransitionNode>()
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

    /// <summary>Asserts that a feature with the given name has the expected keyword and typing.</summary>
    private static void AssertFeature(
        IEnumerable<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlFeatureNode> features,
        string name,
        string keyword,
        string typing)
    {
        var feature = features.FirstOrDefault(f => f.Name == name);
        Assert.NotNull(feature);
        Assert.Equal(keyword, feature!.FeatureKeyword);
        Assert.Equal(typing, feature.FeatureTyping);
    }

    /// <summary>Asserts that the named declaration exists and is a definition with the given keyword.</summary>
    private static void AssertKeyword(SysmlWorkspace workspace, string qualifiedName, string expectedKeyword)
    {
        Assert.True(workspace.Declarations.TryGetValue(qualifiedName, out var node), $"Missing {qualifiedName}");
        var def = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(node);
        Assert.Equal(expectedKeyword, def.DefinitionKeyword);
    }
}

