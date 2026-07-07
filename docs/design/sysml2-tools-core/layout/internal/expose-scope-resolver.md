#### ExposeScopeResolver

##### Purpose

`ExposeScopeResolver` is the single shared helper resolving the qualified-name containment-subtree
scope a view's `expose` statements restrict a diagram to. Every `ILayoutStrategy` implementation
(`GeneralViewLayoutStrategy`, `GridViewLayoutStrategy`, `BrowserViewLayoutStrategy`,
`InterconnectionViewLayoutStrategy`, `StateTransitionViewLayoutStrategy`,
`ActionFlowViewLayoutStrategy`, and `SequenceViewLayoutStrategy`) calls into this unit instead of
each maintaining its own copy, so every view kind honors `expose` scoping identically.

##### Data Model

`ExposeScopeResolver` is a static class with no instance state; all input arrives through method
parameters. It has no private records or fields — every method is a pure function over a
`SysmlWorkspace` and/or a list of qualified-name "subjects".

##### Key Methods

###### `ResolveExposedScope(SysmlWorkspace workspace, SysmlViewNode? viewNode)`

Resolves the qualified-name containment-subtree scope a view's `expose` statements restrict the
diagram to — the only content-scoping mechanism a view has (`render <target>;` names a
rendering style/format, e.g. `asTreeDiagram`/`asElementTable`, per the SysML v2 grammar, never
content, so `RenderTargetName` never affects this decision). Returns `null` — meaning "render
everything", the pre-scoping behavior — whenever the view has no resolved `Expose`-kind
`ResolvedEdges` entries: covering a `null` `viewNode` (the `--auto` synthesized view, which never
carries expose/render/filter data), a view with no `expose` statement, and a view whose every
`expose` entry failed to resolve, uniformly. Otherwise, for each resolved `Expose` edge's target
qualified name, adds that name to the scope; when `workspace.Declarations` resolves the target to
a `SysmlFeatureNode` (a usage, e.g. `part myVehicle : Vehicle;`) rather than a
`SysmlDefinitionNode`, additionally resolves the usage's own `Typing`-kind `ResolvedEdges` entry
(if any) and adds *that* type's qualified name to the scope too — the usage-to-type fallback for
the containment gap where a usage's own (typically empty) subtree would otherwise silently
produce zero content.

###### `IsInSubjectScope(string qualifiedName, IReadOnlyList<string> subjects)`

Returns `true` when `qualifiedName` equals one of `subjects` or lies within one of their
containment subtrees (a `"{subject}::"` prefix match) — the same qualified-name-prefix idiom
`StdlibFilter.IsStdlibElement` already uses for stdlib-prefix matching. Used by every strategy to
decide whether a candidate element belongs in a scoped diagram.

###### `IsRootRelevantToScope(string candidateQualifiedName, IReadOnlyList<string> subjects)`

Returns `true` when `candidateQualifiedName` (a candidate single-root diagram root, e.g. the
definition `InterconnectionViewLayoutStrategy`, `StateTransitionViewLayoutStrategy`,
`ActionFlowViewLayoutStrategy`, or `SequenceViewLayoutStrategy` would otherwise pick by its own
heuristic) is related to the resolved `expose` scope in `subjects`, in either containment
direction: the candidate itself is an exposed subject, the candidate lies within an exposed
subject's containment subtree, or an exposed subject lies within the candidate's own containment
subtree (the common "expose an inner state/action/part/lifeline of the root" case). This method
identifies the *set* of scope-relevant candidates only — because SysML v2 definitions may nest, an
ancestor and one of its nested descendant definitions can both be relevant to the same resolved
scope. Disambiguating among multiple relevant candidates is delegated to
`IsMoreSpecificCandidate`, not decided by this method.

###### `IsMoreSpecificCandidate(string candidateQualifiedName, string? currentBestQualifiedName, bool currentScoreIsBetter)`

Decides, for the scoped case, whether `candidateQualifiedName` should replace the current best
scope-relevant root candidate (`currentBestQualifiedName`, or `null` when no candidate has been
selected yet). Specificity (containment depth) is compared first: because SysML v2 qualified names
are built by `parent::child` concatenation, any genuine descendant's qualified name is strictly
longer than its ancestors', so a longer qualified name is a safe, cheap proxy for "more deeply
nested" and always wins over a shorter one regardless of score. Each strategy's own score heuristic
(transition/connection+part/succession+action/message count) is used only as a fallback to break
ties between candidates of equal qualified-name length (e.g. unrelated siblings), via
`currentScoreIsBetter`. Used only by the four single-root strategies, in place of a plain score
comparison, whenever `scope` is non-null.

##### Error Handling

N/A — none of the four methods validate arguments or throw; a `null` `viewNode` and an empty or
non-existent `subjects`/workspace-declaration lookup are all treated as ordinary "no match" or
"no scope" cases, not error conditions.

##### Dependencies

- `SysmlWorkspace`, `SysmlViewNode`, `SysmlFeatureNode`, `SysmlDefinitionNode`, `SysmlEdge`,
  `SysmlEdgeKind` (Semantic subsystem) — the workspace and view model read by
  `ResolveExposedScope`.
- `StdlibFilter` (Rendering Internal subsystem) — referenced only in documentation, as the origin
  of the qualified-name-prefix idiom `IsInSubjectScope` reuses.
- `ILayoutStrategy` (Rendering subsystem) — referenced only in documentation, identifying the
  callers this unit exists to serve.

##### Callers

- `GeneralViewLayoutStrategy` calls all three scope-membership methods (moved here verbatim from
  its own former private copies of `ResolveExposedScope` and `IsInSubjectScope`).
- `GridViewLayoutStrategy` and `BrowserViewLayoutStrategy` call `ResolveExposedScope` and
  `IsInSubjectScope` to filter their workspace-wide definition/tree-node collections directly (no
  single-root heuristic, so `IsRootRelevantToScope`/`IsMoreSpecificCandidate` do not apply).
- `InterconnectionViewLayoutStrategy`, `StateTransitionViewLayoutStrategy`,
  `ActionFlowViewLayoutStrategy`, and `SequenceViewLayoutStrategy` call `ResolveExposedScope` to
  compute the scope, `IsRootRelevantToScope` to restrict their heuristic root selection to
  candidates relevant to that scope, `IsMoreSpecificCandidate` to break ties among multiple
  relevant candidates by specificity (falling back to their own score only among equally specific
  candidates), and `IsInSubjectScope` to filter the child elements (parts, states, actions,
  lifelines) collected from the selected root.
