// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;

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
            var result = await WorkspaceLoader.LoadAsync([tempFile]);

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
            var result = await WorkspaceLoader.LoadAsync([tempFile]);

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
            var result = await WorkspaceLoader.LoadAsync([tempFile]);

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
            var result = await WorkspaceLoader.LoadAsync([tempFile]);

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
        var result = await WorkspaceLoader.LoadAsync([]);

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
        var result = await WorkspaceLoader.LoadAsync([]);

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
            var result = await WorkspaceLoader.LoadAsync([tempFile]);

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
            var result = await WorkspaceLoader.LoadAsync([tempFile]);

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

            // Act — must complete in finite time
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await WorkspaceLoader.LoadAsync([tempFile1, tempFile2])
                .WaitAsync(cts.Token);

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
}
