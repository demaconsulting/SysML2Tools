#### ContainmentPacker Verification

##### Verification Approach

`ContainmentPacker` is verified through unit tests in `ContainmentPackerTests` that construct
explicit item lists with known sizes and assert on the returned packing. Row sharing and wrapping
are checked by comparing the Y coordinates of placed rectangles; a geometric helper in the test
class checks whether two rectangles overlap, and bounds are checked against the reported region
size. The oversized-item case is exercised directly. No mocking is required; the packer is pure and
deterministic.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ContainmentPackerTests` pass with zero failures across all three target frameworks.
- Items that fit within the content width share a single row, ordered left to right.
- An item that would exceed the content width wraps to a new row below.
- A mixed-size set produces no overlapping rectangles.
- Every packed rectangle lies within the reported region bounds.
- An item wider than the content width is placed alone and the region widens to contain it.
- An empty list yields an empty result sized only by the padding; a single item sits at the padding
  origin.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Pack_EmptyList_ReturnsPaddingOnlyRegion` | No rectangles; region is `2 * padding` on each axis |
| `Pack_SingleItem_PositionsAtPaddingOrigin` | Lone item at the padding origin; region wraps it plus padding |
| `Pack_ItemsFitInRow_ShareSameRow` | Items that fit share a row with increasing X positions |
| `Pack_ItemsExceedWidth_WrapToNewRow` | Overflowing item wraps to a new row at the left padding origin |
| `Pack_MixedSizes_ProducesNoOverlaps` | Every pair of packed rectangles is disjoint |
| `Pack_MixedSizes_AllRectsWithinBounds` | All rectangles lie within the reported region bounds |
| `Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens` | Oversized item placed alone; region grows to fit it |
