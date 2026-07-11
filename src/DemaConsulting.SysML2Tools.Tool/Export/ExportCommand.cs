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

using System.Text;
using System.Text.Json;
using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.Io;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Export;

/// <summary>
///     Implements the <c>export</c> command: loads a SysML v2 workspace and dumps its resolved
///     semantic model (declarations, edges, diagnostics) as a single JSON document or as JSON
///     Lines, for offline consumption by an AI/agent harness.
/// </summary>
/// <remarks>
///     Stdlib filtering mirrors <c>Query.QueryEngine.IsVisible</c> exactly (replicated locally,
///     since this Tool project cannot reference the Core project's internal
///     <c>StdlibFilter</c>): <see cref="ExportResult.Declarations"/> excludes stdlib keys, and
///     <see cref="ExportResult.Edges"/> excludes any edge whose source or target is a stdlib
///     name, unless <c>--include-stdlib</c> was supplied. Diagnostics are never stdlib-filtered:
///     <c>WorkspaceLoader</c> diagnostics only ever originate from the user's own files, because
///     the stdlib symbol table is a pre-resolved seed (<c>StdlibProvider.GetSymbolTable()</c>),
///     not re-parsed per invocation — see <c>docs/design/sysml2-tools-tool/export.md</c>.
/// </remarks>
internal static class ExportCommand
{
    /// <summary>
    ///     Runs the export command.
    /// </summary>
    /// <param name="context">The CLI context, supplying the parsed <see cref="ExportOptions"/> and output methods.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="Context.Export"/> is <see langword="null"/>, or when
    ///     <c>--format</c> is not <c>json</c>/<c>jsonl</c>.
    /// </exception>
    public static async Task RunAsync(Context context)
    {
        var options = context.Export
                      ?? throw new ArgumentException("export: no export options were parsed.", nameof(context));

        // Reject unsupported --format values up front, before doing any work, mirroring the
        // query/render commands' --format validation style.
        var format = options.Format ?? "json";
        if (!format.Equals("json", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("jsonl", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"export: unsupported --format value '{format}'. Valid values are: json, jsonl.",
                nameof(context));
        }

        // Validate that at least one file pattern was supplied
        if (options.Files.Count == 0)
        {
            context.WriteError("export: no input files specified. Provide one or more .sysml or .kerml file paths.");
            return;
        }

        // Resolve the supplied file glob patterns to concrete file paths via the shared
        // GlobFileCollector, supporting recursive '**' patterns and '!' exclusions.
        context.WriteLine($"Loading {options.Files.Count} file pattern(s)...");
        var files = GlobFileCollector.Collect(options.Files, [".sysml", ".kerml"], Directory.GetCurrentDirectory());
        if (files.Count == 0)
        {
            context.WriteError("export: no files matched the given pattern(s).");
            return;
        }

        context.WriteLine($"Resolved {files.Count} file(s) from {options.Files.Count} pattern(s).");

        // Load the workspace from the resolved file paths, exactly as 'lint'/'render'/'query' do
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var loadResult = await WorkspaceLoader.LoadAsync(files, stdlibTable).ConfigureAwait(false);

        if (loadResult.Workspace is null)
        {
            context.WriteError("export: workspace loading failed; no export produced.");
            return;
        }

        var workspace = loadResult.Workspace;

        // Build the filtered declarations/edges, applying the --include-stdlib convention
        var declarations = workspace.Declarations
            .Where(kv => IsVisible(kv.Key, workspace, options.IncludeStdlib))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var edges = workspace.Index.AllEdges
            .Where(edge =>
                (edge.SourceQualifiedName is null || IsVisible(edge.SourceQualifiedName, workspace, options.IncludeStdlib)) &&
                IsVisible(edge.TargetQualifiedName, workspace, options.IncludeStdlib))
            .ToList();

        // Diagnostics are never stdlib-filtered — see this class's remarks.
        var result = new ExportResult
        {
            Declarations = declarations,
            Edges = edges,
            Diagnostics = loadResult.Diagnostics
        };

        var rendered = format.Equals("jsonl", StringComparison.OrdinalIgnoreCase)
            ? RenderJsonLines(result)
            : JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult);

        // Write to --output file if specified, else stdout via context.WriteLine
        if (options.Output is not null)
        {
            await File.WriteAllTextAsync(options.Output, rendered).ConfigureAwait(false);
            context.WriteLine($"export: wrote {declarations.Count} declaration(s), {edges.Count} edge(s), " +
                               $"{result.Diagnostics.Count} diagnostic(s) to '{options.Output}'.");
        }
        else
        {
            context.WriteLine(rendered);
        }
    }

    /// <summary>
    ///     Renders an <see cref="ExportResult"/> as JSON Lines: one compact JSON object per
    ///     declaration, edge, and diagnostic, each tagged with a <c>"kind"</c> discriminator.
    /// </summary>
    /// <param name="result">The export result to render.</param>
    /// <returns>The rendered JSONL text, with one record per line.</returns>
    private static string RenderJsonLines(ExportResult result)
    {
        var builder = new StringBuilder();

        foreach (var (qualifiedName, node) in result.Declarations)
        {
            var line = ExportDeclarationLine.Create(qualifiedName, node);
            builder.AppendLine(JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportDeclarationLine));
        }

        foreach (var edge in result.Edges)
        {
            var line = ExportEdgeLine.Create(edge);
            builder.AppendLine(JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportEdgeLine));
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            var line = ExportDiagnosticLine.Create(diagnostic);
            builder.AppendLine(JsonSerializer.Serialize(line, ExportLineSerializerContext.Default.ExportDiagnosticLine));
        }

        // Trim the trailing newline so the rendered text matches the single-document JSON path's
        // convention of returning a value with no trailing line terminator.
        return builder.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    ///     Determines whether a qualified name should be included in the export output, given the
    ///     <c>--include-stdlib</c> option.
    /// </summary>
    /// <param name="qualifiedName">The qualified name to check.</param>
    /// <param name="workspace">The loaded workspace, supplying <see cref="SysmlWorkspace.StdlibNames"/>.</param>
    /// <param name="includeStdlib">The <c>--include-stdlib</c> option value.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="includeStdlib"/> is <see langword="true"/>,
    ///     or when <paramref name="qualifiedName"/> is not a standard-library name.
    /// </returns>
    /// <remarks>
    ///     Mirrors <c>Query.QueryEngine.IsVisible</c> exactly; replicated here (rather than
    ///     shared) because this Tool project cannot reference the Core project's internal
    ///     <c>StdlibFilter</c>, and <c>QueryEngine.IsVisible</c> is <see langword="private"/>.
    /// </remarks>
    private static bool IsVisible(string qualifiedName, SysmlWorkspace workspace, bool includeStdlib) =>
        includeStdlib || !workspace.StdlibNames.Contains(qualifiedName);

    /// <summary>
    ///     Prints help for the <c>export</c> command.
    /// </summary>
    /// <param name="context">The CLI context for output.</param>
    /// <remarks>
    ///     The single source of truth for both <c>export --help</c> and <c>help export</c> — see
    ///     <see cref="Help.HelpCommand"/> and <c>Program.RunAsync</c>'s command-aware help
    ///     dispatch.
    /// </remarks>
    public static void PrintHelp(Context context)
    {
        context.WriteLine(ExportStrings.Export_Usage);
        context.WriteLine("");
        context.WriteLine(ExportStrings.Export_Description1);
        context.WriteLine(ExportStrings.Export_Description2);
        context.WriteLine("");
        context.WriteLine(ExportStrings.Export_OptionsHeader);
        context.WriteLine(ExportStrings.Export_OptionFormat);
        context.WriteLine(ExportStrings.Export_OptionOutput1);
        context.WriteLine(ExportStrings.Export_OptionOutput2);
        context.WriteLine(ExportStrings.Export_OptionIncludeStdlib);
    }
}
