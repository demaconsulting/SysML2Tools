#### HighwayAssigner

##### Purpose

`HighwayAssigner` performs coarse global routing over the gaps between block clusters. It detects the
horizontal corridors between rows and vertical corridors between columns, coarse-routes each edge
through the least-congested corridor, measures peak concurrent wire occupancy, and promotes corridors
that carry several wires into highways with a reserved trunk width. Bundling parallel wires into
shared corridors keeps a dense diagram readable and lets the detailed router prefer the highways
through a discounted routing cost.

##### Data Model

`HighwayAssigner` is a static class with no instance state. Inputs are a list of `HighwayBox` records
(rectangle plus id), a list of `HighwayEdge` records (block-index pairs with a connector type), a grid
unit, a wire spacing, and a minimum gap. The result is a `HighwayResult` carrying the detected
`Corridors`, one `EdgeAssignment` per edge, and a per-corridor `CostMultipliers` list, all in
deterministic order.

##### Key Methods

###### `Assign(boxes, edges, gridUnit, wireSpacing, minGap)`

1. **Degenerate case.** An empty box or edge list returns empty corridors and multipliers with every
   edge mapped to corridor -1.
2. **Corridor detection.** Block centres are clustered into rows and columns; a corridor sits at the
   midpoint of each adjacent cluster pair (horizontal between rows, vertical between columns).
3. **Coarse routing.** Each edge picks the perpendicular axis with the larger separation and the
   nearest corridor on it, breaking ties toward the lane with fewer wires.
4. **Sizing.** Peak concurrent occupancy is measured with a sweep line; reserved width is
   `peak × wireSpacing + 2 × minGap`. A corridor is a highway when it exceeds the gap and carries
   more than one wire; highways get a 0.6 cost multiplier, others 1.0. Positions snap to the grid.

##### Error Handling

Null `boxes` or `edges` arguments throw `ArgumentNullException`. Edges with out-of-range indices are
mapped to corridor -1. All other inputs are handled without throwing.

##### Dependencies

None beyond the BCL collections.

##### Callers

View layout strategies that compress and quantise blocks then route connectors; they feed the corridor
floors to `GravityCompressor` and the cost bands to `ChannelRouter`.
