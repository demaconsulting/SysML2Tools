// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Semantic;

/// <summary>
///     Level 10 gate: all OMG example models and software-structure.sysml resolve with zero errors.
/// </summary>
public sealed class SemanticOmgModelsTests
{
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

    /// <summary>
    ///     All OMG reference model files (including software-structure.sysml) should load with zero errors.
    /// </summary>
    [Fact]
    public async Task SemanticOmgModels_AllModels_ResolveWithZeroErrors()
    {
        // Arrange
        var modelsRoot = FindSysMLModelsRoot();
        if (modelsRoot is null)
        {
            return;
        }

        var sysmlFiles = Directory.GetFiles(modelsRoot, "*.sysml", SearchOption.AllDirectories);
        if (sysmlFiles.Length == 0)
        {
            return;
        }

        // Act
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(sysmlFiles, stdlibTable);

        // Assert — no errors
        var errors = result.Diagnostics
            .Where(d => d.Severity == DemaConsulting.SysML2Tools.Parser.DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join("\n", errors.Select(e => $"  {e.FilePath}({e.Line},{e.Column}): {e.Message}"));
            Assert.Fail($"Expected zero errors but got {errors.Count}:\n{messages}");
        }
    }
}
