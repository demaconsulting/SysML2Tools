#### DynamicViewSynthesizer Verification

##### Verification Approach

`DynamicViewSynthesizer` is verified through unit tests in `DynamicViewSynthesizerTests` that
call `DiagramRenderer.SynthesizeDynamicView` (the public entry point delegating to
`DynamicViewSynthesizer.Synthesize`) against hand-built `SysmlWorkspace` fixtures covering: the
happy path for each of the seven `--view-type` kinds, an unresolved target, each wrong-kind
target rejection, a standard-library target rejection, each per-kind compatibility pre-check
failure, filter-expression pass-through (including the null case), and the name-collision
diagnostic. No mocking is required; the unit is pure and deterministic.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `DynamicViewSynthesizerTests` pass with zero failures across all three target frameworks.
- Each of the seven `--view-type` kinds synthesizes successfully against a compatible target,
  with `RenderTargetName` set to the correct token.
- An unrecognized `--view-type`, an unresolved `--view-target`, a wrong-kind target (view,
  transition), and a standard-library target each report a diagnostic and return a null view
  node.
- Each per-kind structural compatibility pre-check rejects an incompatible target with a
  specific diagnostic (interconnection: not-a-part-def, no-nested-parts; state: no-transitions;
  action: no-actions-or-successions; sequence: no-messages), and accepts a target satisfying an
  alternate condition (action: succession-only, with no `action` feature).
- `--filter` text is passed through unchanged to the synthesized node's `FilterExpressionText`,
  including remaining `null` when no filter was supplied.
- A pre-existing declaration whose qualified name collides with the synthesized `$`-prefixed name
  is reported as a diagnostic rather than silently overwritten.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Synthesize_GeneralKind_ResolvableTarget_Succeeds` | General view synthesizes; edges/members correct |
| `Synthesize_GridKind_ResolvableTarget_Succeeds` | Grid view synthesizes |
| `Synthesize_BrowserKind_ResolvableTarget_Succeeds` | Browser view synthesizes |
| `Synthesize_InterconnectionKind_PartDefWithNestedPart_Succeeds` | Interconnection view synthesizes |
| `Synthesize_InterconnectionKind_NotPartDef_Fails` | Non-"part def" target rejected |
| `Synthesize_InterconnectionKind_NoNestedParts_Fails` | Part def with no nested parts rejected |
| `Synthesize_StateKind_HasTransition_Succeeds` | State view synthesizes |
| `Synthesize_StateKind_NoTransitions_Fails` | No transitions rejected |
| `Synthesize_ActionKind_HasActionFeature_Succeeds` | Action view synthesizes via action feature |
| `Synthesize_ActionKind_HasSuccession_Succeeds` | Action view synthesizes via succession |
| `Synthesize_ActionKind_NoActionsOrSuccessions_Fails` | Neither present rejected |
| `Synthesize_SequenceKind_HasMessage_Succeeds` | Sequence view synthesizes |
| `Synthesize_SequenceKind_NoMessages_Fails` | No messages rejected (documented pre-check gap) |
| `Synthesize_UnrecognizedViewType_Fails` | Unknown `--view-type` rejected |
| `Synthesize_UnresolvedTarget_Fails` | Unresolved `--view-target` rejected |
| `Synthesize_TargetIsView_Fails` | View-kind target rejected |
| `Synthesize_TargetIsTransition_Fails` | Transition-kind target rejected |
| `Synthesize_TargetIsStdlib_Fails` | Standard-library target rejected |
| `Synthesize_FilterExpression_PassedThroughUnchanged` | Filter text passed through |
| `Synthesize_NoFilterExpression_ResultsInNullFilterExpressionText` | Null filter stays null |
| `Synthesize_NameCollision_Fails` | Pre-existing `$`-prefixed name collision rejected |
