### Layout Internal Subsystem Verification

#### Verification Approach

The Internal subsystem is verified through unit tests, one test class per view layout strategy,
that construct a synthetic `SysmlWorkspace`, invoke `BuildLayout`, and assert on the returned
`LayoutTree`. The tests inspect the node tree for the expected boxes, ports, lines, badges, and
canvas dimensions. No mocking is required: the strategies depend only on the in-memory semantic
model, the geometric engines, and the theme, all of which the tests construct directly.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All view layout strategy test classes pass with zero failures across all three target frameworks.
- Each strategy produces a layout tree whose nodes match the elements of its synthetic input.
- A workspace with no relevant elements yields a minimal empty canvas with no nodes.
- Standard-library elements are absent from the produced diagrams.
- Boxes within a diagram do not overlap one another.
- A layout-quality problem such as a connector crossing a box surfaces a non-fatal warning
  naming the affected view, while a clean layout produces no warning.

#### Test Scenarios

| Scenario | Strategy | Assertion |
| --- | --- | --- |
| Definitions rendered with keywords | `GeneralViewLayoutStrategy` | Each definition becomes a keyword-carrying box |
| Standard-library elements excluded | `GeneralViewLayoutStrategy` | Stdlib-only input yields a minimal empty canvas |
| Parts, ports, and connectors | `InterconnectionViewLayoutStrategy` | Container box, parts, ports, connection lines |
| Non-overlapping part boxes | `InterconnectionViewLayoutStrategy` | No two part boxes overlap |
| States, marker, and transitions | `StateTransitionViewLayoutStrategy` | State boxes, initial badge, guard lines |
| Actions, markers, and flows | `ActionFlowViewLayoutStrategy` | Action boxes, start/done markers, and flow lines |
| Lifelines and ordered messages | `SequenceViewLayoutStrategy` | One lifeline per participant, ordered message lines |
| Specialization matrix | `GridViewLayoutStrategy` | Header row/column of names, marks at related cells |
| Indented membership tree | `BrowserViewLayoutStrategy` | Nested elements indented beyond their parents |
| Layout-quality warning | `LayoutWarnings` | Crossing connectors surface a view-named warning; none when clean |
| Empty workspace | All strategies | A minimal empty canvas with no nodes |
