##### ComponentPacker Verification

###### Verification Approach

`ComponentPacker` is verified through unit tests in `ComponentPackerTests` that build small graphs,
run the stage (wrapping the default inner stages), and assert on the resulting node coordinates and
routed waypoints. Tests cover connected-component detection (a connected core stays one component;
disconnected singletons become separate components), non-overlapping packing of disconnected
components, byte-equality of the single-component fast path against the default pipeline, the
empty-graph no-op and null-graph guard, deterministic component ordering, and translation of edge
waypoints with their owning component. The stage is pure and deterministic, so no mocking is
required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `ComponentPackerTests` pass with zero failures across all three target frameworks.
- A connected graph yields one component; disconnected nodes yield separate, non-overlapping components.
- The single-component output equals the default pipeline output exactly.
- An empty graph is a no-op; a null graph throws `ArgumentNullException`.
- Component order is deterministic, and edge waypoints translate with their component.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ComponentPacker_Apply_ConnectedCore_StaysOneComponent` | A connected graph lays out in one contiguous box |
| `ComponentPacker_Apply_DisconnectedSingletons_PackSeparately` | Disconnected nodes occupy non-overlapping boxes |
| `ComponentPacker_Apply_SingleComponent_EqualsDefaultPipeline` | Single-component output equals the default pipeline |
| `ComponentPacker_Apply_EmptyGraph_IsNoOp` | An empty graph leaves the outputs empty without throwing |
| `ComponentPacker_Apply_NullGraph_Throws` | A null graph throws `ArgumentNullException` |
| `ComponentPacker_Apply_ComponentOrder_IsDeterministic` | Repeated layouts produce identical coordinates |
| `ComponentPacker_Apply_Waypoints_TranslatedWithComponent` | Edge waypoints stay on their translated component |
