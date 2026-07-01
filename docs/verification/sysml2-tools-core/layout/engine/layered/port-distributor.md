##### PortDistributor Verification

###### Verification Approach

`PortDistributor` is verified through unit tests in `PortDistributorTests` that run the stages up to
and including port distribution and assert on the produced port arrays. One test confirms that a
single edge's source and target ports lie within their respective node faces; another confirms that
one source and one target port are recorded for every sub-edge. The stage is pure and deterministic,
so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `PortDistributorTests` pass with zero failures across all three target frameworks.
- A single edge's ports fall within the source and target node faces.
- One source and one target port Y is recorded for every sub-edge.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces` | The edge's ports lie within their node faces |
| `PortDistributor_Apply_AssignsPortYForEverySubEdge` | The port arrays have one entry per sub-edge |
