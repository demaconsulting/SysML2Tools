#### SequenceViewLayoutStrategy

![Core Layout Internal Structure](CoreLayoutInternalView.svg)

##### Purpose

`SequenceViewLayoutStrategy` lays out a Sequence View: it renders the interaction described by a
definition's messages as a set of vertical lifelines under rounded usage headers (the keyword over
`name : Type`) and a horizontal arrow for each message labeled `name of Payload`, ordered
top-to-bottom by the events the messages connect (SysML v2.0 clause 8.2.3.12). Its single
responsibility is to turn the selected definition's participants and messages into a positioned
`LayoutTree`.

##### Data Model

The strategy is a stateless `ILayoutStrategy`. Inputs are a `ViewContext` (carrying the
`SysmlWorkspace`) and `RenderOptions` (carrying the `Theme`). It uses two private records. A
`LifelineItem` holds a lifeline's name, its header keyword (null for a message endpoint that names
no declared usage), its name line, whether the usage is a reference, and the names of the
`event occurrence` usages it declares in declaration order. A `MessageItem` holds the sender and
receiver lifeline indices, the message label, and the position of each of the message's two events
in its lifeline's event list (-1 when the endpoint names no declared event). Output is a
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
3. **Lifeline collection.** `CollectLifelines` first records every `part`, `item`, and
   `occurrence` usage the root declares, in declaration order, so a part that exchanges no message
   still has a lifeline. Its header keyword is `GeneralViewLayoutStrategy.UsageKeyword` (so a
   reference part reads `ref part`), its name line is `GeneralViewLayoutStrategy.FormatUsageNameLine`
   (`name : Type [multiplicity]`), and its events are the names of its `event occurrence` children.
   It then records, in first-appearance order, every participant that a message endpoint names (the
   first dot-separated segment of the reference, for example `client` from `client.a`) but no
   declared usage: that lifeline has no keyword and its plain name. When a scope was resolved, any
   participant whose reconstructed qualified name (`"{root.QualifiedName}::{name}"`, which matches a
   directly-nested part feature's own `QualifiedName`) fails `ExposeScopeResolver.IsInSubjectScope`
   is excluded. An index map from name to column is built alongside.
4. **Message resolution.** `ResolveMessages` maps each message's endpoints to lifeline indices and
   event positions (the second segment of the endpoint, for example `a` from `client.a`, looked up
   in the lifeline's event list), preserving declaration order and skipping messages whose
   endpoints do not resolve — including any message with an endpoint on a lifeline excluded by
   scoping, since that lifeline was never added to the index; no new edge-side logic was required.
   The label is `FormatMessageLabel`: `name of Payload`, with the name or the payload left out when
   the model declares none (`SysmlConnectionNode.PayloadText` carries the payload).
5. **Message ordering.** `OrderMessages` draws a message above another when one of its events
   precedes one of the other's events on the same lifeline (`Precedes`). A pair that the lifelines
   order in both directions carries no constraint. The messages are then sorted topologically,
   always taking the earliest-declared message that has no unplaced predecessor, and taking the
   earliest-declared remaining message when a cycle blocks every candidate. A model with no event
   data therefore keeps declaration order.
6. **Arithmetic placement.** Lifeline X is `margin + headerWidth/2 + columnIndex * pitch`, where
   `pitch` is computed by `ComputePitch` from the widest header line and from the widest message
   label per lifeline it spans (clamped to a minimum). Message Y is
   `firstMessageY + messageOrdinal * rowPitch`. The header height is
   `BoxMetrics.LifelineHeaderHeight(theme, hasKeyword)`, with the keyword height included for every
   header when any lifeline has a keyword, so the headers line up.
7. **Node emission.** Each lifeline becomes a `LayoutLifeline` with the keyword, the name line, the
   `RoundedRectangle` header shape, and a dashed `BoxAppearance` when the usage is a reference;
   each message becomes a horizontal `LayoutLine` with no source end marker and a hollow-triangle
   target end marker (`EndMarkerStyle.HollowTriangle`), carrying the message label
   as its midpoint label. A message whose sender and receiver are the same lifeline is emitted by
   `BuildSelfMessage` as a small rectangular self-loop with the same hollow-triangle head. The
   hollow-triangle target end marker matches SysML v2 sequence message notation. Proxies, found and
   lost messages, and sequence ports are not drawn.

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

##### Members Excluded from Diagrams

Root selection ignores a declaration flagged `IsExcludedFromDiagrams`, and every count and
enumeration of the root's children (`DiagramChildren`) skips the children flagged so; members built
for name resolution only are neither lifelines nor messages, nor a reason to choose their owner as the
root.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. Empty or unresolved input does
not throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutLifeline`, `LayoutLine`, `Point2D`, `EndMarkerStyle`, `LineStyle`, `BoxShape`,
  and `BoxAppearance` (`DemaConsulting.Rendering`).
- `ViewContext` (Rendering subsystem) and `RenderOptions`, `Theme`, `BoxMetrics`
  (`DemaConsulting.Rendering.Abstractions`).
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, and `SysmlConnectionNode` (Semantic
  subsystem).
- `GeneralViewLayoutStrategy` (Layout Internal subsystem) — `UsageKeyword` and `FormatUsageNameLine`
  give a lifeline header the same keyword and name line as every other usage.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectLifelines`.

##### Callers

The layout strategy registry selects `SequenceViewLayoutStrategy` when a Sequence View is
requested; it is not called directly by other units.
