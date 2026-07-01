#### Layout Engine Layered Subsystem Verification

##### Verification Approach

The Layered subsystem is verified at two levels. Each stage unit has its own test class that drives
the stage through the internal `LayeredGraph` API: it runs the prerequisite stages to populate the
inputs, runs the stage under test, and asserts on the fields the stage produces. At the subsystem
level, the pipeline-equivalence tests feed many graphs through both the legacy monolithic engine and
the assembled pipeline and assert that every field of the result is bit-for-bit identical. The
stages are pure and deterministic, so all assertions are stable across repeated runs and no mocking
is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All Layered stage test classes pass with zero failures across all three target frameworks.
- The assembled default pipeline runs to completion for chain, diamond, long-edge, and cyclic graphs.
- For every test input the pipeline output matches the legacy engine byte for byte.
- Recursive hierarchy handling is rejected with a clear error until it is implemented; the right,
  down, left, and up directions are all supported and mapped by `AxisTransform`.

##### Test Scenarios

| Scenario | Unit | Assertion |
| --- | --- | --- |
| Default pipeline over a chain | `LayeredLayoutPipeline` | Runs without throwing and populates waypoints |
| Recursive hierarchy requested | `LayeredLayoutPipeline` | `Build` throws `NotSupportedException` |
| Equivalence over random graphs | subsystem | Every result field matches the legacy oracle exactly |
| Right-direction transform | `AxisTransform` | Coordinates are left unchanged |
| Down/left/up direction transform | `AxisTransform` | Target placed on the correct side; waypoints stay orthogonal |
