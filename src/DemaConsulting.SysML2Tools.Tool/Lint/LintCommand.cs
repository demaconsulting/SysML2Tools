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

using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Lint;

/// <summary>
///     Implements the <c>lint</c> command: parse the workspace and report diagnostics.
/// </summary>
internal static class LintCommand
{
    /// <summary>
    ///     Runs the lint command.
    /// </summary>
    /// <param name="context">The CLI context, supplying file patterns and output methods.</param>
    public static async Task RunAsync(Context context)
    {
        var files = ResolveFiles(context.Files);

        if (files.Count == 0)
        {
            context.WriteError("lint: no input files specified. Provide one or more .sysml or .kerml file paths.");
            return;
        }

        context.WriteLine($"Linting {files.Count} file(s)...");

        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var result = await WorkspaceLoader.LoadAsync(files, stdlibTable).ConfigureAwait(false);

        foreach (var diagnostic in result.Diagnostics)
        {
            var line = $"{diagnostic.FilePath}({diagnostic.Line},{diagnostic.Column}): {diagnostic.Severity.ToString().ToLowerInvariant()}: {diagnostic.Message}";
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                context.WriteError(line);
            }
            else
            {
                context.WriteLine(line);
            }
        }

        if (result.HasErrors)
        {
            context.WriteError($"lint: {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s) found.");
        }
        else
        {
            context.WriteLine("lint: no errors found.");
        }
    }

    /// <summary>
    ///     Resolves file glob patterns to concrete file paths.
    /// </summary>
    private static IReadOnlyList<string> ResolveFiles(IReadOnlyList<string> patterns)
    {
        var resolved = new List<string>();
        foreach (var pattern in patterns)
        {
            var dir = Path.GetDirectoryName(pattern) ?? ".";
            var glob = Path.GetFileName(pattern);

            if (string.IsNullOrEmpty(glob))
            {
                continue;
            }

            if (Directory.Exists(dir))
            {
                resolved.AddRange(Directory.GetFiles(dir, glob, SearchOption.TopDirectoryOnly));
            }
            else if (File.Exists(pattern))
            {
                resolved.Add(pattern);
            }
        }

        return resolved;
    }
}
