##### LayeredLayoutPipeline Verification

###### Verification Approach

`LayeredLayoutPipeline` is verified through unit tests in `LayeredLayoutPipelineTests` that assemble
pipelines with the fluent builder and exercise their assembly and execution behavior. The default
pipeline is run over a chain graph to confirm it executes end to end and populates the result; the
builder is exercised with the recursive hierarchy mode and with null inputs to confirm the
fail-fast contracts. The default stage order is also exercised indirectly by the subsystem
pipeline-equivalence tests.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `LayeredLayoutPipelineTests` pass with zero failures across all three target frameworks.
- The default pipeline runs over a chain graph without throwing and populates one waypoint list per edge.
- Building a recursive-hierarchy pipeline throws `NotSupportedException`.
- Adding a null stage and running with a null graph both throw `ArgumentNullException`.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing` | One waypoint list per edge |
| `LayeredLayoutPipeline_Build_RecursiveHierarchy_ThrowsNotSupportedException` | Throws `NotSupportedException` |
| `LayeredLayoutPipeline_AddStage_NullStage_ThrowsArgumentNullException` | Null stage throws `ArgumentNullException` |
| `LayeredLayoutPipeline_Run_NullGraph_ThrowsArgumentNullException` | Null graph throws `ArgumentNullException` |
