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
            var flow = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(
                result.Workspace!.Declarations["AF::Flow"]);
            var actions = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlFeatureNode>()
                .Count(f => f.FeatureKeyword == "action");
            Assert.Equal(2, actions);

            var succession = flow.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlTransitionNode>()
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
            var protocol = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(
                result.Workspace!.Declarations["Seq::Protocol"]);
            var message = protocol.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlConnectionNode>()
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("A::Child");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Supertype &&
                     e.TargetQualifiedName == "A::Ancestor");

            var incoming = result.Workspace.Index.GetIncomingEdges("A::Ancestor");
            Assert.Contains(incoming,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Supertype &&
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("Car::engine");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "Engine");

            var incoming = result.Workspace.Index.GetIncomingEdges("Engine");
            Assert.Contains(incoming,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Typing &&
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "NonExistentType");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     A wildcard import (<c>import Other::*;</c>) should be recorded as an <c>Import</c>
    ///     edge whose target is the imported namespace, queryable via
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Internal.SemanticIndex"/>'s
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
            var incoming = result.Workspace!.Index.GetIncomingEdges("Other");
            Assert.Contains(incoming, e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Import);
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
            var incoming = result.Workspace!.Index.GetIncomingEdges("Other::Thing");
            Assert.Contains(incoming, e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Import);
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("A::Baz");
            Assert.Contains(outgoing,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Supertype &&
                     e.TargetQualifiedName == "A::Foo");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An import referencing a nonexistent namespace should produce a Warning diagnostic and
    ///     must not crash <see cref="WorkspaceLoader.LoadAsync"/>.
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
            var index = result.Workspace!.Index;

            // Supertype edge, both directions
            Assert.Contains(index.GetOutgoingEdges("App::Gadget"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Supertype &&
                     e.TargetQualifiedName == "Lib::Ancestor");
            Assert.Contains(index.GetIncomingEdges("Lib::Ancestor"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Supertype &&
                     e.SourceQualifiedName == "App::Gadget");

            // Typing edge, both directions
            Assert.Contains(index.GetOutgoingEdges("App::Gadget::core"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Typing &&
                     e.TargetQualifiedName == "Lib::Widget");
            Assert.Contains(index.GetIncomingEdges("Lib::Widget"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Typing &&
                     e.SourceQualifiedName == "App::Gadget::core");

            // Import edge, incoming direction (anonymous import node has no source)
            Assert.Contains(index.GetIncomingEdges("Lib"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Import &&
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy &&
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy);
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy);
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy);
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify &&
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify &&
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify &&
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify);
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Allocate &&
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Allocate);
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
            var index = result.Workspace!.Index;

            // Satisfy edge, both directions
            Assert.Contains(index.GetOutgoingEdges("P::subj"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy &&
                     e.TargetQualifiedName == "P::req");
            Assert.Contains(index.GetIncomingEdges("P::req"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy &&
                     e.SourceQualifiedName == "P::subj");

            // Verify edge, both directions
            Assert.Contains(index.GetOutgoingEdges("P::C"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify &&
                     e.TargetQualifiedName == "P::R");
            Assert.Contains(index.GetIncomingEdges("P::R"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify &&
                     e.SourceQualifiedName == "P::C");

            // Allocate edge, both directions
            Assert.Contains(index.GetOutgoingEdges("P::a"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Allocate &&
                     e.TargetQualifiedName == "P::b");
            Assert.Contains(index.GetIncomingEdges("P::b"),
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Allocate &&
                     e.SourceQualifiedName == "P::a");
        }
        finally
        {
            File.Delete(tempFile);
        }
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy);
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Satisfy);
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Allocate);
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Internal.SysmlEdgeKind.Verify);
    }

    /// <summary>
    ///     An element with a single <c>comment</c> member and no <c>doc</c> captures one
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Comment"/>
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
            var package = result.Workspace!.Declarations["P"];
            var annotation = Assert.Single(package.Annotations);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Comment, annotation.Kind);
            Assert.Equal(" a note about P ", annotation.Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An element with a single <c>doc</c> member and no <c>comment</c> captures one
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Documentation"/>
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
            var package = result.Workspace!.Declarations["P"];
            var annotation = Assert.Single(package.Annotations);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Documentation, annotation.Kind);
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
            var package = result.Workspace!.Declarations["P"];
            Assert.Equal(2, package.Annotations.Count);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Comment, package.Annotations[0].Kind);
            Assert.Equal(" first: a comment ", package.Annotations[0].Text);
            Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Documentation, package.Annotations[1].Kind);
            Assert.Equal(" second: a doc ", package.Annotations[1].Text);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     An element with no <c>comment</c>/<c>doc</c> members has an empty (never null)
    ///     <see cref="DemaConsulting.SysML2Tools.Semantic.Internal.SysmlNode.Annotations"/> list.
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
            var package = result.Workspace!.Declarations["P"];
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
            var package = result.Workspace!.Declarations["P"];
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
        var package = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlPackageNode>(
            result.Workspace!.Declarations["'Documentation Example'"]);
        var packageDoc = Assert.Single(package.Annotations);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Documentation, packageDoc.Kind);
        Assert.Contains("This is documentation of the owning", packageDoc.Text);
        Assert.Contains("package.", packageDoc.Text);

        // Assert — part-def-level named doc
        var automobile = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Internal.SysmlDefinitionNode>(
            result.Workspace!.Declarations["'Documentation Example'::Automobile"]);
        var automobileDoc = Assert.Single(automobile.Annotations);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Internal.SysmlAnnotationKind.Documentation, automobileDoc.Kind);
        Assert.Equal(" This documentation of Automobile. ", automobileDoc.Text);
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

