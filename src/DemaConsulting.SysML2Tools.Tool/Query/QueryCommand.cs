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

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Implements the <c>query</c> command: dispatches to one of eleven model-analysis
///     verbs. Every verb currently reports a "not yet implemented" diagnostic; real verb
///     logic is added incrementally in future releases, one <c>switch</c> arm at a time.
/// </summary>
internal static class QueryCommand
{
    /// <summary>
    ///     Runs the query command.
    /// </summary>
    /// <param name="context">The CLI context, supplying the parsed <see cref="QueryOptions"/> and output methods.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="Context.Query"/> is <see langword="null"/> (no verb was parsed), or when the verb
    ///     requires <c>--element</c> and none was supplied.
    /// </exception>
    public static Task RunAsync(Context context)
    {
        // Defensive: Program only reaches here when Command == Query, which Context.Create only
        // sets after successfully parsing a verb, so Query should never be null in practice.
        var options = context.Query
                      ?? throw new ArgumentException("query: no verb was specified.", nameof(context));

        // Verbs other than 'list'/'find' operate on a single target element
        if (QueryVerbParsing.RequiresElement(options.Verb) && string.IsNullOrWhiteSpace(options.Element))
        {
            var verbToken = QueryVerbParsing.ToToken(options.Verb);
            throw new ArgumentException(
                $"query {verbToken}: --element (-e) is required for this verb.",
                nameof(context));
        }

        // Each verb gets its own switch arm (rather than a lookup/loop) so a future release can
        // replace one verb's stub with real logic without touching the others.
        switch (options.Verb)
        {
            case QueryVerb.Uses:
                return NotImplementedAsync(context, QueryVerb.Uses);

            case QueryVerb.UsedBy:
                return NotImplementedAsync(context, QueryVerb.UsedBy);

            case QueryVerb.Impact:
                return NotImplementedAsync(context, QueryVerb.Impact);

            case QueryVerb.Describe:
                return NotImplementedAsync(context, QueryVerb.Describe);

            case QueryVerb.Hierarchy:
                return NotImplementedAsync(context, QueryVerb.Hierarchy);

            case QueryVerb.Requirements:
                return NotImplementedAsync(context, QueryVerb.Requirements);

            case QueryVerb.Interface:
                return NotImplementedAsync(context, QueryVerb.Interface);

            case QueryVerb.Connections:
                return NotImplementedAsync(context, QueryVerb.Connections);

            case QueryVerb.States:
                return NotImplementedAsync(context, QueryVerb.States);

            case QueryVerb.List:
                return NotImplementedAsync(context, QueryVerb.List);

            case QueryVerb.Find:
                return NotImplementedAsync(context, QueryVerb.Find);

            default:
                throw new ArgumentOutOfRangeException(nameof(context), options.Verb, "Unrecognized query verb.");
        }
    }

    /// <summary>
    ///     Reports that a verb's real implementation has not shipped yet.
    /// </summary>
    /// <param name="context">The CLI context used to report the diagnostic.</param>
    /// <param name="verb">The verb that was requested.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    ///     Uses <see cref="Context.WriteError"/> (setting <see cref="Context.ExitCode"/> to 1)
    ///     rather than throwing, matching the existing <c>lint</c>/<c>render</c> convention for
    ///     reporting "not ready" conditions in a way that <see cref="Program.Main"/> handles
    ///     cleanly without an unhandled-exception crash.
    /// </remarks>
    private static Task NotImplementedAsync(Context context, QueryVerb verb)
    {
        var verbToken = QueryVerbParsing.ToToken(verb);
        context.WriteError($"query {verbToken}: not yet implemented. This verb will be implemented in a future release.");
        return Task.CompletedTask;
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
        context.WriteLine("");
        context.WriteLine($"Note: '{verbToken}' is not yet implemented; running it reports a diagnostic and exits with code 1.");
    }
}
