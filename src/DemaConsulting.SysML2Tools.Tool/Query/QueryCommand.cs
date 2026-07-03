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
using DemaConsulting.SysML2Tools.Semantic.Internal;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Implements the <c>query</c> command: loads a SysML v2 workspace and dispatches to one of
///     eleven model-analysis verbs implemented by <see cref="QueryEngine"/>, rendering the result
///     via <see cref="QueryResultRenderer"/> as Markdown (default) or JSON.
/// </summary>
internal static class QueryCommand
{
    /// <summary>
    ///     Runs the query command.
    /// </summary>
    /// <param name="context">The CLI context, supplying the parsed <see cref="QueryOptions"/> and output methods.</param>
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

        if (options.Files.Count == 0)
        {
            context.WriteError($"query {verbToken}: no input files specified. Provide file glob patterns.");
            return;
        }

        // Load the workspace from the supplied file patterns, exactly as 'lint'/'render' do
        context.WriteLine($"Loading {options.Files.Count} file pattern(s)...");
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var loadResult = await WorkspaceLoader.LoadAsync(options.Files, stdlibTable).ConfigureAwait(false);

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

        // Each verb gets its own switch arm (rather than a lookup/loop) so a future release can
        // change one verb's logic without touching the others.
        var result = options.Verb switch
        {
            QueryVerb.Uses => QueryEngine.Uses(workspace, element!, options),
            QueryVerb.UsedBy => QueryEngine.UsedBy(workspace, element!, options),
            QueryVerb.Impact => QueryEngine.Impact(workspace, element!, options),
            QueryVerb.Describe => QueryEngine.Describe(workspace, element!, options),
            QueryVerb.Hierarchy => QueryEngine.Hierarchy(workspace, element!, options),
            QueryVerb.Requirements => QueryEngine.Requirements(workspace, element!, options),
            QueryVerb.Interface => QueryEngine.Interface(workspace, element!, options),
            QueryVerb.Connections => QueryEngine.Connections(workspace, element!, options),
            QueryVerb.States => QueryEngine.States(workspace, element!, options),
            QueryVerb.List => QueryEngine.List(workspace, options),
            QueryVerb.Find => QueryEngine.Find(workspace, options),
            _ => throw new ArgumentOutOfRangeException(nameof(context), options.Verb, "Unrecognized query verb.")
        };

        // Render via the shared renderer; markdown lines are written one per WriteLine call,
        // JSON is written as a single chunk (mirroring how other commands emit multi-line output)
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            context.WriteLine(QueryResultRenderer.RenderJson(result));
        }
        else
        {
            foreach (var line in QueryResultRenderer.RenderMarkdown(result))
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
        context.WriteLine("Usage: sysml2tools query <verb> [options] <files...>");
        context.WriteLine("");
        context.WriteLine("Verbs:");
        context.WriteLine("  uses           List the elements a given element uses");
        context.WriteLine("  used-by        List the elements that use a given element");
        context.WriteLine("  impact         Report the transitive impact of a change to a given element");
        context.WriteLine("  describe       Describe a single element in detail");
        context.WriteLine("  hierarchy      Report the specialization/generalization hierarchy of a given element");
        context.WriteLine("  requirements   List requirements satisfied, verified, or traced to a given element");
        context.WriteLine("  interface      Describe the ports and interfaces of a given element");
        context.WriteLine("  connections    List the connections attached to a given element");
        context.WriteLine("  states         List the states and transitions of a given state-machine element");
        context.WriteLine("  list           List elements in the workspace, optionally filtered");
        context.WriteLine("  find           Search the workspace for elements matching a filter");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  --element <name>, -e <name>  Qualified name of the target element (required for");
        context.WriteLine("                                every verb except 'list' and 'find')");
        context.WriteLine("  --format markdown|json       Output format (default: markdown); note this is a");
        context.WriteLine("                                different set of values than the 'render' command's");
        context.WriteLine("                                --format (svg/png)");
        context.WriteLine("  --depth <#>                  Maximum impact-walk depth ('impact' verb only); note");
        context.WriteLine("                                this is the same flag as the 'render' command's");
        context.WriteLine("                                diagram nesting --depth");
        context.WriteLine("  --direction up|down|both     Traversal direction ('hierarchy' verb only)");
        context.WriteLine("  --kind <kind>                Element-kind filter ('list'/'find' verbs only)");
        context.WriteLine("  --name <substring>           Name substring filter ('list'/'find' verbs only)");
        context.WriteLine("  --include-stdlib             Include OMG standard library elements in results");
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
            ? $"Usage: sysml2tools query {verbToken} --element <name> [options] <files...>"
            : $"Usage: sysml2tools query {verbToken} [options] <files...>");
        context.WriteLine("");
        context.WriteLine("Options:");

        if (requiresElement)
        {
            context.WriteLine("  --element <name>, -e <name>  Qualified name of the target element (required)");
        }

        switch (verb)
        {
            case QueryVerb.Impact:
                context.WriteLine("  --depth <#>                   Maximum impact-walk depth (default: unlimited)");
                break;

            case QueryVerb.Hierarchy:
                context.WriteLine("  --direction up|down|both      Traversal direction (default: both)");
                break;

            case QueryVerb.List:
            case QueryVerb.Find:
                context.WriteLine("  --kind <kind>                 Element-kind filter");
                context.WriteLine("  --name <substring>            Name substring filter");
                break;
        }

        context.WriteLine("  --format markdown|json        Output format (default: markdown)");
        context.WriteLine("  --include-stdlib              Include OMG standard library elements in results");
    }
}
