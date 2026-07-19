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

using DemaConsulting.SysML2Tools.Utilities;

namespace DemaConsulting.SysML2Tools.Tests;

/// <summary>
///     Tests for the QualifiedNameShortener class.
/// </summary>
[Collection("Sequential")]
public class QualifiedNameShortenerTests
{
    /// <summary>
    ///     Test that Shorten strips the single shared leading segment across a pool of names,
    ///     retaining each name's remaining segments.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_OneSharedLeadingSegment_StripsThatSegment()
    {
        // Arrange: three names sharing the single leading segment "A"
        string[] names = ["A::B::x", "A::B::y", "A::T::g"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: the shared "A" segment is stripped from every name
        Assert.Equal("B::x", result["A::B::x"]);
        Assert.Equal("B::y", result["A::B::y"]);
        Assert.Equal("T::g", result["A::T::g"]);
    }

    /// <summary>
    ///     Test that Shorten leaves every name unchanged when the pool shares no common leading
    ///     segment (different top-level packages).
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_NoCommonPrefix_LeavesNamesUnchanged()
    {
        // Arrange: names rooted in different top-level packages
        string[] names = ["A::B::x", "C::D::y"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: no stripping occurred
        Assert.Equal("A::B::x", result["A::B::x"]);
        Assert.Equal("C::D::y", result["C::D::y"]);
    }

    /// <summary>
    ///     Test that Shorten leaves a single-name pool unchanged, since there is nothing to
    ///     compare it against.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_SingleNamePool_LeavesNameUnchanged()
    {
        // Arrange: a pool containing only one distinct name
        string[] names = ["A::B::x"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: the name is returned unchanged
        Assert.Single(result);
        Assert.Equal("A::B::x", result["A::B::x"]);
    }

    /// <summary>
    ///     Test that Shorten never strips a name down to an empty string: when every name in
    ///     the pool is identical, only one distinct name remains, which - per the "fewer than 2
    ///     distinct names" edge case - is returned unchanged (trivially retaining its leaf).
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_AllIdenticalNames_KeepsLeafSegment()
    {
        // Arrange: a pool of duplicate entries for the same name (e.g., referenced by multiple
        // entries in a dependencies result)
        string[] names = ["A::B::x", "A::B::x", "A::B::x"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: only one distinct name remains, unchanged, so its leaf segment "x" is never
        // stripped away to an empty string
        Assert.Single(result);
        Assert.Equal("A::B::x", result["A::B::x"]);
        Assert.EndsWith("x", result["A::B::x"], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that Shorten strips every shared leading segment when the pool shares a deeper
    ///     (2+ segment) common prefix.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_DeeperCommonPrefix_StripsAllSharedSegments()
    {
        // Arrange: names sharing the two leading segments "A::B"
        string[] names = ["A::B::C::x", "A::B::C::y", "A::B::D::z"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: both shared segments "A::B" are stripped from every name
        Assert.Equal("C::x", result["A::B::C::x"]);
        Assert.Equal("C::y", result["A::B::C::y"]);
        Assert.Equal("D::z", result["A::B::D::z"]);
    }

    /// <summary>
    ///     Test that Shorten caps the common-prefix length at the shortest name's segment count
    ///     minus 1, so a short name is never stripped down to nothing even when a longer name in
    ///     the pool shares more leading segments than the short name has to spare.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_ShortestNameBoundsCap_RetainsShortestNamesLeaf()
    {
        // Arrange: "A::B" has only 2 segments (cap = 1), even though "A::B::C" shares the
        // 2-segment prefix "A::B" with it
        string[] names = ["A::B", "A::B::C"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: only 1 segment ("A") is stripped, not 2, so "A::B" is never reduced to ""
        Assert.Equal("B", result["A::B"]);
        Assert.Equal("B::C", result["A::B::C"]);
    }

    /// <summary>
    ///     Test that Shorten throws ArgumentNullException when the pool itself is null.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_NullPool_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert: null pool throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => QualifiedNameShortener.Shorten(null!));
    }

    /// <summary>
    ///     Test that Shorten throws ArgumentNullException when the pool contains a null name.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_NullEntryInPool_ThrowsArgumentNullException()
    {
        // Arrange: a pool containing a null entry
        List<string> names = ["A::B::x", null!];

        // Act & Assert: a null entry throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => QualifiedNameShortener.Shorten(names));
    }

    /// <summary>
    ///     Test that Shorten deduplicates the pool, so a name repeated many times produces only
    ///     one entry in the returned map.
    /// </summary>
    [Fact]
    public void QualifiedNameShortener_Shorten_DuplicateNamesInPool_ReturnsOneEntryPerDistinctName()
    {
        // Arrange: a pool where "A::B::x" is repeated
        string[] names = ["A::B::x", "A::B::x", "A::T::g"];

        // Act: execute the operation being tested
        var result = QualifiedNameShortener.Shorten(names);

        // Assert: exactly two distinct entries are returned, both shortened
        Assert.Equal(2, result.Count);
        Assert.Equal("B::x", result["A::B::x"]);
        Assert.Equal("T::g", result["A::T::g"]);
    }
}
