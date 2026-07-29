// <copyright file="QueryArgumentParser.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Parses a token list (e.g., words following a <c>query</c> command token) into a
///     <see cref="QueryOptions"/> instance plus any trailing positional (non-flag) tokens.
/// </summary>
/// <remarks>
///     The grammar is structural: the first token must be a recognized verb (see
///     <see cref="QueryVerbParsing"/>) — this is validated eagerly here, not lazily inferred from
///     a shared default case. When no verb is present and the caller's help flag was requested
///     (see the <c>helpRequested</c> parameter of <see cref="Parse"/>), parsing returns a <see langword="null"/>
///     <see cref="QueryOptions"/> (the caller is expected to show general help instead); when no
///     verb is present and help was not requested, a clear <see cref="ArgumentException"/> is
///     thrown rather than leaving the caller in a silent null/None state. Remaining tokens
///     recognize <c>--element</c>/<c>-e</c>, <c>--direction</c>, <c>--kind</c>, <c>--name</c>,
///     <c>--include-stdlib</c>, <c>--include-connections</c>, <c>--format</c>,
///     <c>--walk-depth</c>, and <c>--heading</c>, plus
///     positional file glob patterns returned separately (this type has no file-glob or CLI-I/O
///     concept of its own — <see cref="QueryOptions"/> does not carry an input-files property);
///     any other <c>-</c>-prefixed token is rejected, including <c>--output</c>, which is a
///     Tool-only, CLI-I/O concept this type is intentionally unaware of (see the Tool project's
///     <c>Query.QueryCliArgumentParser</c>, which pre-scans for <c>--output</c> before delegating
///     the remaining tokens here). <c>--format</c>'s value is captured raw and is validated later
///     by the caller. <c>--walk-depth</c> (impact-walk depth, unbounded) is distinct from any
///     caller's own global Markdown heading-depth option (not parsed by this class). <c>--heading</c>
///     (custom Markdown heading text) is also recognized here; both a heading-depth option and
///     <c>--heading</c> are Markdown-output-only and have no effect on JSON output.
/// </remarks>
public static class QueryArgumentParser
{
    /// <summary>
    ///     Parses a token list into a <see cref="QueryOptions"/> instance plus any trailing
    ///     positional (non-flag) tokens.
    /// </summary>
    /// <param name="commandArgs">
    ///     The tokens to parse, starting with the verb token (e.g., the arguments remaining after
    ///     a caller has stripped its own command token and any cross-cutting flags).
    /// </param>
    /// <param name="helpRequested">
    ///     <see langword="true"/> when the caller's own help flag was supplied; suppresses the
    ///     "verb is required" error when no verb token is present.
    /// </param>
    /// <returns>
    ///     The parsed <see cref="QueryOptions"/> and any trailing positional tokens (e.g., file
    ///     glob patterns, interpreted by the caller); the options are <see langword="null"/> when
    ///     no verb token was supplied and <paramref name="helpRequested"/> is
    ///     <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when no verb token is present and <paramref name="helpRequested"/> is
    ///     <see langword="false"/>; when the first token is not a recognized verb; or when an
    ///     unrecognized flag is supplied.
    /// </exception>
    public static (QueryOptions? Options, IReadOnlyList<string> Files) Parse(
        IReadOnlyList<string> commandArgs, bool helpRequested)
    {
        // The verb is a required structural first argument, validated strictly here rather than
        // lazily inferred by a shared default case.
        if (commandArgs.Count == 0)
        {
            if (helpRequested)
            {
                return (null, []);
            }

            throw new ArgumentException(
                $"query: a verb is required. Valid verbs are: {string.Join(", ", QueryVerbParsing.AllTokens)}.");
        }

        var index = 0;
        var verbToken = commandArgs[index++];
        if (verbToken.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"query: expected a verb as the first argument, but found '{verbToken}'. " +
                $"Valid verbs are: {string.Join(", ", QueryVerbParsing.AllTokens)}.");
        }

        var verb = QueryVerbParsing.Parse(verbToken);

        string? element = null;
        string? direction = null;
        string? kind = null;
        string? nameFilter = null;
        string? format = null;
        int? walkDepth = null;
        string? heading = null;
        var includeStdlib = false;
        var includeConnections = false;
        var files = new List<string>();

        while (index < commandArgs.Count)
        {
            var arg = commandArgs[index++];
            switch (arg)
            {
                case "--element":
                case "-e":
                    element = GetRequiredStringArgument(
                        arg, commandArgs, ref index, "an element qualified-name argument");
                    break;

                case "--direction":
                    direction = GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a direction argument (up, down, or both)");
                    break;

                case "--kind":
                    kind = GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a kind filter argument");
                    break;

                case "--name":
                    nameFilter = GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a name filter argument");
                    break;

                case "--format":
                    format = GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a format argument (markdown or json)");
                    break;

                case "--walk-depth":
                    walkDepth = GetRequiredIntArgument(
                        arg, commandArgs, ref index, "an impact-walk depth argument", 1);
                    break;

                case "--heading":
                    heading = GetRequiredStringArgument(
                        arg, commandArgs, ref index, "a heading text argument");
                    break;

                case "--include-stdlib":
                    includeStdlib = true;
                    break;

                case "--include-connections":
                    includeConnections = true;
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"Unsupported argument '{arg}' for the 'query' command.", nameof(commandArgs));
                    }

                    files.Add(arg);
                    break;
            }
        }

        var options = new QueryOptions
        {
            Verb = verb,
            Element = element,
            Format = format,
            WalkDepth = walkDepth,
            Direction = direction,
            Kind = kind,
            NameFilter = nameFilter,
            IncludeStdlib = includeStdlib,
            IncludeConnections = includeConnections,
            Heading = heading
        };

        return (options, files);
    }

    /// <summary>
    ///     Gets a required string argument value, advancing <paramref name="index"/> past it.
    /// </summary>
    /// <param name="arg">The flag token that requires the value (used in error messages).</param>
    /// <param name="args">All arguments in the current parsing scope.</param>
    /// <param name="index">
    ///     The index of the value to read; advanced by one on success.
    /// </param>
    /// <param name="description">Description of what's required, used in error messages.</param>
    /// <returns>The argument value.</returns>
    /// <exception cref="ArgumentException">Thrown when no value is available at <paramref name="index"/>.</exception>
    /// <remarks>
    ///     A minimal, private duplicate of the Tool project's own
    ///     <c>Cli.CliArgumentHelpers.GetRequiredStringArgument</c>, kept local to this one file
    ///     rather than sharing a common helper type across the assembly boundary — this project
    ///     cannot reference the Tool project's <c>Cli</c> namespace, and moving that shared
    ///     helper into this project would touch three unrelated Tool-only parsers
    ///     (<c>RenderArgumentParser</c>, <c>ExportArgumentParser</c>, <c>GlobalArgumentParser</c>)
    ///     purely for this one caller's benefit.
    /// </remarks>
    private static string GetRequiredStringArgument(
        string arg,
        IReadOnlyList<string> args,
        ref int index,
        string description)
    {
        if (index >= args.Count)
        {
            throw new ArgumentException($"{arg} requires {description}", nameof(args));
        }

        return args[index++];
    }

    /// <summary>
    ///     Gets a required integer argument value in the range [<paramref name="min"/>, <paramref name="max"/>],
    ///     advancing <paramref name="index"/> past it.
    /// </summary>
    /// <param name="arg">The flag token that requires the value (used in error messages).</param>
    /// <param name="args">All arguments in the current parsing scope.</param>
    /// <param name="index">
    ///     The index of the value to read; advanced by one on success.
    /// </param>
    /// <param name="description">Description of what's required, used in error messages.</param>
    /// <param name="min">Minimum valid value (inclusive).</param>
    /// <param name="max">Maximum valid value (inclusive).</param>
    /// <returns>The argument value as an integer in [<paramref name="min"/>, <paramref name="max"/>].</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when no value is available at <paramref name="index"/>, or the value is not an
    ///     integer within range.
    /// </exception>
    /// <remarks>
    ///     A minimal, private duplicate of the Tool project's own
    ///     <c>Cli.CliArgumentHelpers.GetRequiredIntArgument</c>; see
    ///     <see cref="GetRequiredStringArgument"/>'s remarks for the rationale.
    /// </remarks>
    private static int GetRequiredIntArgument(
        string arg,
        IReadOnlyList<string> args,
        ref int index,
        string description,
        int min = 1,
        int max = int.MaxValue)
    {
        var value = GetRequiredStringArgument(arg, args, ref index, description);
        if (!int.TryParse(value, out var result) || result < min || result > max)
        {
            throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
        }

        return result;
    }
}
