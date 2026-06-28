#### GridQuantizer

##### Purpose

`GridQuantizer` snaps box positions and sizes to a pixel grid and unifies the widths of boxes sharing
a column and the heights of boxes sharing a row, so aligned blocks become exactly equal and anchor
points fall on predictable grid lines. Grid alignment gives the fine router far fewer unique
positions and makes wire spacing exact integer arithmetic.

##### Data Model

`GridQuantizer` is a static class with no instance state. Inputs are a list of `QuantizeBox` records
(position and size), a `gridUnit`, and a `clusterTolerance`. The result is an ordered list of
`PackedRect` rectangles, one per input box in input order.

##### Key Methods

###### `Quantize(boxes, gridUnit, clusterTolerance)`

1. **Snap.** Each position snaps to the nearest grid multiple; each size rounds up to the next grid
   multiple.
2. **Unify.** Boxes whose start edges cluster within `clusterTolerance` share a column or row; each
   group's extent is widened to its largest member, never shrunk.
3. **Isolation.** Boxes in different columns or rows are never unified together, preserving the free
   2D structure.

##### Error Handling

A null `boxes` argument throws `ArgumentNullException`. A non-positive grid unit disables snapping.
All other inputs are handled without throwing.

##### Dependencies

- `PackedRect` (Layout subsystem) — the quantised-rectangle value type returned.

##### Callers

View layout strategies that align a force or compressed placement to a grid before and after routing.
