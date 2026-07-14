#### DynamicViewSynthesizer

##### Purpose

`DynamicViewSynthesizer` builds an in-memory `SysmlViewNode` targeting any resolvable,
non-stdlib element in a loaded workspace, without requiring the user to add a `view def` to the
model — the engine behind the `render --view-type <kind> --view-target <qualified-name>
[--filter <expr>]` CLI feature ("dynamic" or "ad-hoc" views). It is the unit that lets a user
diagram a single element by name, on demand, without editing the SysML model first.

##### Data Model

`DynamicViewSynthesizer` is a static class with no instance state. Its input is the loaded
`SysmlWorkspace`, the requested `--view-type` string, the `--view-target` qualified name, and an
optional `--filter` expression text. Its output is a `(SysmlViewNode? ViewNode, string?
Diagnostic)` tuple: exactly one of the two is non-null.

##### Key Methods

###### `Synthesize(workspace, viewType, targetQualifiedName, filterExpressionText)`

Performs four steps, in order, short-circuiting to a diagnostic on the first failure:

1. **View-type mapping.** Maps `viewType` to the `SysmlViewNode.RenderTargetName` token
   `DiagramTypeRouter` dispatches on: `general` → `asGeneralDiagram`, `interconnection` →
   `asInterconnectionDiagram`, `state` → `asStateTransitionDiagram`, `action` →
   `asActionFlowDiagram`, `sequence` → `asSequenceDiagram`, `grid` → `asGridDiagram`, `browser` →
   `asTreeDiagram`. An unrecognized value reports a diagnostic listing the valid values.
2. **Target resolution.** Looks up `targetQualifiedName` in `workspace.Declarations`. An
   unresolved name reports a "not found" diagnostic. A resolved node of kind
   `SysmlViewNode`/`SysmlViewpointNode`/`SysmlImportNode`/`SysmlMetadataNode`/
   `SysmlTransitionNode`/`SysmlConnectionNode` is rejected — these kinds cannot serve as a
   dynamic view's rendered content — with a diagnostic naming the offending kind. A resolved
   standard-library element (per `StdlibFilter.IsStdlibElement` against `workspace.StdlibNames`)
   is likewise rejected.
3. **Compatibility pre-check.** Runs a cheap, necessary-but-not-sufficient structural
   compatibility check against the target's own `Children`, mirroring each layout strategy's own
   root-selection gate:
   - `general`/`grid`/`browser`: no precondition — any resolvable, non-stdlib definition or usage
     is accepted, matching `GeneralViewLayoutStrategy`/`GridViewLayoutStrategy`/
     `BrowserViewLayoutStrategy`'s own unconditional acceptance.
   - `interconnection`: the target must be a `SysmlDefinitionNode` with
     `DefinitionKeyword == "part def"` and at least one nested `SysmlFeatureNode` with
     `FeatureKeyword == "part"`, mirroring `InterconnectionViewLayoutStrategy`'s two-part
     `FindRoot`/post-`FindRoot` gate.
   - `state`: at least one nested `SysmlTransitionNode` (a transition) or at least one nested
     `SysmlFeatureNode` with `FeatureKeyword == "state"`, mirroring
     `StateTransitionViewLayoutStrategy`'s `CollectStates` gate (`states.Count == 0`).
   - `action`: at least one nested `SysmlTransitionNode` (a succession) or at least one nested
     `SysmlFeatureNode` with `FeatureKeyword == "action"`, mirroring
     `ActionFlowViewLayoutStrategy`.
   - `sequence`: at least one nested `SysmlConnectionNode` with `ConnectionKeyword == "message"`.

     > **Known limitation.** The AST has no dedicated "lifeline" node —
     > `SequenceViewLayoutStrategy.CollectLifelines` derives lifelines purely from each
     > `message` usage's endpoint references — so this check approximates "at least one
     > lifeline" as "at least one nested `message` usage". This is necessary (zero messages
     > guarantees zero lifelines) but **not sufficient**: a target whose message endpoints fail
     > to resolve to any lifeline still passes this pre-check yet still renders the canonical
     > near-blank `LayoutTree` sentinel. A full message-edge-walk validation was deliberately
     > **not** implemented (see ROADMAP.md); this gap is documented here, in this unit's XML doc
     > comments, and in `docs/user_guide/introduction.md`.

   A failing pre-check reports a diagnostic explaining which structural condition was not met.
4. **Node construction.** Builds a `SysmlViewNode` with:
   - `Name`/`QualifiedName` prefixed with a leading `$` (a character illegal in a SysML
     identifier), guaranteeing no collision with any real, parsed declaration. `Synthesize`
     defensively checks `workspace.Declarations` for the synthesized qualified name first and
     reports an "already exists" diagnostic rather than silently overwriting an existing entry on
     the rare case where it is already present (e.g., a repeat `Synthesize` call against the same
     workspace instance for the same target).
   - `RenderTargetName` set to the resolved token from step 1.
   - A single-entry `ExposeMembers` list (`[new ExposeMember(targetQualifiedName, null,
     ExposeRecursionKind.MembershipRecursive)]`) and
     matching single-entry `ResolvedEdges` list (`[new SysmlEdge(viewQualifiedName,
     targetQualifiedName, SysmlEdgeKind.Expose)]`) — the same two properties a real, parsed
     `view def V { expose Target::**; }` produces via `ReferenceResolver`, so
     `ExposeScopeResolver.ResolveExposedScope` (which reads only these two properties, with no
     notion of provenance) scopes the rendered diagram to the requested target's whole
     containment subtree (matching the pre-fix behavior for dynamic/ad-hoc views, which are
     intended to show the full context around a requested element rather than the element
     alone). This is the key difference from `DiagramRenderer.SynthesizeAutoView`, whose synthesized node carries
     no `ResolvedEdges` at all — that absence is what makes `ExposeScopeResolver` return a
     `null` scope (render everything); a dynamic view instead always resolves to a definite,
     non-null scope.
   - `FilterExpressionText` set to `filterExpressionText` unchanged (including `null`).

##### Error Handling

Every failure path returns a `(null, string)` tuple with a specific, human-readable diagnostic —
never an exception — for: unrecognized `--view-type`, unresolved `--view-target`, a wrong-kind
target, a standard-library target, a failed per-kind compatibility pre-check, and a name
collision. Argument-null checks on `workspace`/`viewType`/`targetQualifiedName` throw
`ArgumentNullException`, consistent with the rest of the Core public/internal surface.

##### Dependencies

- `DiagramTypeRouter` (indirectly, via the shared `RenderTargetName` token vocabulary).
- `SysmlWorkspace`, `SysmlNode` and its subtypes, `SysmlEdge`/`SysmlEdgeKind`, `ExposeMember`
  (Semantic subsystem).
- `StdlibFilter` (Rendering subsystem) for standard-library exclusion.

##### Callers

`DiagramRenderer.SynthesizeDynamicView`, which delegates directly to `Synthesize` and is in turn
called by `RenderCommand.TryProcessDynamicView` (Tool subsystem) to implement the
`render --view-type/--view-target/--filter` CLI flags.
