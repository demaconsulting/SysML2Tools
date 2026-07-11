#### DiagramTypeRouter Verification

##### Verification Approach

`DiagramTypeRouter` is verified through unit tests in `DiagramTypeRouterTests` that construct a
view node of each kind (by name and by specialization) and assert that `GetStrategy` returns the
expected concrete strategy type. No mocking is required; the router is pure and deterministic.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `DiagramTypeRouterTests` pass with zero failures across all three target frameworks.
- Each recognized view kind, whether identified by name or by a specialized supertype, routes to
  its corresponding strategy.
- A view matching no recognized kind routes to the general view strategy.
- A view declaring `render asTreeDiagram;`, `render asInterconnectionDiagram;`,
  `render asGeneralDiagram;`, `render asStateTransitionDiagram;`, `render asActionFlowDiagram;`,
  `render asSequenceDiagram;`, or `render asGridDiagram;` routes to the corresponding strategy,
  taking precedence over a conflicting name/supertype heuristic match.
- A view declaring any other render target — `asElementTable`, `asTextualNotation`, an
  unrecognized name, or none — falls through unchanged to the name/supertype heuristic, with no
  diagnostic.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `GetStrategy_InterconnectionNamedView_ReturnsInterconnectionStrategy` | Interconnection by name |
| `GetStrategy_ViewSpecializingInterconnection_ReturnsInterconnectionStrategy` | Interconnection by supertype |
| `GetStrategy_StateTransitionNamedView_ReturnsStateStrategy` | State transition view |
| `GetStrategy_ActionFlowNamedView_ReturnsActionFlowStrategy` | Action flow view |
| `GetStrategy_MatrixNamedView_ReturnsGridStrategy` | Grid/matrix view |
| `GetStrategy_BrowserNamedView_ReturnsBrowserStrategy` | Browser/tree view |
| `GetStrategy_SequenceNamedView_ReturnsSequenceStrategy` | Sequence view |
| `GetStrategy_PlainView_ReturnsGeneralViewStrategy` | Unrecognized view falls back to general |
| `GetStrategy_RenderAsTreeDiagram_ReturnsBrowserStrategy` | `render asTreeDiagram;` selects browser strategy |
| `GetStrategy_RenderAsInterconnectionDiagram_ReturnsInterconnectionStrategy` | render picks interconnection |
| `GetStrategy_RenderTargetPrecedenceOverridesNameHeuristic` | Render target wins over a conflicting name heuristic |
| `GetStrategy_RenderAsGeneralDiagram_ReturnsGeneralViewStrategy` | `asGeneralDiagram;` selects general strategy |
| `GetStrategy_RenderAsStateTransitionDiagram_ReturnsStateTransitionStrategy` | selects state strategy |
| `GetStrategy_RenderAsActionFlowDiagram_ReturnsActionFlowStrategy` | `asActionFlowDiagram;` selects action-flow |
| `GetStrategy_RenderAsSequenceDiagram_ReturnsSequenceStrategy` | `asSequenceDiagram;` selects sequence strategy |
| `GetStrategy_RenderAsGridDiagram_ReturnsGridStrategy` | `render asGridDiagram;` selects grid strategy |
| `GetStrategy_RenderAsGeneralDiagram_PrecedenceOverridesNameHeuristic` | New token wins over name heuristic |
| `GetStrategy_RenderAsElementTable_FallsThroughUnchanged` | `render asElementTable;` has no effect |
| `GetStrategy_RenderAsTextualNotation_FallsThroughUnchanged` | `render asTextualNotation;` has no effect |
| `GetStrategy_UnrecognizedRenderTarget_FallsThroughUnchanged` | Unrecognized render target has no effect |
