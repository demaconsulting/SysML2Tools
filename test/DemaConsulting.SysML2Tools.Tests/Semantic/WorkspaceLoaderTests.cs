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
            var vehicle = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace!.Declarations["Demo::Vehicle"]);
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
                result.Workspace!.Declarations["Demo::Drivetrain"]);
            var connection = drivetrain.Children
                .OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlConnectionNode>()
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
            var light = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
                result.Workspace!.Declarations["SM::Light"]);
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
                result.Workspace!.Declarations["AF::Flow"]);
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
                result.Workspace!.Declarations["Seq::Protocol"]);
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("A::Child");
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("Car::engine");
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
                result.Workspace!.Declarations["SmallVehicle"]);
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
                result.Workspace!.Declarations["SmallVehicle"]);
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
                result.Workspace!.Declarations["Car"]);
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
                result.Workspace!.Declarations["Vehicle"]);
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
                result.Workspace!.Declarations["Vehicle"]);
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("SmallVehicle::smallEng");
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
    ///     <em>before</em> the file containing its ancestors in <see cref="WorkspaceLoader.LoadAsync"/>'s
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            var thingNode = result.Workspace!.Declarations["Thing"];
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
            var incoming = result.Workspace!.Index.GetIncomingEdges("Other");
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
            var incoming = result.Workspace!.Index.GetIncomingEdges("Other::Thing");
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
            var outgoing = result.Workspace!.Index.GetOutgoingEdges("A::Baz");
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            var edges = result.Workspace!.Index.AllEdges
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            var index = result.Workspace!.Index;

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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
                e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Transition &&
                     e.SourceQualifiedName == "P::Behavior::a" &&
                     e.TargetQualifiedName == "P::Behavior::b");
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
        Assert.Contains(result.Workspace!.Index.AllEdges,
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
            var package = result.Workspace!.Declarations["P"];
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
            var package = result.Workspace!.Declarations["P"];
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
            var package = result.Workspace!.Declarations["P"];
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
        var package = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlPackageNode>(
            result.Workspace!.Declarations["'Documentation Example'"]);
        var packageDoc = Assert.Single(package.Annotations);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation, packageDoc.Kind);
        Assert.Contains("This is documentation of the owning", packageDoc.Text);
        Assert.Contains("package.", packageDoc.Text);

        // Assert — part-def-level named doc
        var automobile = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(
            result.Workspace!.Declarations["'Documentation Example'::Automobile"]);
        var automobileDoc = Assert.Single(automobile.Annotations);
        Assert.Equal(DemaConsulting.SysML2Tools.Semantic.Model.SysmlAnnotationKind.Documentation, automobileDoc.Kind);
        Assert.Equal(" This documentation of Automobile. ", automobileDoc.Text);
    }

    /// <summary>
    ///     A <c>view def</c> with a <c>render &lt;target&gt;;</c> member naming a rendering-style
    ///     identifier that is not declared anywhere in the file (which would have failed
    ///     resolution under the old, incorrect content-scoping semantics) produces zero
    ///     diagnostics and zero edges sourced from the view — <c>ReferenceResolver</c> never
    ///     inspects <c>RenderTargetName</c> — while <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.RenderTargetName"/>
    ///     is still captured verbatim.
    /// </summary>
    [Fact]
    public async Task WorkspaceLoader_LoadAsync_ViewRenderTarget_CapturedRawNeverResolvedNoDiagnostic()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".sysml");
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                package P {
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
                result.Workspace!.Declarations["P::V"]);
            Assert.Equal("asTreeDiagram", view.RenderTargetName);
            Assert.Empty(result.Diagnostics);
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
                result.Workspace!.Declarations["P::V"]);
            Assert.Equal("@SysML::PartUsage", view.FilterExpressionText);
            Assert.Empty(result.Diagnostics);
            Assert.DoesNotContain(result.Workspace!.Index.AllEdges,
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
                result.Workspace!.Declarations["P::V"]);
            Assert.Contains("Exposed", view.GetExposedNames());
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
                result.Workspace!.Declarations["P::V"]);
            Assert.Contains("Exposed", view.GetExposedNames());
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
                result.Workspace!.Declarations["P::V"]);
            Assert.Contains("Exposed", view.GetExposedNames());
            Assert.Contains(result.Workspace!.Index.AllEdges,
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
            result.Workspace!.Declarations["'11b-Safety and Security Feaure Views'::Views::vehicleMandatorySafetyFeatureViewStandalone"]);
        Assert.NotEmpty(view.GetExposedNames());
        Assert.Contains(result.Workspace!.Index.AllEdges,
            e => e.Kind == DemaConsulting.SysML2Tools.Semantic.Model.SysmlEdgeKind.Expose &&
                 e.SourceQualifiedName == "'11b-Safety and Security Feaure Views'::Views::vehicleMandatorySafetyFeatureViewStandalone" &&
                 e.TargetQualifiedName == "'11b-Safety and Security Feaure Views'::PartsTree::vehicle");
        var member = Assert.Single(view.ExposeMembers);
        Assert.Equal("@Safety and (as Safety).isMandatory", member.BracketFilterExpressionText);
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
                result.Workspace!.Declarations["P::V"]);
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
                result.Workspace!.Declarations["AF::Flow"]);
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
                result.Workspace!.Declarations["AF::Flow"]);
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
                result.Workspace!.Declarations["AF::Flow"]);
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
                result.Workspace!.Declarations["AF::Flow"]);
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
                result.Workspace!.Declarations["AF::Flow"]);
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
    ///     instead of a synthesized one.
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
                result.Workspace!.Declarations["AF::Flow"]);
            var features = flow.Children.OfType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlFeatureNode>().ToList();

            Assert.Contains(features, f => f.FeatureKeyword == "fork" && f.Name == "f");
            Assert.Contains(features, f => f.FeatureKeyword == "join" && f.Name == "j");
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
                result.Workspace!.Declarations["AF::Flow"]);
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
        Assert.Equal(keyword, feature!.FeatureKeyword);
        Assert.Equal(typing, feature.FeatureTyping);
    }

    /// <summary>Asserts that the named declaration exists and is a definition with the given keyword.</summary>
    private static void AssertKeyword(SysmlWorkspace workspace, string qualifiedName, string expectedKeyword)
    {
        Assert.True(workspace.Declarations.TryGetValue(qualifiedName, out var node), $"Missing {qualifiedName}");
        var def = Assert.IsType<DemaConsulting.SysML2Tools.Semantic.Model.SysmlDefinitionNode>(node);
        Assert.Equal(expectedKeyword, def.DefinitionKeyword);
    }
}

