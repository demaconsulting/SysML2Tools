#### ConnectedPairSpacer

##### Purpose

`ConnectedPairSpacer` pushes connected box pairs apart along their dominant axis so two boxes that
share a boundary edge keep a visible approach zone for the connector that joins them. It runs after
grid quantisation, where snapping can erase the gap that earlier compression reserved. Unconnected
boxes and already-separated pairs are left untouched, and box sizes never change.

##### Data Model

`ConnectedPairSpacer` is a static class with no instance state. Inputs are a `Rect[]` of boxes, a
list of `ConnectedPair` records (the two indices of each connected pair), an `approachZone`, and an
optional `maxPasses` bound. The result is a `Rect[]` of adjusted positions in input order.

##### Key Methods

###### `Space(boxes, pairs, approachZone, maxPasses)`

1. **Degenerate cases.** Empty boxes or no pairs return the input unchanged.
2. **Primary axis.** For each pair the axis is X when the centres are farther apart horizontally
   than vertically (ties pick X for determinism), matching the routing direction.
3. **Facing gap.** The clear gap between the two boxes on the primary axis is measured; the needed
   gap is `2 × approachZone` (one zone per face).
4. **Push.** When the gap is short, each centre is moved apart by half the deficit along that axis;
   the perpendicular axis is untouched so grid alignment is preserved.
5. **Convergence.** Passes repeat until no pair moves or the bound is reached; if it does not
   converge the original arrangement is returned unchanged.

##### Error Handling

A null `boxes` or `pairs` argument throws `ArgumentNullException`. All other inputs are handled
without throwing; the pass is bounded so it always terminates.

##### Dependencies

- `Rect` (Layout subsystem) — the geometric box value type.
- `ConnectedPair` — the index pair value type consumed by the spacer.

##### Callers

The interconnection view strategy, between grid quantisation and the final cleanup compression, to
restore directional clearance for connected pairs.
