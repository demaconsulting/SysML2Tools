#### SequenceViewLayoutStrategy

##### Purpose

`SequenceViewLayoutStrategy` lays out a Sequence View: it renders the interaction described by a
definition's messages as a set of vertical lifelines with header boxes and a horizontal arrow for
each message, ordered top-to-bottom by declaration order. Its single responsibility is to turn the
selected definition's messages into a positioned `LayoutTree`.

##### Data Model

The strategy is a stateless `ILayoutStrategy`. Inputs are a `ViewContext` (carrying the
`SysmlWorkspace`) and `RenderOptions` (carrying the `Theme`). It uses a private `MessageItem`
record holding the sender and receiver lifeline indices and the message label. Output is a
`LayoutTree` whose nodes are `LayoutLifeline` headers/stems and `LayoutLine` message arrows.

##### Key Methods

###### `BuildLayout(context, options)`

Builds the diagram:

1. **Scope resolution.** `ExposeScopeResolver.ResolveExposedScope` resolves the view's `expose`
   scope once (or `null` when none applies).
2. **Root selection.** `FindRoot` scans the non-stdlib declarations and chooses the definition that
   declares the most `message` connections, so the most message-rich definition drives the view —
   restricted, when a scope was resolved, to candidates for which
   `ExposeScopeResolver.IsRootRelevantToScope` returns `true`. When multiple candidates are
   relevant to a non-null scope (possible because a nested definition and its ancestor can both be
   relevant), the most specific (deepest/longest qualified name) relevant candidate is preferred via
   `ExposeScopeResolver.IsMoreSpecificCandidate`, with the message-count tie-break used only to
   break ties among equally specific candidates; this ordering does not apply when `scope` is
   `null`.
3. **Lifeline collection.** `CollectLifelines` walks the root's messages and records the distinct
   participants in first-appearance order, where a participant is the first dot-separated segment of
   a message endpoint reference (for example `client` from `client.a`) — excluding, when a scope was
   resolved, any participant whose reconstructed qualified name (`"{root.QualifiedName}::{name}"`,
   which matches a directly-nested part feature's own `QualifiedName`) fails
   `ExposeScopeResolver.IsInSubjectScope`. An index map from name to column is built alongside.
4. **Message resolution.** `ResolveMessages` maps each message's endpoints to lifeline indices,
   preserving declaration order and skipping messages whose endpoints do not resolve — including any
   message with an endpoint on a lifeline excluded by scoping, since that lifeline was never added to
   the index; no new edge-side logic was required.
5. **Arithmetic placement.** Lifeline X is `margin + headerWidth/2 + columnIndex * pitch`, where
   `pitch` is computed by `ComputePitch` from the widest label (clamped to a minimum). Message Y is
   `firstMessageY + messageOrdinal * rowPitch`. Header height and margins derive from the theme.
6. **Node emission.** Each lifeline becomes a `LayoutLifeline`; each message becomes a horizontal
   `LayoutLine` with no source end marker and an open target end marker, carrying the message label
   as its midpoint label. A message whose sender and receiver are the same lifeline is emitted by
   `BuildSelfMessage` as a small rectangular self-loop. The open target end marker matches SysML v2
   sequence message notation.

When no root is found (including when scoping excludes every heuristic candidate), or there are no
lifelines or messages (including when scoping excludes every message's remaining endpoint), a
minimal empty `LayoutTree` with no nodes is returned.

##### Expose Scoping

Because this strategy renders exactly one selected root's lifelines, scoping restricts **which
root is selected** and then narrows **which of that root's lifelines are shown**, mirroring the
other single-root strategies' approach. `FindRoot` only considers candidates
`ExposeScopeResolver.IsRootRelevantToScope` accepts, so exposing the current heuristic root
itself, an inner lifeline of it, or a definition that itself contains the heuristic default all
correctly select a root, while exposing an unrelated definition yields no root and thus the
minimal empty canvas. When more than one candidate is relevant (a nested definition and an
ancestor definition can both be relevant to the same exposed subject),
`ExposeScopeResolver.IsMoreSpecificCandidate` prefers the most deeply nested candidate, so
exposing an inner lifeline participant of a nested definition correctly selects that nested
definition rather than its ancestor, even when the ancestor has more messages. `CollectLifelines`
then narrows the selected root's lifelines by reconstructing each candidate participant's
qualified name as `"{root.QualifiedName}::{name}"` and testing it with
`ExposeScopeResolver.IsInSubjectScope`; this reconstruction was confirmed reliable for realistic
models — a directly-nested `part` feature under a root part definition, referenced by a message
endpoint's first dotted segment, has exactly this `QualifiedName` — by a dedicated test
(`SequenceView_LifelineQualifiedNameReconstruction_MatchesDeclaredFeature`) mirroring the real
`client-server-sequence.sysml` fixture, so no conservative fallback (restricting only root
selection) was needed. `ResolveMessages`'s existing "skip a message whose endpoint did not resolve"
behavior transparently drops any message touching an excluded lifeline — no new edge-side logic was
required. A view with no `expose` statement (including the synthesized `--auto` view, whose
`ViewNode` is `null`) resolves no scope, so `FindRoot` considers every candidate and
`CollectLifelines` keeps every lifeline, unchanged from the pre-scoping behavior.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. Empty or unresolved input does
not throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutLifeline`, `LayoutLine`, `Point2D`, `EndMarkerStyle`, and `LineStyle`
  (`DemaConsulting.Rendering`).
- `ViewContext` (Rendering subsystem) and `RenderOptions`, `Theme`
  (`DemaConsulting.Rendering.Abstractions`).
- `SysmlWorkspace`, `SysmlDefinitionNode`, and `SysmlConnectionNode` (Semantic subsystem).
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectLifelines`.

##### Callers

The layout strategy registry selects `SequenceViewLayoutStrategy` when a Sequence View is
requested; it is not called directly by other units.
