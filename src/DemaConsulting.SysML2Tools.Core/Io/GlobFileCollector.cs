// <copyright file="GlobFileCollector.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

// cspell:ignore Metacharacters metacharacter metacharacters

using Microsoft.Extensions.FileSystemGlobbing;

namespace DemaConsulting.SysML2Tools.Io;

/// <summary>Collects files from the filesystem using gitignore-style glob patterns.</summary>
/// <remarks>
///     Supports absolute and relative patterns, exclusions via <c>!</c> prefixes, and
///     automatic extension filtering when a pattern's final segment is a bare <c>*</c>.
///     This utility is shared across the <c>lint</c>, <c>render</c>, and <c>query</c> CLI
///     commands so all three resolve file glob patterns identically. All members are
///     stateless and thread-safe.
/// </remarks>
public static class GlobFileCollector
{
    private static readonly char[] GlobMetacharacters = ['*', '?', '[', '{'];

    /// <summary>
    ///     Collects files from the filesystem matching the specified glob patterns,
    ///     filtered to files with extensions in <paramref name="extensions"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each pattern is evaluated directly against the filesystem. Patterns prefixed
    ///         with <c>!</c> are exclusion patterns — they remove matching files from the
    ///         accumulated result set. All other patterns are inclusion patterns that add
    ///         matching files to the result set. Patterns are processed in order: inclusions
    ///         build the set, then exclusions subtract from it.
    ///     </para>
    ///     <para>
    ///         Relative patterns are resolved against <paramref name="workingDirectory"/>.
    ///         Absolute patterns determine their own filesystem root from the longest
    ///         non-glob path prefix; the remainder becomes the glob tail passed to
    ///         <see cref="Matcher"/>.
    ///     </para>
    ///     <para>
    ///         When the final path segment of the glob tail is exactly <c>*</c> (a bare
    ///         wildcard with no extension), results are filtered to files whose extension
    ///         (case-insensitive) appears in <paramref name="extensions"/>. When the
    ///         final segment specifies an explicit extension (e.g. <c>*.sysml</c>,
    ///         <c>**/*.kerml</c>), all results are taken as-is without additional filtering.
    ///     </para>
    ///     <para>
    ///         When an absolute pattern contains no glob metacharacters it is treated as a
    ///         literal file path: if the file exists and its extension appears in
    ///         <paramref name="extensions"/> it is added to or removed from the result
    ///         set directly, without any directory traversal. Non-existent literal paths and
    ///         non-existent pattern roots are silently skipped; the method never throws for
    ///         missing files or directories.
    ///     </para>
    /// </remarks>
    /// <param name="patterns">
    ///     Ordered list of glob patterns. Entries prefixed with <c>!</c> are exclusion patterns.
    ///     Relative patterns are resolved against <paramref name="workingDirectory"/>.
    /// </param>
    /// <param name="extensions">
    ///     File extensions — including the leading dot (e.g. <c>.sysml</c>, <c>.kerml</c>) — used
    ///     to filter results when a pattern's final segment is a bare <c>*</c>.
    /// </param>
    /// <param name="workingDirectory">
    ///     Absolute path used as the root for relative patterns. Must be an absolute path.
    /// </param>
    /// <returns>
    ///     Sorted, deduplicated list of absolute file paths that match the accumulated
    ///     inclusion patterns and are not removed by any exclusion pattern.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="patterns"/>, <paramref name="extensions"/>,
    ///     or <paramref name="workingDirectory"/> is null. Empty collections and an empty
    ///     working directory string are valid; null references are not.
    /// </exception>
    public static IReadOnlyList<string> Collect(
        IEnumerable<string> patterns,
        IEnumerable<string> extensions,
        string workingDirectory)
    {
        // Guard against null arguments — empty patterns and extensions are valid
        // but null references indicate a programming error in the caller
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        var extensionSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

        // Ordinal comparison is correct here because every path added to `collected` carries
        // on-disk casing: glob paths come from Matcher.GetResultsInFullPath (which returns the
        // real filesystem entry name) and literal paths go through ResolveOnDiskPath (which uses
        // Directory.GetFiles to obtain the same on-disk name). Two patterns that refer to the
        // same physical file will therefore produce identical strings, so Ordinal deduplication
        // is exact on both case-sensitive (Linux) and case-insensitive (Windows/macOS) filesystems.
        var collected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in patterns)
        {
            // Parse the exclusion prefix and trim the pattern body
            var isExclusion = pattern.StartsWith('!');
            var patternBody = isExclusion ? pattern.Substring(1).Trim() : pattern.Trim();

            if (patternBody.Length == 0)
            {
                continue;
            }

            // Determine the filesystem root and the glob tail for this pattern
            var (root, globTail) = ParsePattern(patternBody, workingDirectory);

            if (globTail.Length == 0)
            {
                // No glob portion — root is a literal path; resolve on-disk casing so
                // the path key is consistent with paths returned by Matcher.GetResultsInFullPath
                if (extensionSet.Contains(Path.GetExtension(root)))
                {
                    var onDisk = ResolveOnDiskPath(root);
                    if (onDisk != null)
                    {
                        AccumulateResults(collected, [onDisk], isExclusion);
                    }
                }

                continue;
            }

            if (!Directory.Exists(root))
            {
                continue;
            }

            // Determine whether extension inference is needed (bare-star final segment)
            var needsExtensionFilter = HasBareStarFinalSegment(globTail);

            // Run the glob matcher against the resolved filesystem root
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude(globTail.Replace('\\', '/'));
            var results = matcher.GetResultsInFullPath(root);

            // Apply extension filter only when the final segment is a bare wildcard
            if (needsExtensionFilter)
            {
                results = results.Where(f => extensionSet.Contains(Path.GetExtension(f)));
            }

            // Accumulate: include patterns add files; exclusion patterns remove them
            AccumulateResults(collected, results, isExclusion);
        }

        return collected.OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Adds or removes the fully-qualified paths from <paramref name="results"/> in
    ///     <paramref name="collected"/> depending on whether the originating pattern was
    ///     an exclusion.
    /// </summary>
    /// <param name="collected">The mutable set of collected file paths to update in-place.</param>
    /// <param name="results">Glob result paths to incorporate or remove.</param>
    /// <param name="isExclusion">
    ///     When <see langword="true"/>, each matching file is removed from
    ///     <paramref name="collected"/>; when <see langword="false"/>, it is added.
    /// </param>
    private static void AccumulateResults(HashSet<string> collected, IEnumerable<string> results, bool isExclusion)
    {
        if (isExclusion)
        {
            foreach (var file in results)
            {
                collected.Remove(Path.GetFullPath(file));
            }
        }
        else
        {
            foreach (var file in results)
            {
                collected.Add(Path.GetFullPath(file));
            }
        }
    }

    /// <summary>
    ///     Resolves the on-disk path for a literal file path, correcting the casing to
    ///     match the actual filesystem entry.
    /// </summary>
    /// <remarks>
    ///     On case-insensitive filesystems (Windows, macOS) the caller-supplied casing may
    ///     differ from the entry stored on disk. Using
    ///     <see cref="Directory.GetFiles(string, string)"/> asks the OS to resolve the real
    ///     name, producing a path whose casing is consistent with the paths returned by
    ///     <see cref="Matcher"/>'s <c>GetResultsInFullPath</c>. On case-sensitive filesystems (Linux)
    ///     this is equivalent to a guarded existence check with exact case.
    /// </remarks>
    /// <param name="literalPath">The caller-supplied absolute file path to resolve.</param>
    /// <returns>
    ///     The absolute path with on-disk casing when the file exists;
    ///     <see langword="null"/> when the file does not exist or the path is malformed.
    /// </returns>
    private static string? ResolveOnDiskPath(string literalPath)
    {
        var directory = Path.GetDirectoryName(literalPath);
        var fileName = Path.GetFileName(literalPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        if (!Directory.Exists(directory))
        {
            return null;
        }

        var matches = Directory.GetFiles(directory, fileName);
        return matches.Length > 0 ? matches[0] : null;
    }

    /// <remarks>
    ///     The final segment is determined by splitting the glob tail on both forward and
    ///     backward slashes. A segment of <c>**</c> or <c>*.ext</c> does not trigger
    ///     extension inference; only the bare single-star <c>*</c> does.
    /// </remarks>
    /// <param name="globTail">The glob tail string to inspect (may use any directory separator).</param>
    /// <returns><see langword="true"/> when the final path segment is exactly <c>*</c>.</returns>
    private static bool HasBareStarFinalSegment(string globTail)
    {
        // Split on both separators to find the final path segment
        var lastSeparator = globTail.LastIndexOfAny(['/', '\\']);
        var lastSegment = lastSeparator >= 0
            ? globTail.Substring(lastSeparator + 1)
            : globTail;
        return lastSegment == "*";
    }

    /// <summary>
    ///     Splits a pattern body into a filesystem root and a glob tail to pass to
    ///     <see cref="Matcher"/>.
    /// </summary>
    /// <remarks>
    ///     For fully-qualified absolute patterns the root is the longest non-glob path prefix —
    ///     segments are included until the first one containing a glob metacharacter (<c>*</c>,
    ///     <c>?</c>, <c>[</c>, <c>{</c>). The remainder from the last directory separator
    ///     before the first glob metacharacter to the end is the glob tail.
    ///     For all other patterns (relative, or rooted-but-not-fully-qualified such as
    ///     <c>C:foo.sysml</c> or <c>\foo.sysml</c> on Windows) the root is
    ///     <paramref name="workingDirectory"/> and the entire pattern body is the glob tail, so
    ///     they resolve against the working directory.
    /// </remarks>
    /// <param name="patternBody">The pattern with any leading <c>!</c> prefix already stripped.</param>
    /// <param name="workingDirectory">Absolute path used as the root for relative patterns.</param>
    /// <returns>
    ///     A tuple of (root, globTail) where root is an absolute filesystem path and
    ///     globTail is the pattern string to pass to <see cref="Matcher"/>.
    /// </returns>
    private static (string Root, string GlobTail) ParsePattern(string patternBody, string workingDirectory)
    {
        if (Path.IsPathFullyQualified(patternBody))
        {
            // Normalize backslashes to forward slashes for uniform metacharacter scanning
            return SplitAbsolutePattern(patternBody.Replace('\\', '/'));
        }

        // Relative or rooted-but-not-fully-qualified pattern — resolve against workingDirectory
        return (workingDirectory, patternBody);
    }

    /// <summary>
    ///     Splits a normalized (forward-slash) absolute pattern into a static root prefix
    ///     and a glob tail by locating the first glob metacharacter.
    /// </summary>
    /// <remarks>
    ///     The root is everything up to (but not including) the last <c>/</c> before the
    ///     first glob metacharacter. The tail is everything after that separator. When
    ///     there is no <c>/</c> before the first glob, the root is empty and the tail is
    ///     the entire pattern. A leading <c>/</c> at position zero is preserved as the
    ///     root for Unix absolute paths such as <c>/opt/models/**/*.sysml</c>. The
    ///     trailing slash of a Windows drive root (<c>C:/</c>) is likewise preserved so
    ///     that <c>C:/*.sysml</c> resolves to the drive root rather than the
    ///     drive-relative path <c>C:</c>.
    /// </remarks>
    /// <param name="normalizedPattern">
    ///     Absolute pattern with all backslashes replaced by forward slashes.
    /// </param>
    /// <returns>
    ///     A (root, globTail) tuple where root is the longest static directory prefix and
    ///     globTail is the portion to pass to <see cref="Matcher.AddInclude"/>.
    /// </returns>
    private static (string Root, string GlobTail) SplitAbsolutePattern(string normalizedPattern)
    {
        // Find the index of the first glob metacharacter in the pattern
        var firstGlob = normalizedPattern.IndexOfAny(GlobMetacharacters);

        if (firstGlob < 0)
        {
            // No glob metacharacters — the entire pattern is a static path; nothing to glob
            return (normalizedPattern, string.Empty);
        }

        // Locate the last '/' strictly before the first glob metacharacter
        var prefix = normalizedPattern.Substring(0, firstGlob);
        var lastSlash = prefix.LastIndexOf('/');

        if (lastSlash < 0)
        {
            // No separator precedes the first glob — cannot derive a static root
            return (string.Empty, normalizedPattern);
        }

        // Preserve the trailing slash when the slash is a filesystem root boundary:
        //   lastSlash == 0 → Unix absolute root '/'  (e.g. "/*.sysml" → root "/")
        //   lastSlash == 2 && [1] == ':' → Windows drive root 'C:/' (e.g. "C:/*.sysml" → root "C:/")
        // Without the slash the result would be "" or "C:" — both are drive-relative, not absolute.
        var isRootBoundary = lastSlash == 0 || (lastSlash == 2 && normalizedPattern[1] == ':');
        var root = isRootBoundary ? normalizedPattern.Substring(0, lastSlash + 1) : normalizedPattern.Substring(0, lastSlash);
        var tail = normalizedPattern.Substring(lastSlash + 1);

        return (root, tail);
    }
}
