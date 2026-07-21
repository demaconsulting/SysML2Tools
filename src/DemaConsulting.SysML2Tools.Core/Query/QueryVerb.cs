// <copyright file="QueryVerb.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Identifies one of the twelve model-analysis operations supported by the
///     <c>query</c> command.
/// </summary>
public enum QueryVerb
{
    /// <summary>Lists the elements a given element uses (its outgoing dependencies).</summary>
    Uses,

    /// <summary>Lists the elements that use a given element (its incoming dependencies).</summary>
    UsedBy,

    /// <summary>
    ///     Combines <see cref="Uses"/> and <see cref="UsedBy"/> for a given element into one
    ///     prose-rendered result: what it depends on, and what depends on it.
    /// </summary>
    Dependencies,

    /// <summary>Reports the transitive set of elements affected by a change to a given element.</summary>
    Impact,

    /// <summary>Describes a single element in detail (kind, properties, documentation).</summary>
    Describe,

    /// <summary>Reports the specialization/generalization hierarchy of a given element.</summary>
    Hierarchy,

    /// <summary>Lists the requirements satisfied, verified, or traced to a given element.</summary>
    Requirements,

    /// <summary>Describes the ports and interfaces exposed by a given element.</summary>
    Interface,

    /// <summary>Lists the connections (bindings, flows) attached to a given element.</summary>
    Connections,

    /// <summary>Lists the states and transitions of a given state-machine element.</summary>
    States,

    /// <summary>Lists elements in the workspace, optionally filtered by kind and/or name.</summary>
    List,

    /// <summary>Searches the workspace for elements matching a kind and/or name filter.</summary>
    Find
}

/// <summary>
///     Provides conversion between the kebab-case verb tokens accepted on the command line
///     and the <see cref="QueryVerb"/> enumeration.
/// </summary>
public static class QueryVerbParsing
{
    /// <summary>
    ///     Gets the ordered list of all recognized verb tokens, used to build error messages
    ///     and help text.
    /// </summary>
    public static IReadOnlyList<string> AllTokens { get; } =
    [
        "uses",
        "used-by",
        "dependencies",
        "impact",
        "describe",
        "hierarchy",
        "requirements",
        "interface",
        "connections",
        "states",
        "list",
        "find"
    ];

    /// <summary>
    ///     Parses a kebab-case verb token into a <see cref="QueryVerb"/> value.
    /// </summary>
    /// <param name="token">The verb token as supplied on the command line (e.g., <c>"used-by"</c>).</param>
    /// <returns>The matching <see cref="QueryVerb"/> value.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="token"/> does not match any recognized verb; the message
    ///     lists all valid tokens.
    /// </exception>
    public static QueryVerb Parse(string token)
    {
        // Match against the fixed, ROADMAP-defined verb vocabulary
        return token switch
        {
            "uses" => QueryVerb.Uses,
            "used-by" => QueryVerb.UsedBy,
            "dependencies" => QueryVerb.Dependencies,
            "impact" => QueryVerb.Impact,
            "describe" => QueryVerb.Describe,
            "hierarchy" => QueryVerb.Hierarchy,
            "requirements" => QueryVerb.Requirements,
            "interface" => QueryVerb.Interface,
            "connections" => QueryVerb.Connections,
            "states" => QueryVerb.States,
            "list" => QueryVerb.List,
            "find" => QueryVerb.Find,
            _ => throw new ArgumentException(
                $"Unsupported query verb '{token}'. Valid verbs are: {string.Join(", ", AllTokens)}.",
                nameof(token))
        };
    }

    /// <summary>
    ///     Converts a <see cref="QueryVerb"/> value back to its kebab-case command-line token.
    /// </summary>
    /// <param name="verb">The verb value to convert.</param>
    /// <returns>The kebab-case token (e.g., <c>"used-by"</c>) for <paramref name="verb"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="verb"/> is not a recognized value.</exception>
    public static string ToToken(QueryVerb verb)
    {
        return verb switch
        {
            QueryVerb.Uses => "uses",
            QueryVerb.UsedBy => "used-by",
            QueryVerb.Dependencies => "dependencies",
            QueryVerb.Impact => "impact",
            QueryVerb.Describe => "describe",
            QueryVerb.Hierarchy => "hierarchy",
            QueryVerb.Requirements => "requirements",
            QueryVerb.Interface => "interface",
            QueryVerb.Connections => "connections",
            QueryVerb.States => "states",
            QueryVerb.List => "list",
            QueryVerb.Find => "find",
            _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "Unrecognized query verb.")
        };
    }

    /// <summary>
    ///     Determines whether <paramref name="verb"/> requires the <c>--element</c> option to be
    ///     supplied.
    /// </summary>
    /// <param name="verb">The verb to check.</param>
    /// <returns><see langword="true"/> for every verb except <see cref="QueryVerb.List"/> and <see cref="QueryVerb.Find"/>.</returns>
    public static bool RequiresElement(QueryVerb verb)
    {
        // Only the workspace-wide enumeration verbs operate without a target element
        return verb is not (QueryVerb.List or QueryVerb.Find);
    }
}
