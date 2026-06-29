#### ChannelRouter

##### Purpose

`ChannelRouter` routes a single orthogonal connector between a source anchor and a target
anchor, steering around obstacle rectangles and keeping a requested clearance. It is the
engine through which all routed connector quality (state transitions, action successions,
interconnection connectors, specialization edges) flows.

##### Data Model

`ChannelRouter` is a static class with no instance state. Inputs are the source and target
`Point2D` anchors, a list of obstacle `Rect`, a clearance distance, optional source and target
`PortSide` values, and an optional list of `CostBand` records. The result is a `RouteResult`
record carrying the ordered `Waypoints` and a `Crossed` flag.

##### Key Methods

###### `RouteWithStatus(source, target, obstacles, clearance, sourceSide?, targetSide?, costBands?)`

Computes the route and reports whether it had to cross an obstacle. The algorithm is:

1. **Perpendicular stubs.** When a side is supplied, the anchor is stepped off its edge by a
   short stub so the connector leaves and enters boxes at right angles. Each stub length is
   capped to half the gap to the opposing anchor along the step axis, so two stubs facing
   each other across a narrow gap meet at the midline instead of overshooting (which would
   produce a visible reversal at the arrowhead).
2. **Grid construction.** Candidate grid lines are built from the two endpoint coordinates
   plus each obstacle's near and far edges offset outward by the current clearance.
3. **Clearance-retry ladder.** An A\*-style search runs over the grid at successively smaller
   clearances — full, half, quarter, then zero. Segments are rejected when they pass within
   the current clearance of an obstacle (the obstacles are inflated by the clearance and
   tested with strict inequalities, so a segment exactly one clearance away is allowed). The
   largest clearance that yields an obstacle-free path is used. At clearance zero, grid lines
   sit on box edges and edge-hugging routes are permitted, so a clean path is found in almost
   all cases.
4. **Crossing fallback.** Only when no obstacle-free path exists at any clearance (for example
   an enclosed target) does the router fall back to a best-effort L-shape and set
   `Crossed = true`.
5. **Finalize.** The original anchors are re-attached outside their stubs and the path is
   simplified — collinear interior points are removed, but U-turns are preserved so a
   perpendicular stub is never collapsed, and duplicate points are dropped.

The turn penalty in the search biases toward routes with fewer bends, so connectors prefer
straight runs where the geometry allows. When cost bands are supplied, each segment's length is
scaled by the cheapest band covering its midpoint, so a 0.6 highway band attracts wires into shared
corridors; a null band list leaves cost neutral.

###### `Route(...)`

A thin wrapper that returns only the `Waypoints` of `RouteWithStatus`, for callers that do
not need the crossing status.

##### Error Handling

Null `source`, `target`, or `obstacles` arguments throw `ArgumentNullException`. Degenerate
geometry never throws: when no clean route exists the router returns a crossing route with
`Crossed = true` rather than failing, leaving the decision to surface a warning to the caller.

##### Dependencies

- `Point2D` and the internal `Rect` geometric value types (Layout subsystem).
- `PortSide` (Layout subsystem) for perpendicular-stub direction.

##### Callers

Every view strategy that draws connectors: `GeneralViewLayoutStrategy`,
`InterconnectionViewLayoutStrategy`, `StateTransitionViewLayoutStrategy`, and
`ActionFlowViewLayoutStrategy`. The `Crossed` flag feeds `LayoutWarnings`.
