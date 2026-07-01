##### LayeredGraph Verification

###### Verification Approach

`LayeredGraph` is verified through unit tests in `LayeredGraphTests` that construct the object with
valid and invalid arguments and assert on the resulting state. Null-argument tests verify the
fail-fast contract; the construction test verifies that the supplied nodes, edges, direction, and
node count are preserved. The type holds no behavior beyond construction, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `LayeredGraphTests` pass with zero failures across all three target frameworks.
- Constructing with a null node list or null edge list throws `ArgumentNullException`.
- Construction preserves the supplied nodes, edges, and direction and reports the node count.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException` | Null nodes throws `ArgumentNullException` |
| `LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException` | Null edges throws `ArgumentNullException` |
| `LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount` | Stored fields match the input |
