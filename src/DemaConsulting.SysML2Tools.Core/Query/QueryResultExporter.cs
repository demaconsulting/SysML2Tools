// <copyright file="QueryResultExporter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.SysML2Tools.Query;

/// <summary>
///     Writes a rendered <see cref="QueryResult"/> directly to a file, as either Markdown or
///     JSON, via <see cref="QueryResultRenderer"/>.
/// </summary>
/// <remarks>
///     <para>
///     A thin convenience wrapper around <see cref="QueryResultRenderer.RenderMarkdown"/>/
///     <see cref="QueryResultRenderer.RenderJson"/> plus a plain <c>File.WriteAllText(Async)</c>
///     call. Deliberately minimal: no parent-directory creation, no "clean CLI error" catching of
///     filesystem exceptions — this project has no CLI-I/O convention to justify either behavior,
///     so both are left as caller (e.g., the Tool project's <c>query</c> CLI command)
///     responsibilities, exactly mirroring how the Tool project's own <c>export --output</c>/
///     <c>render --output</c> handling creates the parent directory and catches
///     <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/> itself before calling
///     into a library method like this one.
///     </para>
///     <para>
///     Markdown output is written by joining <see cref="QueryResultRenderer.RenderMarkdown"/>'s
///     lines with <c>"\n"</c> (no trailing line terminator), matching this codebase's existing,
///     platform-neutral file-output convention (no path in this codebase normalizes line endings
///     to <c>"\r\n"</c>).
///     </para>
/// </remarks>
public static class QueryResultExporter
{
    /// <summary>
    ///     Renders <paramref name="result"/> as Markdown (via
    ///     <see cref="QueryResultRenderer.RenderMarkdown"/>) and writes it to
    ///     <paramref name="path"/>, overwriting any existing file.
    /// </summary>
    /// <param name="result">The result to render and write.</param>
    /// <param name="path">The file path to write to.</param>
    /// <param name="depth">The Markdown heading depth; see <see cref="QueryResultRenderer.RenderMarkdown"/>.</param>
    /// <param name="heading">The custom Markdown heading text; see <see cref="QueryResultRenderer.RenderMarkdown"/>.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written; propagates uncaught.</exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the caller lacks permission to write to <paramref name="path"/>; propagates
    ///     uncaught.
    /// </exception>
    public static void WriteMarkdown(QueryResult result, string path, int depth = 1, string? heading = null)
    {
        var lines = QueryResultRenderer.RenderMarkdown(result, depth, heading);
        File.WriteAllText(path, string.Join("\n", lines));
    }

    /// <summary>
    ///     Asynchronously renders <paramref name="result"/> as Markdown (via
    ///     <see cref="QueryResultRenderer.RenderMarkdown"/>) and writes it to
    ///     <paramref name="path"/>, overwriting any existing file.
    /// </summary>
    /// <param name="result">The result to render and write.</param>
    /// <param name="path">The file path to write to.</param>
    /// <param name="depth">The Markdown heading depth; see <see cref="QueryResultRenderer.RenderMarkdown"/>.</param>
    /// <param name="heading">The custom Markdown heading text; see <see cref="QueryResultRenderer.RenderMarkdown"/>.</param>
    /// <param name="cancellationToken">A token to cancel the write operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be written; propagates uncaught.</exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the caller lacks permission to write to <paramref name="path"/>; propagates
    ///     uncaught.
    /// </exception>
    public static async Task WriteMarkdownAsync(
        QueryResult result, string path, int depth = 1, string? heading = null,
        CancellationToken cancellationToken = default)
    {
        var lines = QueryResultRenderer.RenderMarkdown(result, depth, heading);
        await File.WriteAllTextAsync(path, string.Join("\n", lines), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Renders <paramref name="result"/> as JSON (via
    ///     <see cref="QueryResultRenderer.RenderJson"/>) and writes it to <paramref name="path"/>,
    ///     overwriting any existing file.
    /// </summary>
    /// <param name="result">The result to render and write.</param>
    /// <param name="path">The file path to write to.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written; propagates uncaught.</exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the caller lacks permission to write to <paramref name="path"/>; propagates
    ///     uncaught.
    /// </exception>
    public static void WriteJson(QueryResult result, string path)
    {
        File.WriteAllText(path, QueryResultRenderer.RenderJson(result));
    }

    /// <summary>
    ///     Asynchronously renders <paramref name="result"/> as JSON (via
    ///     <see cref="QueryResultRenderer.RenderJson"/>) and writes it to <paramref name="path"/>,
    ///     overwriting any existing file.
    /// </summary>
    /// <param name="result">The result to render and write.</param>
    /// <param name="path">The file path to write to.</param>
    /// <param name="cancellationToken">A token to cancel the write operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be written; propagates uncaught.</exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the caller lacks permission to write to <paramref name="path"/>; propagates
    ///     uncaught.
    /// </exception>
    public static async Task WriteJsonAsync(
        QueryResult result, string path, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(path, QueryResultRenderer.RenderJson(result), cancellationToken)
            .ConfigureAwait(false);
    }
}
