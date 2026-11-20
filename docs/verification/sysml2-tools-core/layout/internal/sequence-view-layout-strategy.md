#### SequenceViewLayoutStrategy Verification

##### Verification Approach

`SequenceViewLayoutStrategy` is verified through unit tests in `SequenceViewLayoutStrategyTests`
that build a `SysmlWorkspace` containing a definition with message connections, run `BuildLayout`,
and assert on the returned `LayoutTree`. The strategy is pure and deterministic, so no mocking is
required; real workspace and rendering-option values are constructed directly.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `SequenceViewLayoutStrategyTests` pass with zero failures across all target frameworks.
- A definition with messages yields one lifeline per participant and one line per message, ordered
  top-to-bottom by the events the messages connect, and by declaration order otherwise.
- A lifeline declared by a part, item, or occurrence usage has a rounded header with the usage
  keyword (`ref part` for a reference part, whose header is dashed) over the name line
  `name : Type`; a part that exchanges no message still has a lifeline, after the declared ones come
  the undeclared message endpoints, with a plain name and no keyword.
- A message is labeled `name of Payload`, with the name or payload left out when absent.
- Messages declared out of order are drawn in event order, transitively, and keep declaration order
  when the events order them contradictorily or give no ordering.
- A message label wider than the default lifeline spacing widens the spacing so the label fits.
- A message between two lifelines is a horizontal line with a hollow-triangle end marker at the
  receiver; a message from a lifeline to itself (a self-loop) carries the same hollow-triangle head.
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
  (plus any lifeline still reachable via a surviving message), dropping any message that
  references the excluded lifeline (both endpoints excluded, or one excluded endpoint) while a
  message between two lifelines that remain in scope — including a self-message on the retained
  lifeline — is still drawn.
- A view with an `expose` statement naming two separate lifelines unions both, keeping every
  message between them.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still resolves to
  the usage's type as the root, via the shared usage-to-type fallback.
- A view whose resolved `Expose` edge names an inner lifeline participant of a definition
  genuinely nested inside another eligible root candidate selects the nested definition, not the
  ancestor, even though the ancestor has more messages and would win the old pure-score tie-break.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `SequenceView_BuildLayout_Messages_ProducesLifelinesAndOrderedLines` | Lifeline per participant; ordered top-down |
| `SequenceView_BuildLayout_Lifeline_HasRoundedHeaderKeywordAndTypedLabel` | Rounded header, `part`, `name : Type` |
| `SequenceView_BuildLayout_ReferencePartLifeline_HasDashedHeader` | `ref part` keyword, dashed header |
| `SequenceView_BuildLayout_ItemAndOccurrenceUsages_AreLifelines` | Item and occurrence lifelines |
| `SequenceView_BuildLayout_DeclaredPartWithoutMessages_GetsLifeline` | Silent part still has a lifeline |
| `SequenceView_BuildLayout_UndeclaredEndpoint_FallsBackToFirstAppearance` | Undeclared endpoint: plain lifeline |
| `SequenceView_BuildLayout_MessageWithPayload_LabelIsNameOfPayload` | `name of Payload` forms |
| `SequenceView_BuildLayout_MessagesDeclaredOutOfEventOrder_AreOrderedByEvents` | Event order beats declaration |
| `SequenceView_BuildLayout_MessagesChained_AreOrderedTransitivelyAndStably` | Chained events; stable order |
| `SequenceView_BuildLayout_NoEventData_KeepsDeclarationOrder` | Declaration order kept |
| `SequenceView_BuildLayout_ContradictoryEventOrders_FallBackToDeclarationOrder` | Contradiction keeps declaration |
| `SequenceView_BuildLayout_LongMessageLabel_WidensLifelinePitch` | Long label widens pitch |
| `SequenceView_BuildLayout_Message_IsHorizontalBetweenLifelines` | Horizontal line, hollow-triangle head at receiver |
| `SequenceView_BuildLayout_NoMessages_ReturnsMinimalCanvas` | Workspace with no messages yields no nodes |
| `SequenceView_BuildLayout_MessageArrow_HasHollowTriangleHead` | Hollow-triangle end marker at receiver end |
| `SequenceView_BuildLayout_SelfMessage_HasHollowTriangleHead` | Self-message loop ends in a hollow-triangle head |
| `SequenceView_LifelineQualifiedNameReconstruction_MatchesDeclaredFeature` | Reconstructed name matches feature |
| `SequenceView_BuildLayout_NullViewNode_PicksHeuristicRootUnchanged` | Null `ViewNode` renders unchanged |
| `SequenceView_BuildLayout_ExposeNonHeuristicRoot_SelectsExposedRoot` | Non-heuristic root is selected |
| `SequenceView_BuildLayout_ExposeInnerChildOfNonHeuristicRoot_SelectsItsRoot` | Inner lifeline selects root |
| `SequenceView_BuildLayout_ExposeUnrelatedDefinition_NoRootSelected_ReturnsMinimalCanvas` | Unrelated def |
| `SequenceView_BuildLayout_ExposeSingleLifeline_NarrowsLifelines` | Drops `req`/`resp`; keeps `self` message |
| `SequenceView_BuildLayout_ExposeBothLifelines_UnionsSubtreesKeepsMessages` | Two lifelines union, keep messages |
| `SequenceView_BuildLayout_ExposedUsage_ResolvesThroughTypingToRoot` | Usage resolves via `Typing` to root |
| `SequenceView_BuildLayout_ExposeInnerLifelineOfNestedDefinition_SelectsNestedDefinitionNotAncestor` | Nested wins |
| `SequenceView_BuildLayout_ExcludedMessagesAndLifelines_RenderIdenticallyToWorkspaceWithout` | Hidden not drawn |
