#### GravityCompressor

##### Purpose

`GravityCompressor` removes overlaps between placed boxes by separating each colliding pair to a
requested minimum gap, leaving already-clear pairs untouched. Boxes move monotonically apart along
the axis of least penetration, and each box's label-inclusive minimum extent reserves clearance for
labels. It tightens an oversized placement to a feasible, readable footprint before edge routing.

##### Data Model

`GravityCompressor` is a static class with no instance state. Inputs are a list of `CompressBox`
records (position, drawn size, and label-inclusive minimum extent), a `minGap`, a `gridUnit`, and an
optional list of `CorridorConstraint` records carrying highway floor widths. The result is a
`CompressResult` record carrying the `Positions` (one `CompressedPosition` per box) and a `Feasible`
flag.

##### Key Methods

###### `Compress(boxes, minGap, gridUnit, corridors)`

1. **Degenerate cases.** An empty list is feasible with no positions; a negative `minGap` is
   infeasible.
2. **Corridors.** When constraints are supplied, each corridor gap is first widened to its minimum
   width by pushing the adjacent box clusters apart; with no corridors the pass is unchanged.
3. **Separation.** Centres are pushed apart pairwise until every pair clears `minGap`, resolving
   along the least-penetration axis. Already-clear pairs are not moved.
4. **Snap.** Resulting positions snap to the grid unit when one is supplied.
5. **Feasibility.** If overlaps remain after a bounded number of passes the result is flagged
   infeasible.

##### Error Handling

A null `boxes` argument throws `ArgumentNullException`. All other inputs are handled without throwing;
the separation pass is bounded so it always terminates.

##### Dependencies

- `CompressedPosition` — the position value type returned in the result.

##### Callers

View layout strategies that need to compress an oversized force or layered placement to minimum
clearance before routing connectors.
