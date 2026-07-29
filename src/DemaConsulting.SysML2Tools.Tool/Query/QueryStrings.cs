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

using System.Resources;

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Hand-written, culture-aware accessor for the strings embedded in
///     <c>Query/QueryStrings.resx</c>. See <see cref="ProgramStrings"/> for the rationale
///     behind hand-writing this class instead of relying on the Visual Studio
///     "ResXFileCodeGenerator" custom tool.
/// </summary>
/// <remarks>
///     Adding a future locale requires zero code changes: place a
///     <c>Query/QueryStrings.{culture}.resx</c> file alongside this file with the same key
///     names.
/// </remarks>
internal static class QueryStrings
{
    private static readonly ResourceManager ResourceManager =
        new("DemaConsulting.SysML2Tools.Query.QueryStrings", typeof(QueryStrings).Assembly);

    /// <summary>Gets the general 'query' usage line.</summary>
    public static string Query_GeneralUsage => ResourceManager.GetString(nameof(Query_GeneralUsage))!;

    /// <summary>Gets the "Verbs:" header line.</summary>
    public static string Query_VerbsHeader => ResourceManager.GetString(nameof(Query_VerbsHeader))!;

    /// <summary>Gets the 'uses' verb summary line.</summary>
    public static string Query_VerbUses => ResourceManager.GetString(nameof(Query_VerbUses))!;

    /// <summary>Gets the 'used-by' verb summary line.</summary>
    public static string Query_VerbUsedBy => ResourceManager.GetString(nameof(Query_VerbUsedBy))!;

    /// <summary>Gets the 'dependencies' verb summary line.</summary>
    public static string Query_VerbDependencies => ResourceManager.GetString(nameof(Query_VerbDependencies))!;

    /// <summary>Gets the 'impact' verb summary line.</summary>
    public static string Query_VerbImpact => ResourceManager.GetString(nameof(Query_VerbImpact))!;

    /// <summary>Gets the 'describe' verb summary line.</summary>
    public static string Query_VerbDescribe => ResourceManager.GetString(nameof(Query_VerbDescribe))!;

    /// <summary>Gets the 'hierarchy' verb summary line.</summary>
    public static string Query_VerbHierarchy => ResourceManager.GetString(nameof(Query_VerbHierarchy))!;

    /// <summary>Gets the 'requirements' verb summary line.</summary>
    public static string Query_VerbRequirements => ResourceManager.GetString(nameof(Query_VerbRequirements))!;

    /// <summary>Gets the 'interface' verb summary line.</summary>
    public static string Query_VerbInterface => ResourceManager.GetString(nameof(Query_VerbInterface))!;

    /// <summary>Gets the 'connections' verb summary line.</summary>
    public static string Query_VerbConnections => ResourceManager.GetString(nameof(Query_VerbConnections))!;

    /// <summary>Gets the 'states' verb summary line.</summary>
    public static string Query_VerbStates => ResourceManager.GetString(nameof(Query_VerbStates))!;

    /// <summary>Gets the 'list' verb summary line.</summary>
    public static string Query_VerbList => ResourceManager.GetString(nameof(Query_VerbList))!;

    /// <summary>Gets the 'find' verb summary line.</summary>
    public static string Query_VerbFind => ResourceManager.GetString(nameof(Query_VerbFind))!;

    /// <summary>Gets the shared "Options:" header line.</summary>
    public static string Query_OptionsHeader => ResourceManager.GetString(nameof(Query_OptionsHeader))!;

    /// <summary>Gets the first line of the general --element option description.</summary>
    public static string Query_GeneralOptionElement1 => ResourceManager.GetString(nameof(Query_GeneralOptionElement1))!;

    /// <summary>Gets the second line of the general --element option description.</summary>
    public static string Query_GeneralOptionElement2 => ResourceManager.GetString(nameof(Query_GeneralOptionElement2))!;

    /// <summary>Gets the first line of the general --format option description.</summary>
    public static string Query_GeneralOptionFormat1 => ResourceManager.GetString(nameof(Query_GeneralOptionFormat1))!;

    /// <summary>Gets the second line of the general --format option description.</summary>
    public static string Query_GeneralOptionFormat2 => ResourceManager.GetString(nameof(Query_GeneralOptionFormat2))!;

    /// <summary>Gets the third line of the general --format option description.</summary>
    public static string Query_GeneralOptionFormat3 => ResourceManager.GetString(nameof(Query_GeneralOptionFormat3))!;

    /// <summary>Gets the first line of the general --walk-depth option description.</summary>
    public static string Query_GeneralOptionWalkDepth1 => ResourceManager.GetString(nameof(Query_GeneralOptionWalkDepth1))!;

    /// <summary>Gets the second line of the general --walk-depth option description.</summary>
    public static string Query_GeneralOptionWalkDepth2 => ResourceManager.GetString(nameof(Query_GeneralOptionWalkDepth2))!;

    /// <summary>Gets the first line of the general --depth option description.</summary>
    public static string Query_GeneralOptionDepth1 => ResourceManager.GetString(nameof(Query_GeneralOptionDepth1))!;

    /// <summary>Gets the second line of the general --depth option description.</summary>
    public static string Query_GeneralOptionDepth2 => ResourceManager.GetString(nameof(Query_GeneralOptionDepth2))!;

    /// <summary>Gets the first line of the general --heading option description.</summary>
    public static string Query_GeneralOptionHeading1 => ResourceManager.GetString(nameof(Query_GeneralOptionHeading1))!;

    /// <summary>Gets the second line of the general --heading option description.</summary>
    public static string Query_GeneralOptionHeading2 => ResourceManager.GetString(nameof(Query_GeneralOptionHeading2))!;

    /// <summary>Gets the general --direction option line.</summary>
    public static string Query_GeneralOptionDirection => ResourceManager.GetString(nameof(Query_GeneralOptionDirection))!;

    /// <summary>Gets the general --kind option line.</summary>
    public static string Query_GeneralOptionKind => ResourceManager.GetString(nameof(Query_GeneralOptionKind))!;

    /// <summary>Gets the general --name option line.</summary>
    public static string Query_GeneralOptionName => ResourceManager.GetString(nameof(Query_GeneralOptionName))!;

    /// <summary>Gets the general --include-stdlib option line.</summary>
    public static string Query_GeneralOptionIncludeStdlib => ResourceManager.GetString(nameof(Query_GeneralOptionIncludeStdlib))!;

    /// <summary>Gets the general --include-connections option line.</summary>
    public static string Query_GeneralOptionIncludeConnections => ResourceManager.GetString(nameof(Query_GeneralOptionIncludeConnections))!;

    /// <summary>Gets the first line of the general --output option description.</summary>
    public static string Query_GeneralOptionOutput1 => ResourceManager.GetString(nameof(Query_GeneralOptionOutput1))!;

    /// <summary>Gets the second line of the general --output option description.</summary>
    public static string Query_GeneralOptionOutput2 => ResourceManager.GetString(nameof(Query_GeneralOptionOutput2))!;

    /// <summary>Gets the first line of the "typical workflow" note.</summary>
    public static string Query_WorkflowNote1 => ResourceManager.GetString(nameof(Query_WorkflowNote1))!;

    /// <summary>Gets the second line of the "typical workflow" note.</summary>
    public static string Query_WorkflowNote2 => ResourceManager.GetString(nameof(Query_WorkflowNote2))!;

    /// <summary>Gets the third line of the "typical workflow" note.</summary>
    public static string Query_WorkflowNote3 => ResourceManager.GetString(nameof(Query_WorkflowNote3))!;

    /// <summary>Gets the fourth line of the "typical workflow" note.</summary>
    public static string Query_WorkflowNote4 => ResourceManager.GetString(nameof(Query_WorkflowNote4))!;

    /// <summary>Gets the fifth line of the "typical workflow" note (a --depth/--heading example).</summary>
    public static string Query_WorkflowNote5 => ResourceManager.GetString(nameof(Query_WorkflowNote5))!;

    /// <summary>Gets the verb-specific usage format string used when the verb requires --element.</summary>
    public static string Query_VerbUsageWithElement => ResourceManager.GetString(nameof(Query_VerbUsageWithElement))!;

    /// <summary>Gets the verb-specific usage format string used when the verb does not require --element.</summary>
    public static string Query_VerbUsageNoElement => ResourceManager.GetString(nameof(Query_VerbUsageNoElement))!;

    /// <summary>Gets the required --element option line shown in verb-specific help.</summary>
    public static string Query_OptionElementRequired => ResourceManager.GetString(nameof(Query_OptionElementRequired))!;

    /// <summary>Gets the --walk-depth option line shown for the 'impact' verb.</summary>
    public static string Query_OptionWalkDepthImpact => ResourceManager.GetString(nameof(Query_OptionWalkDepthImpact))!;

    /// <summary>Gets the first line of the --include-connections option shown for the 'impact' verb.</summary>
    public static string Query_OptionIncludeConnectionsImpact1 => ResourceManager.GetString(nameof(Query_OptionIncludeConnectionsImpact1))!;

    /// <summary>Gets the second line of the --include-connections option shown for the 'impact' verb.</summary>
    public static string Query_OptionIncludeConnectionsImpact2 => ResourceManager.GetString(nameof(Query_OptionIncludeConnectionsImpact2))!;

    /// <summary>Gets the third line of the --include-connections option shown for the 'impact' verb.</summary>
    public static string Query_OptionIncludeConnectionsImpact3 => ResourceManager.GetString(nameof(Query_OptionIncludeConnectionsImpact3))!;

    /// <summary>Gets the --direction option line shown for the 'hierarchy' verb.</summary>
    public static string Query_OptionDirectionHierarchy => ResourceManager.GetString(nameof(Query_OptionDirectionHierarchy))!;

    /// <summary>Gets the --kind option line shown for the 'list'/'find' verbs.</summary>
    public static string Query_OptionKindListFind => ResourceManager.GetString(nameof(Query_OptionKindListFind))!;

    /// <summary>Gets the --name option line shown for the 'list'/'find' verbs.</summary>
    public static string Query_OptionNameListFind => ResourceManager.GetString(nameof(Query_OptionNameListFind))!;

    /// <summary>Gets the --format option line shown in verb-specific help.</summary>
    public static string Query_OptionFormatVerb => ResourceManager.GetString(nameof(Query_OptionFormatVerb))!;

    /// <summary>Gets the --depth option line shown in verb-specific help.</summary>
    public static string Query_OptionDepthVerb => ResourceManager.GetString(nameof(Query_OptionDepthVerb))!;

    /// <summary>Gets the --heading option line shown in verb-specific help.</summary>
    public static string Query_OptionHeadingVerb => ResourceManager.GetString(nameof(Query_OptionHeadingVerb))!;

    /// <summary>Gets the --include-stdlib option line shown in verb-specific help.</summary>
    public static string Query_OptionIncludeStdlibVerb => ResourceManager.GetString(nameof(Query_OptionIncludeStdlibVerb))!;

    /// <summary>Gets the --output option line shown in verb-specific help.</summary>
    public static string Query_OptionOutputVerb => ResourceManager.GetString(nameof(Query_OptionOutputVerb))!;

    /// <summary>Gets the "Example:" header line shown in verb-specific help.</summary>
    public static string Query_ExampleHeader => ResourceManager.GetString(nameof(Query_ExampleHeader))!;

    /// <summary>Gets the example invocation line for the 'uses' verb.</summary>
    public static string Query_Example_Uses => ResourceManager.GetString(nameof(Query_Example_Uses))!;

    /// <summary>Gets the example invocation line for the 'used-by' verb.</summary>
    public static string Query_Example_UsedBy => ResourceManager.GetString(nameof(Query_Example_UsedBy))!;

    /// <summary>Gets the example invocation line for the 'dependencies' verb.</summary>
    public static string Query_Example_Dependencies => ResourceManager.GetString(nameof(Query_Example_Dependencies))!;

    /// <summary>Gets the example invocation line for the 'impact' verb.</summary>
    public static string Query_Example_Impact => ResourceManager.GetString(nameof(Query_Example_Impact))!;

    /// <summary>Gets the example invocation line for the 'describe' verb.</summary>
    public static string Query_Example_Describe => ResourceManager.GetString(nameof(Query_Example_Describe))!;

    /// <summary>Gets the example invocation line for the 'hierarchy' verb.</summary>
    public static string Query_Example_Hierarchy => ResourceManager.GetString(nameof(Query_Example_Hierarchy))!;

    /// <summary>Gets the example invocation line for the 'requirements' verb.</summary>
    public static string Query_Example_Requirements => ResourceManager.GetString(nameof(Query_Example_Requirements))!;

    /// <summary>Gets the example invocation line for the 'interface' verb.</summary>
    public static string Query_Example_Interface => ResourceManager.GetString(nameof(Query_Example_Interface))!;

    /// <summary>Gets the example invocation line for the 'connections' verb.</summary>
    public static string Query_Example_Connections => ResourceManager.GetString(nameof(Query_Example_Connections))!;

    /// <summary>Gets the example invocation line for the 'states' verb.</summary>
    public static string Query_Example_States => ResourceManager.GetString(nameof(Query_Example_States))!;

    /// <summary>Gets the example invocation line for the 'list' verb.</summary>
    public static string Query_Example_List => ResourceManager.GetString(nameof(Query_Example_List))!;

    /// <summary>Gets the example invocation line for the 'find' verb.</summary>
    public static string Query_Example_Find => ResourceManager.GetString(nameof(Query_Example_Find))!;

    /// <summary>Gets the Markdown output-shape schema hint, shared by every verb.</summary>
    public static string Query_SchemaHint_Markdown => ResourceManager.GetString(nameof(Query_SchemaHint_Markdown))!;

    /// <summary>Gets the JSON output-shape schema hint, shared by every verb.</summary>
    public static string Query_SchemaHint_Json => ResourceManager.GetString(nameof(Query_SchemaHint_Json))!;

    /// <summary>Gets the Markdown output-shape schema hint specific to the 'dependencies' verb's bullet-prose rendering.</summary>
    public static string Query_SchemaHint_Markdown_Dependencies => ResourceManager.GetString(nameof(Query_SchemaHint_Markdown_Dependencies))!;

    /// <summary>Gets the JSON output-shape schema hint specific to the 'dependencies' verb (includes the Direction field).</summary>
    public static string Query_SchemaHint_Json_Dependencies => ResourceManager.GetString(nameof(Query_SchemaHint_Json_Dependencies))!;

    /// <summary>Gets the --walk-depth no-op note shown for the 'dependencies' verb.</summary>
    public static string Query_OptionWalkDepthIgnoredDependencies => ResourceManager.GetString(nameof(Query_OptionWalkDepthIgnoredDependencies))!;

    /// <summary>
    ///     Gets the resx-sourced example invocation line for the given verb via the matching
    ///     per-verb property above, so <see cref="Query.QueryCommand.PrintVerbHelp"/> can add
    ///     example support with a single call site instead of a 12-arm switch of its own.
    /// </summary>
    /// <param name="verb">The verb to get the example invocation for.</param>
    /// <returns>The example invocation line for <paramref name="verb"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="verb"/> is not a recognized value.</exception>
    public static string GetExample(QueryVerb verb)
    {
        return verb switch
        {
            QueryVerb.Uses => Query_Example_Uses,
            QueryVerb.UsedBy => Query_Example_UsedBy,
            QueryVerb.Dependencies => Query_Example_Dependencies,
            QueryVerb.Impact => Query_Example_Impact,
            QueryVerb.Describe => Query_Example_Describe,
            QueryVerb.Hierarchy => Query_Example_Hierarchy,
            QueryVerb.Requirements => Query_Example_Requirements,
            QueryVerb.Interface => Query_Example_Interface,
            QueryVerb.Connections => Query_Example_Connections,
            QueryVerb.States => Query_Example_States,
            QueryVerb.List => Query_Example_List,
            QueryVerb.Find => Query_Example_Find,
            _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "Unrecognized query verb.")
        };
    }
}
