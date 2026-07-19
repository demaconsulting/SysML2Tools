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

namespace DemaConsulting.SysML2Tools.Utilities;

/// <summary>
///     Reusable utility that strips the longest shared leading "::"-segment prefix from a set
///     of qualified names, so Markdown-oriented renderers can present names more compactly
///     without losing distinguishing information.
/// </summary>
/// <remarks>
///     Deliberately verb/format-agnostic (holds no reference to any query/render type) so any
///     future renderer needing the same compaction can reuse it without new coupling. Stateless
///     and thread-safe; performs no I/O.
/// </remarks>
internal static class QualifiedNameShortener
{
    /// <summary>
    ///     The "::" segment delimiter used by SysML qualified names.
    /// </summary>
    private const string SegmentSeparator = "::";

    /// <summary>
    ///     Computes a shortened form of every supplied qualified name by stripping the longest
    ///     run of leading "::"-delimited segments common to ALL names in
    ///     <paramref name="qualifiedNames"/>, capped so every name always retains at least its
    ///     own final (leaf) segment.
    /// </summary>
    /// <param name="qualifiedNames">
    ///     The pool of qualified names to shorten together (e.g., a query subject's name plus
    ///     every related entry's name) - the common prefix is computed across the whole pool,
    ///     not per-name, so every returned mapping strips the same number of leading segments.
    ///     Must not be null; entries must not be null. May contain duplicates.
    /// </param>
    /// <returns>
    ///     A dictionary mapping each distinct original name (ordinal key comparison) to its
    ///     shortened form. When <paramref name="qualifiedNames"/> has fewer than 2 distinct
    ///     names, or the names share no common leading segment, every value equals its key
    ///     unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="qualifiedNames"/> or any contained name is
    ///     <see langword="null"/>.
    /// </exception>
    internal static IReadOnlyDictionary<string, string> Shorten(IReadOnlyList<string> qualifiedNames)
    {
        // Validate inputs - a null pool or a null entry cannot be split into segments
        ArgumentNullException.ThrowIfNull(qualifiedNames);
        foreach (var name in qualifiedNames)
        {
            ArgumentNullException.ThrowIfNull(name);
        }

        // Reduce to the distinct names actually present - the common prefix (and the returned
        // map's keys) only need to be computed once per distinct name, regardless of how many
        // times it appears in the pool (e.g., the same dependency referenced by many entries)
        var distinct = qualifiedNames.Distinct(StringComparer.Ordinal).ToList();

        // Fewer than 2 distinct names means there is nothing to compare against, so stripping
        // is skipped entirely per the "always retain distinguishing information" contract
        if (distinct.Count < 2)
        {
            return distinct.ToDictionary(name => name, name => name, StringComparer.Ordinal);
        }

        // Split every distinct name into its "::" segments once, reused by both the prefix
        // computation and the final join
        var segmentLists = distinct.Select(name => name.Split(SegmentSeparator)).ToList();

        // Compute the common-prefix length, already capped so every name keeps its own leaf
        var commonPrefixLength = ComputeCommonPrefixLength(segmentLists);

        // A zero-length common prefix means no stripping is possible; otherwise strip that many
        // leading segments from every distinct name and rejoin the remainder
        if (commonPrefixLength == 0)
        {
            return distinct.ToDictionary(name => name, name => name, StringComparer.Ordinal);
        }

        var map = new Dictionary<string, string>(distinct.Count, StringComparer.Ordinal);
        for (var i = 0; i < distinct.Count; i++)
        {
            map[distinct[i]] = string.Join(SegmentSeparator, segmentLists[i][commonPrefixLength..]);
        }

        return map;
    }

    /// <summary>
    ///     Computes the length of the longest run of leading segments identical (ordinal) across
    ///     every entry of <paramref name="segmentLists"/>, capped so every name always retains
    ///     at least its own final (leaf) segment.
    /// </summary>
    /// <param name="segmentLists">
    ///     The "::"-split segments of every distinct qualified name in the pool; must contain at
    ///     least 2 entries.
    /// </param>
    /// <returns>The common-prefix length, in segments (never negative).</returns>
    private static int ComputeCommonPrefixLength(IReadOnlyList<string[]> segmentLists)
    {
        // Cap the search at the shortest name's segment count minus 1, so the loop below can
        // never strip a name down to nothing - every name always keeps its own leaf segment
        var cap = segmentLists.Min(segments => segments.Length) - 1;

        // Walk segment indices up to the cap, stopping at the first index where not every name
        // agrees on that segment's value; the count of indices that matched is the answer
        var length = 0;
        while (length < cap && segmentLists.All(segments =>
                   string.Equals(segments[length], segmentLists[0][length], StringComparison.Ordinal)))
        {
            length++;
        }

        return length;
    }
}
