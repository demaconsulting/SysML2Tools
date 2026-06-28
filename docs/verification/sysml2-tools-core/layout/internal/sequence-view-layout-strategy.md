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
- A message between two lifelines is a horizontal line with an open arrowhead at the receiver.
- A workspace with no messages yields an empty diagram.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `SequenceView_BuildLayout_Messages_ProducesLifelinesAndOrderedLines` | Lifeline per participant; ordered top-down |
| `SequenceView_BuildLayout_Message_IsHorizontalBetweenLifelines` | Horizontal line, open arrowhead at receiver |
| `SequenceView_BuildLayout_NoMessages_ReturnsMinimalCanvas` | Workspace with no messages yields no nodes |
| `SequenceView_BuildLayout_MessageArrow_HasOpenArrowhead` | Open arrowhead at receiver end |
