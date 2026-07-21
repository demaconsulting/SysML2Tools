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
using DemaConsulting.SysML2Tools.Filtering;
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
///     Stdlib filtering mirrors <c>Query.QueryEngine.IsVisible</c> exactly (intentionally
///     duplicated locally — a one-line, no-state check now trivially shareable since
///     <c>QueryEngine.IsVisible</c> became part of Core's public API, but left un-shared here to
///     avoid growing Core's public surface for a one-line convenience and to avoid an
///     out-of-scope edit to this class): <see cref="ExportResult.Declarations"/> excludes stdlib
///     keys, and
///     <see cref="ExportResult.Edges"/> excludes any edge whose source or target is a stdlib
///     name, unless <c>--include-stdlib</c> was supplied. Diagnostics are never stdlib-filtered:
///     <c>WorkspaceLoader</c> diagnostics only ever originate from the user's own files, because
///     the stdlib symbol table is a pre-resolved seed (<c>StdlibProvider.GetSymbolTable()</c>),
///     not re-parsed per invocation — see <c>docs/design/sysml2-tools-tool/export.md</c>.
///     <para>
///     <c>--target</c>/<c>--filter</c> scoping (in that composition order — see
///     <see cref="RunAsync"/>'s remarks) are applied on top of the stdlib filtering above, not in
///     place of it: both narrowing steps are independent <c>&amp;&amp;</c>-ed predicates over the
///     same already-stdlib-filtered <c>declarations</c>/<c>edges</c> collections.
///     </para>
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
    /// <remarks>
    ///     Composition order for <c>--target</c>/<c>--filter</c> (mirroring
    ///     <c>GeneralViewLayoutStrategy</c>'s <c>expose</c>-then-<c>filter</c> pipeline):
    ///     <list type="number">
    ///         <item><description>
    ///         Stdlib filtering (<see cref="IsVisible"/>) is applied first, exactly as before this
    ///         feature existed.
    ///         </description></item>
    ///         <item><description>
    ///         <c>--target</c> (when supplied) narrows the stdlib-filtered declarations/edges to
    ///         the target's containment subtree next. An unresolvable or stdlib-hidden (without
    ///         <c>--include-stdlib</c>) target is a clean error — no export is produced.
    ///         </description></item>
    ///         <item><description>
    ///         <c>--filter</c> (when supplied) narrows the (possibly <c>--target</c>-scoped)
    ///         declaration set last, using <see cref="FilterExpressionParser"/>/
    ///         <see cref="FilterExpressionEvaluator"/> (both public <c>Core.Filtering</c> types,
    ///         reused directly — unlike the target-subtree helper below, no duplication is
    ///         needed here). A parse/evaluation failure does not abort the export: it falls back
    ///         to the unfiltered (but still target-scoped, if applicable) result, with a synthetic
    ///         warning <see cref="SysmlDiagnostic"/> appended to <see cref="ExportResult.Diagnostics"/>
    ///         (<c>FilePath = "&lt;--filter&gt;"</c>, mirroring the <c>[stdlib]…</c> virtual-path
    ///         convention already used for non-ordinary <see cref="SysmlDiagnostic.FilePath"/>
    ///         values) and a matching console warning via <see cref="Context.WriteLine"/>.
    ///         </description></item>
    ///     </list>
    ///     Edges always require <em>both</em> endpoints (when the source is non-null) to survive
    ///     every active narrowing step — stdlib filtering, <c>--target</c> subtree membership, and
    ///     <c>--filter</c> matching — consistent with the pre-existing dual-endpoint stdlib-edge
    ///     behavior (see class remarks); this is <em>not</em> "target-only" edge inclusion.
    /// </remarks>
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

        // Build the stdlib-filtered declarations/edges, applying the --include-stdlib convention,
        // exactly as before --target/--filter existed.
        var declarations = workspace.Declarations
            .Where(kv => IsVisible(kv.Key, workspace, options.IncludeStdlib))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var edges = workspace.Index.AllEdges
            .Where(edge =>
                (edge.SourceQualifiedName is null || IsVisible(edge.SourceQualifiedName, workspace, options.IncludeStdlib)) &&
                IsVisible(edge.TargetQualifiedName, workspace, options.IncludeStdlib))
            .ToList();

        // Diagnostics are never stdlib-filtered — see this class's remarks. Copied to a mutable
        // list up front so a --filter parse failure (below) can append a synthetic warning
        // diagnostic without mutating loadResult.Diagnostics itself.
        var diagnostics = loadResult.Diagnostics.ToList();

        // Apply --target subtree scoping first (see RunAsync's remarks for the composition order).
        if (options.Target is not null)
        {
            var subjects = ResolveTargetSubtreeSubjects(workspace, options.Target, options.IncludeStdlib);
            if (subjects is null)
            {
                context.WriteError($"export: --target '{options.Target}' was not found in the workspace.");
                return;
            }

            declarations = declarations
                .Where(kv => IsInTargetSubtree(kv.Key, subjects))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

            edges = edges
                .Where(edge =>
                    (edge.SourceQualifiedName is null || IsInTargetSubtree(edge.SourceQualifiedName, subjects)) &&
                    IsInTargetSubtree(edge.TargetQualifiedName, subjects))
                .ToList();
        }

        // Apply --filter narrowing second, over the (possibly --target-scoped) declaration set.
        if (options.FilterExpression is not null)
        {
            var parseResult = FilterExpressionParser.Parse(options.FilterExpression);
            if (parseResult.Expression is { } expression)
            {
                var evaluation = FilterExpressionEvaluator.Evaluate(workspace, declarations.Keys.ToList(), expression);
                var matched = new HashSet<string>(evaluation.MatchedQualifiedNames, StringComparer.Ordinal);

                declarations = declarations
                    .Where(kv => matched.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

                edges = edges
                    .Where(edge =>
                        (edge.SourceQualifiedName is null || matched.Contains(edge.SourceQualifiedName)) &&
                        matched.Contains(edge.TargetQualifiedName))
                    .ToList();
            }
            else
            {
                // Parse failure: fall back to the unfiltered (but still --target-scoped, if
                // applicable) result, surfacing the failure via both channels (see remarks).
                var reason = parseResult.Diagnostics.FirstOrDefault()?.Message ?? "unknown parse error";
                var message =
                    $"export: --filter expression '{options.FilterExpression}' failed to parse/evaluate: {reason}; exporting unfiltered.";

                diagnostics.Add(new SysmlDiagnostic("<--filter>", 0, 0, DiagnosticSeverity.Warning, message));
                context.WriteLine($"export: warning: --filter expression '{options.FilterExpression}' failed to parse/evaluate: {reason}; exporting unfiltered.");
            }
        }

        var result = new ExportResult
        {
            Declarations = declarations,
            Edges = edges,
            Diagnostics = diagnostics
        };

        var rendered = format.Equals("jsonl", StringComparison.OrdinalIgnoreCase)
            ? RenderJsonLines(result)
            : JsonSerializer.Serialize(result, ExportResultSerializerContext.Default.ExportResult);

        // Write to --output file if specified, else stdout via context.WriteLine
        if (options.Output is not null)
        {
            try
            {
                // Ensure the parent directory exists (mirroring 'render's Directory.CreateDirectory
                // guard for its --output directory), so a nonexistent output path fails cleanly
                // rather than throwing DirectoryNotFoundException from WriteAllTextAsync below.
                var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.Output));
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                await File.WriteAllTextAsync(options.Output, rendered).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Covers cases such as '--output' pointing at an existing directory (unlike
                // 'render', where --output is a directory), a read-only/locked target, or an
                // otherwise-invalid path, surfacing a clean error instead of an unhandled
                // exception with a stack trace.
                context.WriteError($"export: failed to write output file '{options.Output}': {ex.Message}");
                return;
            }

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
    ///     Mirrors <c>Query.QueryEngine.IsVisible</c> exactly. Intentionally kept as a small,
    ///     private duplicate rather than shared: <c>QueryEngine.IsVisible</c> is now part of
    ///     Core's public Query API surface, but it is a trivial, one-line, no-state check, so
    ///     making <c>ExportCommand</c> take a dependency on it (or promoting it to
    ///     <see langword="public"/> purely to let this duplicate be deleted) is not worth the
    ///     added coupling/public-API-surface growth for this one-line convenience.
    /// </remarks>
    private static bool IsVisible(string qualifiedName, SysmlWorkspace workspace, bool includeStdlib) =>
        includeStdlib || !workspace.StdlibNames.Contains(qualifiedName);

    /// <summary>
    ///     Resolves the containment-subtree "subject" qualified names that <c>--target</c> scopes
    ///     the export to, or <see langword="null"/> when <paramref name="target"/> does not resolve
    ///     to a visible declaration (either because it is genuinely absent from the workspace, or
    ///     because it names a standard-library declaration and <paramref name="includeStdlib"/> is
    ///     <see langword="false"/> — the caller reports a single unified "not found" error for
    ///     both cases, matching <see cref="IsVisible"/>'s existing exclude semantics elsewhere in
    ///     this file).
    /// </summary>
    /// <param name="workspace">The loaded workspace, supplying <see cref="SysmlWorkspace.Declarations"/>.</param>
    /// <param name="target">The raw <c>--target</c> qualified-name text.</param>
    /// <param name="includeStdlib">The <c>--include-stdlib</c> option value.</param>
    /// <returns>
    ///     The resolved subject qualified names (the target itself, plus — when the target
    ///     resolves to a usage/feature — its resolved type's qualified name, so a usage target
    ///     still yields useful subtree content), or <see langword="null"/> when the target is not
    ///     a visible declaration.
    /// </returns>
    /// <remarks>
    ///     Duplicates (in miniature) the whole-subtree-subject logic of Core's internal
    ///     <c>DemaConsulting.SysML2Tools.Layout.Internal.ExposeScopeResolver.AddWholeSubtreeSubject</c>
    ///     (usage-to-resolved-type expansion) — this Tool project cannot reference that
    ///     Core-internal type, exactly the same duplication rationale already documented on
    ///     <see cref="IsVisible"/> above. Unlike <c>ExposeScopeResolver</c>, <c>export --target</c>
    ///     only ever has a single target and no per-target bracket filter (the standalone
    ///     <c>--filter</c> option already covers narrowing), so the multi-target/bracket-filter
    ///     machinery is deliberately omitted here.
    /// </remarks>
    private static List<string>? ResolveTargetSubtreeSubjects(SysmlWorkspace workspace, string target, bool includeStdlib)
    {
        if (!workspace.Declarations.TryGetValue(target, out var declaration) ||
            !IsVisible(target, workspace, includeStdlib))
        {
            return null;
        }

        var subjects = new List<string> { target };

        // When the target resolves to a usage (e.g. 'part myVehicle : Vehicle;'), its own
        // containment subtree is typically empty — the real content lives under its type's
        // subtree. Add the resolved type's qualified name too, so a usage target still yields
        // useful output instead of a near-empty result.
        if (declaration is SysmlFeatureNode feature)
        {
            var typeTarget = feature.ResolvedEdges
                .FirstOrDefault(edge => edge.Kind == SysmlEdgeKind.Typing)
                ?.TargetQualifiedName;
            if (typeTarget is not null)
            {
                subjects.Add(typeTarget);
            }
        }

        return subjects;
    }

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="qualifiedName"/> is one of
    ///     <paramref name="subjects"/> or lies within one of their containment subtrees (a
    ///     <c>"{subject}::"</c> prefix match).
    /// </summary>
    /// <param name="qualifiedName">The qualified name to test.</param>
    /// <param name="subjects">The resolved <c>--target</c> subtree subjects.</param>
    /// <returns><see langword="true"/> when in scope; otherwise <see langword="false"/>.</returns>
    private static bool IsInTargetSubtree(string qualifiedName, IReadOnlyList<string> subjects) =>
        subjects.Any(subject =>
            qualifiedName == subject || qualifiedName.StartsWith(subject + "::", StringComparison.Ordinal));

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
        context.WriteLine(ExportStrings.Export_OptionTarget1);
        context.WriteLine(ExportStrings.Export_OptionTarget2);
        context.WriteLine(ExportStrings.Export_OptionFilter1);
        context.WriteLine(ExportStrings.Export_OptionFilter2);
    }
}
