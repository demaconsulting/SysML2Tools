#### SequenceViewLayoutStrategy Verification

##### Verification Approach

`SequenceViewLayoutStrategy` is verified through unit tests in `SequenceViewLayoutStrategyTests`
that build a `SysmlWorkspace` containing a definition with message connections, run `BuildLayout`,
and assert on the returned `LayoutTree`. The strategy is pure and deterministic, so no mocking is
required; real workspace and rendering-option values are constructed directly.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `SequenceViewLayoutStrategyTests` pass with zero failures across all target frameworks.
- A definition with messages yields one lifeline per participant and one line per message, ordered
  top-to-bottom by declaration order.
- A message between two lifelines is a horizontal line with an open end marker at the receiver.
- A workspace with no messages yields an empty diagram.
- A directly-nested `part` feature under a root part definition, referenced by a message
  endpoint's first dotted segment, has a `QualifiedName` matching the reconstructed
  `"{root.QualifiedName}::{lifelineName}"` form — confirming Assumption 4 of the expose-scoping
  plan holds for realistic models before it is relied upon for lifeline-level scope filtering.
- A `null` `ViewContext.ViewNode` selects the pre-scoping heuristic root and renders every
  lifeline, unchanged from before this feature — the critical `--auto`/no-expose regression guard.
- A view whose resolved `Expose` edge names a definition other than the heuristic root selects
  that definition as the root instead.
- A view whose resolved `Expose` edge names an inner lifeline of a non-heuristic-root definition
  selects that definition's own root.
- A view whose resolved `Expose` edge names a definition unrelated to any candidate root selects
  no root, producing the minimal empty canvas.
- A view whose resolved `Expose` edge names a single lifeline narrows the diagram to that lifeline
  (plus any lifeline still reachable via a surviving message), dropping messages whose other
  endpoint was excluded.
- A view with an `expose` statement naming two separate lifelines unions both, keeping every
  message between them.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still resolves to
  the usage's type as the root, via the shared usage-to-type fallback.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `SequenceView_BuildLayout_Messages_ProducesLifelinesAndOrderedLines` | Lifeline per participant; ordered top-down |
| `SequenceView_BuildLayout_Message_IsHorizontalBetweenLifelines` | Horizontal line, open end marker at receiver |
| `SequenceView_BuildLayout_NoMessages_ReturnsMinimalCanvas` | Workspace with no messages yields no nodes |
| `SequenceView_BuildLayout_MessageArrow_HasOpenArrowhead` | Open end marker at receiver end |
| `SequenceView_LifelineQualifiedNameReconstruction_MatchesDeclaredFeature` | Reconstructed name matches feature |
| `SequenceView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged` | Null `ViewNode` renders unchanged |
| `SequenceView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot` | Non-heuristic root is selected |
| `SequenceView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot` | Inner lifeline selects root |
| `SequenceView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas` | Unrelated def |
| `SequenceView_BuildLayout_ExposeSingleLifeline_NarrowsLifelines` | Single exposed lifeline narrows the diagram |
| `SequenceView_BuildLayout_ExposeBothLifelines_UnionsSubtreesKeepsMessages` | Two lifelines union, keeping messages |
| `SequenceView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot` | Usage resolves via `Typing` to root |
