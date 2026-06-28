#### ContainmentPacker

##### Purpose

`ContainmentPacker` arranges a sequence of variable-size items into rows within a width budget. It
places items left to right, wraps to a new row when the next item would exceed the maximum content
width, and sizes the enclosing region to fit all items plus uniform outer padding. It is used to
pack child elements inside a containing box (for example, the parts inside a block) in a compact,
ordered grid.

##### Data Model

`ContainmentPacker` is a static class with no instance state. Inputs are a list of `PackItem`
records (each a `Width` and `Height`), a `maxContentWidth`, a `horizontalGap`, a `verticalGap`, and
a `padding`. The result is a `PackResult` record carrying the region `Width`, `Height`, and the
ordered list of `PackedRect` rectangles, one per input item in input order, each positioned
relative to the region origin `(0, 0)`.

##### Key Methods

###### `Pack(items, maxContentWidth, horizontalGap, verticalGap, padding)`

Computes the packing. The algorithm is a single left-to-right shelf (row) pass:

1. **Degenerate case.** An empty item list returns a region of `2 * padding` on each axis with no
   rectangles.
2. **Row filling.** A horizontal cursor starts at the left padding offset. Each item is placed at
   the current cursor and the cursor advances past the item plus `horizontalGap`. The row's height
   tracks the tallest item placed so far.
3. **Wrapping.** Before placing an item that is not the first in its row, the packer checks whether
   its right edge would exceed `padding + maxContentWidth`. If so, it drops to a new row below the
   current one (advancing the row top by the current row height plus `verticalGap`), resets the
   cursor to the left padding offset, and places the item there. Because the first-in-row item is
   exempt from the check, an item wider than the content width is placed alone on its own row rather
   than being dropped, and the region width grows to contain it.
4. **Region sizing.** The total width is the widest row's right edge plus padding; the total height
   is the last row's bottom plus padding. Tracking the widest content right edge across all rows is
   what lets an oversized item extend the region width.

Input order is preserved, and the left-to-right, no-backtracking placement is what guarantees that
no two rectangles overlap and that every rectangle stays within the reported region.

##### Error Handling

A null `items` argument throws `ArgumentNullException`. An empty item list returns a padding-only
region. No other input causes a throw; an oversized item is handled by the first-in-row exemption
rather than by an error.

##### Dependencies

- `PackedRect` (Layout subsystem) — the placed-rectangle value type returned in the result. This
  type is declared alongside `ContainmentPacker`.

##### Callers

View layout strategies that pack child elements inside a containing box, using the returned
rectangles to position children and the region size to size the container.
