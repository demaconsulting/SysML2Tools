### QualifiedNameShortener

#### Verification Approach

`QualifiedNameShortener` is verified with unit tests defined in `QualifiedNameShortenerTests.cs`.
Because `QualifiedNameShortener` performs pure string manipulation using only .NET BCL types, no
mocking or test doubles are required. Tests call `QualifiedNameShortener.Shorten` directly with
controlled pools of qualified names and assert on the returned map or the thrown exception type.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A pool of names sharing one leading segment has that segment stripped from every name.
- A pool of names sharing no common leading segment is returned unchanged.
- A pool containing a single distinct name is returned unchanged.
- A pool of all-identical names reduces to a single distinct name (per the "fewer than 2
  distinct names" edge case) and is returned unchanged, so its leaf segment is never stripped
  to an empty string.
- A pool sharing a deeper (2+ segment) common prefix has every shared segment stripped.
- The common-prefix length is capped at the shortest name's segment count minus 1, so a short
  name in the pool is never stripped down to nothing even when a longer name shares more
  leading segments than the short name has to spare.
- Null pools and null pool entries cause `ArgumentNullException`.
- Duplicate names in the pool produce exactly one entry per distinct name in the returned map.

#### Test Scenarios

**QualifiedNameShortener_Shorten_OneSharedLeadingSegment_StripsThatSegment**: The worked example
`["A::B::x", "A::B::y", "A::T::g"]` is shortened; the shared leading segment `"A"` is stripped
from every name, producing `["B::x", "B::y", "T::g"]`. This scenario is tested by
`QualifiedNameShortener_Shorten_OneSharedLeadingSegment_StripsThatSegment`.

**QualifiedNameShortener_Shorten_NoCommonPrefix_LeavesNamesUnchanged**: A pool of names rooted
in different top-level packages (`["A::B::x", "C::D::y"]`) is shortened; every name is returned
unchanged since no leading segment is shared. This scenario is tested by
`QualifiedNameShortener_Shorten_NoCommonPrefix_LeavesNamesUnchanged`.

**QualifiedNameShortener_Shorten_SingleNamePool_LeavesNameUnchanged**: A pool containing only
one distinct name is shortened; the name is returned unchanged since there is nothing to compare
it against. This scenario is tested by
`QualifiedNameShortener_Shorten_SingleNamePool_LeavesNameUnchanged`.

**QualifiedNameShortener_Shorten_AllIdenticalNames_KeepsLeafSegment**: A pool where every entry
is the same name (`["A::B::x", "A::B::x", "A::B::x"]`) reduces to a single distinct name and is
returned unchanged, confirming the leaf segment `"x"` is never stripped down to an empty string.
This scenario is tested by `QualifiedNameShortener_Shorten_AllIdenticalNames_KeepsLeafSegment`.

**QualifiedNameShortener_Shorten_DeeperCommonPrefix_StripsAllSharedSegments**: A pool sharing the
two leading segments `"A::B"` (`["A::B::C::x", "A::B::C::y", "A::B::D::z"]`) is shortened; both
shared segments are stripped from every name. This scenario is tested by
`QualifiedNameShortener_Shorten_DeeperCommonPrefix_StripsAllSharedSegments`.

**QualifiedNameShortener_Shorten_ShortestNameBoundsCap_RetainsShortestNamesLeaf**: A pool
containing `"A::B"` (2 segments) alongside `"A::B::C"` (which shares the 2-segment prefix
`"A::B"`) is shortened; only 1 segment (`"A"`) is stripped, capped by `"A::B"`'s own segment
count, so `"A::B"` becomes `"B"` rather than an empty string. This scenario is tested by
`QualifiedNameShortener_Shorten_ShortestNameBoundsCap_RetainsShortestNamesLeaf`.

**QualifiedNameShortener_Shorten_NullPool_ThrowsArgumentNullException**: `null` is passed as the
`qualifiedNames` argument; an `ArgumentNullException` is thrown. This scenario is tested by
`QualifiedNameShortener_Shorten_NullPool_ThrowsArgumentNullException`.

**QualifiedNameShortener_Shorten_NullEntryInPool_ThrowsArgumentNullException**: A pool
containing a `null` entry is passed; an `ArgumentNullException` is thrown. This scenario is
tested by `QualifiedNameShortener_Shorten_NullEntryInPool_ThrowsArgumentNullException`.

**QualifiedNameShortener_Shorten_DuplicateNamesInPool_ReturnsOneEntryPerDistinctName**: A pool
where one name is repeated (`["A::B::x", "A::B::x", "A::T::g"]`) is shortened; the returned map
contains exactly one entry per distinct name, both correctly shortened. This scenario is tested
by `QualifiedNameShortener_Shorten_DuplicateNamesInPool_ReturnsOneEntryPerDistinctName`.
