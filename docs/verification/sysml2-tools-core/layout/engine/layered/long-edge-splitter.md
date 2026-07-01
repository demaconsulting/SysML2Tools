##### LongEdgeSplitter Verification

###### Verification Approach

`LongEdgeSplitter` is verified through unit tests in `LongEdgeSplitterTests` that run the
cycle-breaking, layering, and splitting stages over graphs with and without long edges and assert on
the resulting `AugNodes`. A unit-span chain confirms that no dummy nodes are added; a span-three edge
confirms that one dummy node is inserted per intermediate layer. The stage is pure and deterministic,
so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `LongEdgeSplitterTests` pass with zero failures across all three target frameworks.
- A graph with only unit-span edges produces no dummy nodes.
- A span-three edge inserts exactly two dummy nodes.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LongEdgeSplitter_Apply_SpanOneEdge_AddsNoDummyNodes` | Augmented node count equals the input node count |
| `LongEdgeSplitter_Apply_LongEdge_InsertsDummyNodesPerIntermediateLayer` | A span-three edge adds two dummy nodes |
