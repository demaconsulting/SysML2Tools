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

using System.Globalization;
using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.Io;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Implements the <c>query</c> command: loads a SysML v2 workspace and dispatches to one of
///     twelve model-analysis verbs implemented by Core's public <see cref="QueryEngine"/>,
///     rendering the result via <see cref="QueryResultRenderer"/> as Markdown (default) or JSON —
///     to stdout, or to a file when <c>--output</c> is supplied (via
///     <see cref="QueryResultExporter"/>). A thin CLI adapter: glob resolution, workspace loading,
///     element lookup, and console/log I/O are the only logic that remains in this class; the
///     verb-semantics logic itself lives in Core's public, reusable Query API.
/// </summary>
internal static class QueryCommand
{
    /// <summary>
    ///     Runs the query command.
    /// </summary>
    /// <param name="context">
    ///     The CLI context, supplying the parsed <see cref="QueryOptions"/>,
    ///     <see cref="Context.QueryFiles"/>, <see cref="Context.QueryOutput"/>, and output methods.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="Context.Query"/> is <see langword="null"/> (no verb was parsed), when the verb
    ///     requires <c>--element</c> and none was supplied, when <c>find</c> is invoked without <c>--kind</c> or
    ///     <c>--name</c>, or when <c>--format</c> is not <c>markdown</c>/<c>json</c>.
    /// </exception>
    public static async Task RunAsync(Context context)
    {
        // Defensive: Program only reaches here when Command == Query, which Context.Create only
        // sets after successfully parsing a verb, so Query should never be null in practice.
        var options = context.Query
                      ?? throw new ArgumentException("query: no verb was specified.", nameof(context));
        var verbToken = QueryVerbParsing.ToToken(options.Verb);

        // Verbs other than 'list'/'find' operate on a single target element
        if (QueryVerbParsing.RequiresElement(options.Verb) && string.IsNullOrWhiteSpace(options.Element))
        {
            throw new ArgumentException(
                $"query {verbToken}: --element (-e) is required for this verb.",
                nameof(context));
        }

        // 'find' additionally requires at least one of --kind/--name; validated up front so an
        // obviously-invalid invocation fails fast without loading any files.
        if (options.Verb == QueryVerb.Find &&
            string.IsNullOrWhiteSpace(options.Kind) && string.IsNullOrWhiteSpace(options.NameFilter))
        {
            throw new ArgumentException(
                "query find: at least one of --kind or --name is required.",
                nameof(context));
        }

        // Reject unsupported --format values up front, before doing any work
        var format = options.Format ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"query {verbToken}: unsupported --format value '{format}'. Valid values are: markdown, json.",
                nameof(context));
        }

        if (context.QueryFiles.Count == 0)
        {
            context.WriteError($"query {verbToken}: no input files specified. Provide file glob patterns.");
            return;
        }

        // Resolve the supplied file glob patterns to concrete file paths via the shared
        // GlobFileCollector, supporting recursive '**' patterns and '!' exclusions.
        context.WriteLine($"Loading {context.QueryFiles.Count} file pattern(s)...");
        var files = GlobFileCollector.Collect(context.QueryFiles, [".sysml", ".kerml"], Directory.GetCurrentDirectory());
        if (files.Count == 0)
        {
            context.WriteError($"query {verbToken}: no files matched the given pattern(s).");
            return;
        }

        context.WriteLine($"Resolved {files.Count} file(s) from {context.QueryFiles.Count} pattern(s).");

        // Load the workspace from the resolved file paths, exactly as 'lint'/'render' do
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var loadResult = await WorkspaceLoader.LoadAsync(files, stdlibTable).ConfigureAwait(false);

        foreach (var diagnostic in loadResult.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                context.WriteError($"  {diagnostic}");
            }
            else
            {
                context.WriteLine($"  {diagnostic}");
            }
        }

        if (loadResult.Workspace is null)
        {
            context.WriteError($"query {verbToken}: workspace loading failed; no query performed.");
            return;
        }

        var workspace = loadResult.Workspace;

        // Look up the target element for verbs that require one
        SysmlNode? element = null;
        if (QueryVerbParsing.RequiresElement(options.Verb) &&
            !workspace.Declarations.TryGetValue(options.Element!, out element))
        {
            context.WriteError(
                $"query {verbToken}: element '{options.Element}' not found in the workspace.");
            return;
        }

        // Dispatch to Core's public QueryEngine.Execute, which contains the per-verb switch
        // (previously inlined here) now shared by any caller of the public Query API.
        var result = QueryEngine.Execute(workspace, options, element);

        // Render via the shared renderer; markdown lines are written one per WriteLine call,
        // JSON is written as a single chunk (mirroring how other commands emit multi-line output)
        // -- unless '--output' was supplied, in which case the rendered output is written to that
        // file instead (mirroring 'export'/'render's own '--output' file-writing behavior).
        if (context.QueryOutput is not null)
        {
            try
            {
                // Ensure the parent directory exists (mirroring 'export'/'render's
                // Directory.CreateDirectory guard for their own --output paths), so a nonexistent
                // output path fails cleanly rather than throwing DirectoryNotFoundException.
                var outputDir = Path.GetDirectoryName(Path.GetFullPath(context.QueryOutput));
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    await QueryResultExporter.WriteJsonAsync(result, context.QueryOutput).ConfigureAwait(false);
                }
                else
                {
                    await QueryResultExporter.WriteMarkdownAsync(
                        result, context.QueryOutput, context.HeadingDepth, options.Heading).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                             or ArgumentException or PathTooLongException)
            {
                // Covers cases such as '--output' pointing at an existing directory, a
                // read-only/locked target, an otherwise-invalid path, or a malformed/too-long
                // path string, surfacing a clean error instead of an unhandled exception with a
                // stack trace.
                context.WriteError($"query {verbToken}: failed to write output file '{context.QueryOutput}': {ex.Message}");
                return;
            }

            context.WriteLine($"query {verbToken}: wrote output to '{context.QueryOutput}'.");
        }
        else if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            context.WriteLine(QueryResultRenderer.RenderJson(result));
        }
        else
        {
            foreach (var line in QueryResultRenderer.RenderMarkdown(
                         result, context.HeadingDepth, options.Heading))
            {
                context.WriteLine(line);
            }
        }
    }

    /// <summary>
    ///     Prints general help for the <c>query</c> command when no verb was supplied.
    /// </summary>
    /// <param name="context">The CLI context for output.</param>
    public static void PrintGeneralHelp(Context context)
    {
        context.WriteLine(QueryStrings.Query_GeneralUsage);
        context.WriteLine("");
        context.WriteLine(QueryStrings.Query_VerbsHeader);
        context.WriteLine(QueryStrings.Query_VerbUses);
        context.WriteLine(QueryStrings.Query_VerbUsedBy);
        context.WriteLine(QueryStrings.Query_VerbDependencies);
        context.WriteLine(QueryStrings.Query_VerbImpact);
        context.WriteLine(QueryStrings.Query_VerbDescribe);
        context.WriteLine(QueryStrings.Query_VerbHierarchy);
        context.WriteLine(QueryStrings.Query_VerbRequirements);
        context.WriteLine(QueryStrings.Query_VerbInterface);
        context.WriteLine(QueryStrings.Query_VerbConnections);
        context.WriteLine(QueryStrings.Query_VerbStates);
        context.WriteLine(QueryStrings.Query_VerbList);
        context.WriteLine(QueryStrings.Query_VerbFind);
        context.WriteLine("");
        context.WriteLine(QueryStrings.Query_OptionsHeader);
        context.WriteLine(QueryStrings.Query_GeneralOptionElement1);
        context.WriteLine(QueryStrings.Query_GeneralOptionElement2);
        context.WriteLine(QueryStrings.Query_GeneralOptionFormat1);
        context.WriteLine(QueryStrings.Query_GeneralOptionFormat2);
        context.WriteLine(QueryStrings.Query_GeneralOptionFormat3);
        context.WriteLine(QueryStrings.Query_GeneralOptionWalkDepth1);
        context.WriteLine(QueryStrings.Query_GeneralOptionWalkDepth2);
        context.WriteLine(QueryStrings.Query_GeneralOptionDepth1);
        context.WriteLine(QueryStrings.Query_GeneralOptionDepth2);
        context.WriteLine(QueryStrings.Query_GeneralOptionHeading1);
        context.WriteLine(QueryStrings.Query_GeneralOptionHeading2);
        context.WriteLine(QueryStrings.Query_GeneralOptionDirection);
        context.WriteLine(QueryStrings.Query_GeneralOptionKind);
        context.WriteLine(QueryStrings.Query_GeneralOptionName);
        context.WriteLine(QueryStrings.Query_GeneralOptionIncludeStdlib);
        context.WriteLine(QueryStrings.Query_GeneralOptionOutput1);
        context.WriteLine(QueryStrings.Query_GeneralOptionOutput2);
        context.WriteLine("");
        context.WriteLine(QueryStrings.Query_WorkflowNote1);
        context.WriteLine(QueryStrings.Query_WorkflowNote2);
        context.WriteLine(QueryStrings.Query_WorkflowNote3);
        context.WriteLine(QueryStrings.Query_WorkflowNote4);
        context.WriteLine(QueryStrings.Query_WorkflowNote5);
    }

    /// <summary>
    ///     Prints verb-specific help for the <c>query</c> command.
    /// </summary>
    /// <param name="context">The CLI context for output.</param>
    /// <param name="verb">The verb to print help for.</param>
    public static void PrintVerbHelp(Context context, QueryVerb verb)
    {
        var verbToken = QueryVerbParsing.ToToken(verb);
        var requiresElement = QueryVerbParsing.RequiresElement(verb);

        context.WriteLine(requiresElement
            ? string.Format(CultureInfo.InvariantCulture, QueryStrings.Query_VerbUsageWithElement, verbToken)
            : string.Format(CultureInfo.InvariantCulture, QueryStrings.Query_VerbUsageNoElement, verbToken));
        context.WriteLine("");
        context.WriteLine(QueryStrings.Query_OptionsHeader);

        if (requiresElement)
        {
            context.WriteLine(QueryStrings.Query_OptionElementRequired);
        }

        switch (verb)
        {
            case QueryVerb.Impact:
                context.WriteLine(QueryStrings.Query_OptionWalkDepthImpact);
                break;

            case QueryVerb.Dependencies:
                context.WriteLine(QueryStrings.Query_OptionWalkDepthIgnoredDependencies);
                break;

            case QueryVerb.Hierarchy:
                context.WriteLine(QueryStrings.Query_OptionDirectionHierarchy);
                break;

            case QueryVerb.List:
            case QueryVerb.Find:
                context.WriteLine(QueryStrings.Query_OptionKindListFind);
                context.WriteLine(QueryStrings.Query_OptionNameListFind);
                break;
        }

        context.WriteLine(QueryStrings.Query_OptionFormatVerb);
        context.WriteLine(QueryStrings.Query_OptionDepthVerb);
        context.WriteLine(QueryStrings.Query_OptionHeadingVerb);
        context.WriteLine(QueryStrings.Query_OptionIncludeStdlibVerb);
        context.WriteLine(QueryStrings.Query_OptionOutputVerb);
        context.WriteLine("");
        context.WriteLine(QueryStrings.Query_ExampleHeader);
        context.WriteLine(QueryStrings.GetExample(verb));
        context.WriteLine("");
        if (verb == QueryVerb.Dependencies)
        {
            context.WriteLine(QueryStrings.Query_SchemaHint_Markdown_Dependencies);
            context.WriteLine(QueryStrings.Query_SchemaHint_Json_Dependencies);
        }
        else
        {
            context.WriteLine(QueryStrings.Query_SchemaHint_Markdown);
            context.WriteLine(QueryStrings.Query_SchemaHint_Json);
        }
    }
}
