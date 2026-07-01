### Layout Engine Subsystem Verification

#### Verification Approach

The Engine subsystem is verified through unit tests, one test class per engine, that supply
synthetic geometric input and assert on the returned geometry. No mocking is required: the
engines have no dependencies beyond the geometric value types, so tests construct inputs
directly and check observable properties of the output (orthogonality, non-overlap, bounds,
determinism, and layer ordering).

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services,
files, or configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All engine test classes pass with zero failures across all three target frameworks.
- Routed paths consist solely of axis-aligned segments.
- Placed boxes within a common region do not overlap.
- Placed geometry lies within the reported region bounds.
- Repeated invocations with identical input produce identical geometry.

#### Test Scenarios

| Scenario | Engine | Assertion |
| --- | --- | --- |
| Orthogonal path with no obstacles | `ChannelRouter` | Every segment is axis-aligned |
| Path around an obstacle | `ChannelRouter` | No segment crosses the obstacle interior |
| Clean route respects clearance | `ChannelRouter` | Segments stay the requested clearance from obstacles |
| Bounded packing | `ContainmentPacker` | All packed rectangles lie within the reported bounds |
| Layered chain | `LayerAssigner` (Layered) | Layers increase monotonically along the flow direction |
| Independent components | `ComponentPacker` (Layered) | Separately packed components do not overlap |
| Deterministic packing | `ComponentPacker` (Layered) | Identical input yields identical geometry |
| Container packing | `ContainmentPacker` | Packed boxes fit within the container |
