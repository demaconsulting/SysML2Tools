##### AxisTransform

###### Purpose

`AxisTransform` is the final pipeline stage. It maps the abstract along/cross coordinates computed by
the earlier stages onto screen coordinates for the requested `LayoutDirection`, isolating all
direction handling to a single unit.

###### Responsibilities

The earlier stages always compute in the left-to-right-equivalent abstract axes (layer progression on
the +X axis, within-layer position on the +Y axis). For `LayoutDirection.Right` — the abstract
identity direction — those coordinates are already in screen space, so the stage performs no
transformation and the pipeline output is unchanged. The other directions (down, left, up) are
reserved scaffolding for a later phase and are not yet implemented.

###### Inputs and Outputs

- Reads: `LayeredGraph.Direction`.
- Writes: nothing for the identity direction (coordinates are left in place).

###### Error Handling

A null graph throws `ArgumentNullException`. Any direction other than `Right` throws
`NotSupportedException`.

###### Dependencies

- `LayeredGraph` (Layered) — the shared state whose direction is read.
- `LayoutDirection` (Layered) — the requested flow direction.
