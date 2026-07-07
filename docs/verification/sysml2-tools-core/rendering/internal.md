### Rendering Internal Subsystem Verification

#### Verification Approach

The internal rendering components are verified through unit tests. `DiagramTypeRouter` is
covered by `DiagramTypeRouterTests`, which construct view nodes of each kind and assert on the
concrete strategy returned. The `StdlibFilter` behavior is verified indirectly through the
view-strategy tests that assert standard-library elements are excluded from the produced
layout. No mocking is required; both components are pure and deterministic.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files,
or configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All `DiagramTypeRouterTests` pass with zero failures across all three target frameworks.
- Each recognized view kind routes to its corresponding strategy.
- A view matching no recognized kind routes to the general view strategy.
- A view declaring `render asTreeDiagram;` or `render asInterconnectionDiagram;` routes to the
  browser or interconnection strategy regardless of the view's name, taking precedence over the
  name/supertype heuristic.
- A view declaring `render asElementTable;`, `render asTextualNotation;`, or any other
  unrecognized (or absent) render target falls through unchanged to the existing
  name-heuristic/general-view behavior, with no diagnostic.
- Standard-library elements do not appear in the produced layout.

#### Test Scenarios

| Scenario | Assertion |
| --- | --- |
| Interconnection-named or specializing view | Routes to the interconnection strategy |
| State transition / action flow / matrix / browser / sequence views | Route to their strategies |
| Plain view | Routes to the general view strategy |
| `render asTreeDiagram;` view | Routes to the browser (tree) strategy regardless of name |
| `render asInterconnectionDiagram;` view | Routes to the interconnection strategy regardless of name |
| `asElementTable;` / `asTextualNotation;` / other render target | Falls through unchanged, no diagnostic |
| Standard-library-only workspace | Produces a minimal canvas (stdlib excluded) |
