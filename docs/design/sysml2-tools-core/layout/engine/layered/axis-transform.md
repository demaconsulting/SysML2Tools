##### AxisTransform

###### Purpose

`AxisTransform` is the final pipeline stage. It maps the abstract along/cross coordinates computed by
the earlier stages onto screen coordinates for the requested `LayoutDirection`, isolating all
direction handling to a single unit.

###### Responsibilities

The earlier stages always compute in the left-to-right-equivalent abstract axes (layer progression on
the +X axis, within-layer position on the +Y axis); a node's along-extent is its width and its
cross-extent its height. This stage converts those abstract coordinates into screen coordinates for
each direction:

- **Right** is the abstract identity direction — coordinates are already in screen space, so the stage
  performs no transformation and the pipeline output is byte-identical to the pre-direction baseline.
- **Down** is a pure transpose: along maps to screen Y and cross to screen X. A source point on the
  abstract right face becomes the node's SOUTH face and a target point on the abstract left face
  becomes the node's NORTH face, so the SOUTH-exit / NORTH-entry behaviour emerges from the mapping
  without separate face logic.
- **Left** reflects the along-axis about its maximum (screen X = max − along); cross is unchanged.
- **Up** transposes and reflects the along-axis into screen Y.

Mapping top-to-bottom or bottom-to-top additionally requires the along-axis to be driven by the node
*height* rather than its width, so a final coordinate remap alone is insufficient. The static
`NormalizeInputAxes` method swaps each input node's width and height for the down/up directions before
the stages run (a no-op for right/left, which keeps those outputs byte-identical); the pipeline calls
it at the start of `Run`. The instance `Apply` method then performs the final coordinate remap over the
augmented-node positions and every edge's waypoints.

###### Inputs and Outputs

- Reads: `LayeredGraph.Direction`, `AugNodes` (along-extents), `AugX`/`AugY`, and `Waypoints`.
- Writes: the remapped `AugX`/`AugY` and `Waypoints` (no-op for the right identity direction).
  `NormalizeInputAxes` writes the swapped `Nodes` for the down/up directions.

###### Error Handling

A null graph throws `ArgumentNullException`. All four layout directions are supported.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state whose direction, node sizes, positions, and waypoints
  are read and (for non-right directions) rewritten; `SwapNodeAxes` is the input-normalization seam.
- `LayoutDirection` (Layered) — the requested flow direction.
