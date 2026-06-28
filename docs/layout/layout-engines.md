# Layout Engines

The layout subsystem provides eight reusable, stateless engines in `Layout/Engine/`. Each
engine accepts plain geometric input (sizes, connection pairs, obstacle rectangles) and
returns computed geometry. No engine references the SysML semantic model.

## Existing Engines

### ChannelRouter

Routes an orthogonal connector between two anchors using A\* on a Hanan grid. Clears
obstacles with configurable `EdgeClearance`. Extended in Layout Engine v2 to accept a
per-cell cost-multiplier map (highway discounts).

### ForceDirectedEngine

Fruchterman-Reingold spring placement. Extended in Layout Engine v2 with:

- Dimensionless force model with explicit rest length `r(ŵ) = L·(1.5 − ŵ)` and
  characteristic length `L = mean(block_diagonal) + EdgeClearance`
- Dimensionless hierarchy-gravity ratio `κ_h`
- Temperature-annealed wire-pressure force at block boundaries
- Cooling schedule retained; kinetic energy exposed as a termination signal
- Barnes–Hut quadtree repulsion above a node-count threshold (O(n log n))
- Deterministic jitter for coincident seed positions

### LayeredLayoutEngine

Simplified Sugiyama (layer assignment + barycenter ordering + x-alignment). Extended in
Layout Engine v2 with:

- Monte Carlo multi-seed option with size-scaled `K`
- Fixed barycenter sweep budget (8) with keep-best (no "to convergence" looping)
- **Virtual/dummy nodes** for edges spanning more than one layer, ordered and routed
  through intermediate layers
- Per-seed crossing count exposed for seed selection

### ContainmentPacker

Bottom-up bin-packing of children in a container. Extended in Layout Engine v2 to emit a
gap array compatible with `GravityCompressor` so the pipeline can recurse into a
container and compress its interior at the same density as the top level.

### PortAssigner

Port-side assignment and slot distribution along a box edge. Extended in Layout Engine v2
to be **highway-aware**: it chooses the box face pointing at an edge's committed corridor
and orders slots along each face to match the corridor's wire order, so stubs do not cross
at the box boundary. When several wires share the same
`(face, directionality, highway_id, connector_type)` it collapses them into a single
**merged trunk** with a comb fan-out (see Port Merging), exposing **one** corridor-facing
port point on that face instead of N independent slots; the comb then fans the trunk,
without crossings, into the N individual corridor lanes that carry the wires on to their
separate far-end blocks. Runs symmetrically for both outgoing groups (source-side fan-out)
and incoming groups (destination-side fan-in) on every face of every block. Also assigns
container-boundary ports for edges that cross into a container.

## New Engines (Layout Engine v2)

### ConnectivityAnalyzer

Computes the sparse affinity adjacency, layer hints, community (cluster) membership via
label-propagation/Louvain, and crossing-minimisation scores from a graph of blocks and
edges. Pure graph arithmetic; no geometry; no dense n² matrix.

### HighwayAssigner

Performs global routing on the coarse grid, scores channels, classifies highways by the
geometric necessity rule (`required_width > min_gap`), assigns edges to corridors with a
capped reserved width (`W_cap`, two-sided split for hubs), and produces a per-cell
cost-multiplier map plus hard corridor-membership constraints for `ChannelRouter`.

### GravityCompressor

Implements the closed-form, corridor-constrained gap sizing (Step 8). Accepts the
committed corridor assignments, wire-and-label bounding boxes, and highway floor
constraints; returns the minimal feasible gap array and a feasibility flag. Includes the
bounded (≤2) outer re-evaluation that keeps the best feasible result.

### GridQuantizer

Performs 1-D constraint-graph compaction per axis: snaps positions and sizes to grid unit
G while preserving order and minimum separations (including highway widths) by
construction. Applies only *local* width/height unification. Accepts the current
placed-box list; returns the quantised, feasible placed-box list.

---
