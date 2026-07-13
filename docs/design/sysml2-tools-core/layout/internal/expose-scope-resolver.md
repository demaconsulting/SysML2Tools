#### ExposeScopeResolver

##### Purpose

`ExposeScopeResolver` is the single shared helper resolving the qualified-name containment-subtree
scope a view's `expose` statements restrict a diagram to. Every `ILayoutStrategy` implementation
(`GeneralViewLayoutStrategy`, `GridViewLayoutStrategy`, `BrowserViewLayoutStrategy`,
`InterconnectionViewLayoutStrategy`, `StateTransitionViewLayoutStrategy`,
`ActionFlowViewLayoutStrategy`, and `SequenceViewLayoutStrategy`) calls into this unit instead of
each maintaining its own copy, so every view kind honors `expose` scoping identically.

##### Data Model

`ExposeScopeResolver` is a static class with no instance state; input arrives through method
parameters, and (Phase 2a) resolution output is returned as two internal record types:

- `ExposedScope(IReadOnlyList<string> PrefixSubjects, IReadOnlyList<string> ExplicitMembers)` —
  the resolved scope. `PrefixSubjects` are exposed subject qualified names whose entire
  containment subtree is in scope (the pre-Phase-2a whole-subtree behavior, used for `expose`
  entries with no bracket filter and as the fallback for a bracket-filtered entry that failed to
  parse or evaluate). `ExplicitMembers` are individual qualified names — of a `SysmlDefinitionNode`
  **or a named `SysmlFeatureNode`** (Phase 2d — widened from definitions-only, see below) —
  matched by a successfully-evaluated bracket filter — exact matches only, not their own nested
  members unless those also match. A settable `Failures` init-property (default empty) carries any
  bracket filters that failed to parse or evaluate.
- `BracketFilterFailure(string ExpressionText, string? Reason)` — one failed bracket-filter
  expression's raw source text plus a short human-readable reason, feeding
  `LayoutWarnings.ForUnevaluatedExposeBracketFilter`.

##### Key Methods

###### `ResolveExposedScope(SysmlWorkspace workspace, SysmlViewNode? viewNode)`

Resolves the qualified-name containment-subtree scope a view's `expose` statements restrict the
diagram to — the only content-scoping mechanism a view has (`render <target>;` names a
rendering style/format, e.g. `asTreeDiagram`/`asElementTable`, per the SysML v2 grammar, never
content, so `RenderTargetName` never affects this decision). Returns `null` — meaning "render
everything", the pre-scoping behavior — whenever the view has no resolved `Expose`-kind
`ResolvedEdges` entries: covering a `null` `viewNode` (the `--auto` synthesized view, which never
carries expose/render/filter data), a view with no `expose` statement, and a view whose every
`expose` entry failed to resolve, uniformly. Otherwise, for each resolved `Expose` edge, re-pairs
the edge's target with the `ExposeMember` it originated from (see Design below) and:

- when that entry carries no bracket-filter expression, adds the target (and, for a usage target,
  its resolved type — see the usage-to-type fallback below) to `PrefixSubjects`, unchanged from
  Phase 1;
- when that entry's bracket-filter expression parses and evaluates successfully (Phase 2a), computes
  the candidate set as every `workspace.Declarations` key that is the target itself or lies in its
  containment subtree (`qn == target || qn.StartsWith(target + "::")`) *and* is a
  `SysmlDefinitionNode` **or named `SysmlFeatureNode`** (Phase 2d — widened from
  definitions-only so a metaclass-kind classification test like `@SysML::PartUsage` can match a
  usage-level candidate too) — mirroring `GeneralViewLayoutStrategy.CollectDefinitions`'s
  candidate-set restriction — evaluates with `FilterExpressionEvaluator.Evaluate` against that
  candidate set unchanged, and adds the matched subset to `ExplicitMembers`;
- when that entry's bracket-filter expression fails to parse or evaluate, falls back to
  whole-subtree inclusion (`PrefixSubjects`, same as the unfiltered case) and records a
  `BracketFilterFailure` with the raw expression text and a short reason, so the caller can
  degrade gracefully instead of silently losing the exposed path's content.

When an exposed target resolves to a `SysmlFeatureNode` (a usage, e.g.
`part myVehicle : Vehicle;`) rather than a `SysmlDefinitionNode`, the usage's own containment
subtree is typically empty — the real content lives under its type's subtree. To avoid silently
scoping to nothing, whole-subtree inclusion also resolves the usage's own `Typing`-kind
`ResolvedEdges` entry (if any) and adds that type's qualified name to `PrefixSubjects` too, so both
the usage and its type's subtree are included. This expansion only applies to whole-subtree
inclusion — a successfully-evaluated bracket filter's `ExplicitMembers` already name the exact
matched definitions/usages directly.

###### `IsInSubjectScope(string qualifiedName, ExposedScope scope)`

Returns `true` when `qualifiedName` equals one of `scope.PrefixSubjects` or lies within one of
their containment subtrees (a `"{subject}::"` prefix match, the same qualified-name-prefix idiom
`StdlibFilter.IsStdlibElement` already uses for stdlib-prefix matching), or is an exact match of
one of `scope.ExplicitMembers` (a bracket-filter-matched definition or usage). Used by every strategy to
decide whether a candidate element belongs in a scoped diagram.

###### `IsRootRelevantToScope(string candidateQualifiedName, ExposedScope scope)`

Returns `true` when `candidateQualifiedName` (a candidate single-root diagram root, e.g. the
definition `InterconnectionViewLayoutStrategy`, `StateTransitionViewLayoutStrategy`,
`ActionFlowViewLayoutStrategy`, or `SequenceViewLayoutStrategy` would otherwise pick by its own
heuristic) is related to the resolved `expose` scope in `scope`, in either containment
direction — checked against both `scope.PrefixSubjects` and `scope.ExplicitMembers`: the candidate
itself is an exposed subject or matched member, the candidate lies within an exposed subject's
containment subtree, or an exposed subject/matched member lies within the candidate's own
containment subtree (the common "expose an inner state/action/part/lifeline of the root" case).
This method identifies the *set* of scope-relevant candidates only — because SysML v2 definitions
may nest, an ancestor and one of its nested descendant definitions can both be relevant to the same
resolved scope. Disambiguating among multiple relevant candidates is delegated to
`IsMoreSpecificCandidate`, not decided by this method.

###### `IsMoreSpecificCandidate(string candidateQualifiedName, string? currentBestQualifiedName, bool currentScoreIsBetter)`

Decides, for the scoped case, whether `candidateQualifiedName` should replace the current best
scope-relevant root candidate (`currentBestQualifiedName`, or `null` when no candidate has been
selected yet). Specificity (containment depth) is compared first, via a private `CountSegments`
helper that counts the `"::"`-separated segments of a qualified name (a bare simple name with no
`"::"` separator has depth 1, not 0): because SysML v2 qualified names are built by `parent::child`
concatenation, any genuine descendant has strictly more segments than its ancestors, so the deeper
candidate always wins over a shallower one regardless of score. Each strategy's own score heuristic
(transition/connection+part/succession+action/message count) is used only as a fallback to break
ties between candidates of equal containment depth (e.g. unrelated siblings), via
`currentScoreIsBetter`. Used only by the four single-root strategies, in place of a plain score
comparison, whenever `scope` is non-null.

##### Design

`ResolveExposedScope` re-pairs each resolved `Expose` edge with the `ExposeMember` it originated
from using a forward-scanning, order-preserving heuristic: `ReferenceResolver` builds one edge per
successfully-resolved `ExposeMember`, in source order, silently skipping entries that fail to
resolve (no edge emitted for them, only a diagnostic). Since a resolved edge carries no direct back
-reference to its originating `ExposeMember`, the method walks `viewNode.ExposeMembers` alongside
`ResolvedEdges` with a single forward-only index: for each resolved edge's target (in order), it
scans forward through the remaining, not-yet-consumed `ExposeMembers` until it finds one whose
`QualifiedName` matches the edge's target — either exactly or via a `target.EndsWith("::" + qn)`
suffix heuristic (covering a partially-qualified `ExposeMember.QualifiedName` resolved to a fully
-qualified edge target) — consuming every entry it scans past, including ones that failed to
resolve. This is a best-effort heuristic: it is correct for every realistic corpus case (entries
are declared and resolved in the same order, and duplicate simple names within one view's `expose`
list are vanishingly rare), but could incorrectly assign a bracket filter to the wrong entry in a
pathological case with duplicate exposed names — an accepted, documented risk, not a defect.

##### Error Handling

None of the methods throw. A `null` `viewNode` and an empty or non-existent
`subjects`/workspace-declaration lookup are treated as ordinary "no match" or "no scope" cases. A
bracket-filter expression that fails to parse or evaluate is degraded gracefully to whole-subtree
inclusion, recorded in `ExposedScope.Failures`, and surfaced only as a warning
(`LayoutWarnings.ForUnevaluatedExposeBracketFilter`) — never as an exception or a rendering
failure.

##### Dependencies

- `SysmlWorkspace`, `SysmlViewNode`, `SysmlFeatureNode`, `SysmlDefinitionNode`, `SysmlEdge`,
  `SysmlEdgeKind`, `ExposeMember` (Semantic subsystem) — the workspace and view model read by
  `ResolveExposedScope`.
- `FilterExpressionParser.Parse` / `FilterExpressionEvaluator.Evaluate` (Filtering subsystem,
  Phase 2a) — reused unchanged to parse and evaluate each bracket-filtered `ExposeMember`'s
  expression text against its own containment-subtree candidate set.
- `StdlibFilter` (Rendering Internal subsystem) — referenced only in documentation, as the origin
  of the qualified-name-prefix idiom `IsInSubjectScope` reuses.
- `ILayoutStrategy` (Rendering subsystem) — referenced only in documentation, identifying the
  callers this unit exists to serve.

##### Callers

- `GeneralViewLayoutStrategy` calls `ResolveExposedScope` and `IsInSubjectScope` to filter its
  workspace-wide model-edge collection directly, and threads `scope?.Failures` into
  `LayoutWarnings.ForUnevaluatedExposeBracketFilter`; it has no single-root heuristic, so
  `IsRootRelevantToScope`/`IsMoreSpecificCandidate` do not apply to it.
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

##### Requirements Traceability

- `SysML2Tools-Core-Layout-Internal-GeneralViewLayoutStrategy-ExposeBracketFilterEvaluation` —
  `ResolveExposedScope`'s per-entry bracket-filter parsing/evaluation and `ExposedScope.ExplicitMembers`
- `SysML2Tools-Core-Filtering-BracketFormExposeEvaluation` — `ResolveExposedScope` reusing
  `FilterExpressionParser.Parse`/`FilterExpressionEvaluator.Evaluate` unchanged
- `SysML2Tools-Core-Layout-Internal-LayoutWarnings-UnevaluatedExposeBracketFilter` —
  `ExposedScope.Failures`/`BracketFilterFailure`
