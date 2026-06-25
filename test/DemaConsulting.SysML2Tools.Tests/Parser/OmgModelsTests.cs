// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.SysML2Tools.Parser;

namespace DemaConsulting.SysML2Tools.Tests.Parser;

/// <summary>
///     Tests that validate the parser against the OMG reference model files.
/// </summary>
public sealed class OmgModelsTests
{
    /// <summary>
    ///     Finds the test/SysMLModels/OMG directory relative to the test assembly.
    /// </summary>
    private static string FindOmgModelsRoot()
    {
        // Walk up from the test assembly location to find the repo root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "test", "SysMLModels", "OMG")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Cannot locate test/SysMLModels/OMG from test assembly location.");
        }

        return Path.Combine(dir.FullName, "test", "SysMLModels", "OMG");
    }

    /// <summary>
    ///     Every OMG reference model file (examples, training, validation) must
    ///     parse without syntax errors. This is the Phase 1 gate from the architecture.
    /// </summary>
    [Fact]
    public void Parse_OmgModels_NoSyntaxErrors()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = Directory.GetFiles(omgRoot, "*.sysml", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var result = WorkspaceParser.Parse(files);

        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            $"{errors.Count} syntax error(s) in OMG models:{Environment.NewLine}" +
            string.Join(Environment.NewLine, errors.Select(d =>
                $"  {d.FilePath}({d.Line},{d.Column}): {d.Message}")));
    }

    /// <summary>
    ///     Confirms all 251 expected OMG model files are present.
    /// </summary>
    [Fact]
    public void OmgModels_FileCount_IsExpected()
    {
        var omgRoot = FindOmgModelsRoot();
        var files = Directory.GetFiles(omgRoot, "*.sysml", SearchOption.AllDirectories);
        Assert.True(files.Length >= 251,
            $"Expected at least 251 OMG model files, found {files.Length}");
    }
}
