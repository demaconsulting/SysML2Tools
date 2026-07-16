#### InterconnectionViewLayoutStrategy

##### Purpose

`InterconnectionViewLayoutStrategy` implements `ILayoutStrategy` to produce an Interconnection
View diagram. It shows the internal structure of a single part definition: its nested part usages
as boxes placed through `LayeredPlacement`, ports on the box boundaries for the incident connections,
and the connection usages routed as orthogonal connector lines between the ports, all enclosed by a
container box for the host definition.

##### Data Model

`InterconnectionViewLayoutStrategy` has no instance state; all input arrives through the
`BuildLayout` parameters. Layout constants (`MinPartWidth`, `CharWidthFactor`) are declared as
`private const double` fields. Four private records carry intermediate data: `PartItem` (a nested
part usage with its computed box size, typing, and — when the part is a container — its
pre-laid-out `InnerContent`), `TopLevelPart` (pairs a top-level-fallback `PartItem` with its owning
definition's qualified name, `OwnerQualifiedName`, so `ResolveTopLevelConnections` can scope
endpoint resolution correctly per owning definition — see _No-single-root scoped fallback_ below),
`ConnPair` (a resolved binary connection between two nested-part
indices, together with the optional port-name label for each end), and `InteriorLayout` (the full
container size and content produced by laying out one definition's interior).

##### Key Methods

###### `BuildLayout(ViewContext context, RenderOptions options)`

Entry point. Resolves the view's `expose` scope via `ExposeScopeResolver.ResolveExposedScope`,
selects the root part definition via `FindRoot(workspace, scope)`, builds the container-definition
index via `BuildDefinitionIndex`, lays out the root's interior via `LayOutInterior` (applying
`scope`'s namespace-prefix filter only at the root's own direct children, depth 0; every deeper
recursive call passes `scope: null`, since a nested container's own interior's membership in the
exposed scope is never re-derived from its own qualified name — composition structure and
namespace/file organization are independent in SysML v2 — but whether that deeper interior is
expanded **at all** is gated by a per-branch `ancestorUnlimitedRecursion` flag, decided once per
depth-0 feature and threaded unchanged through every recursive call beneath it; see
_Per-Branch Depth-Limited Recursion_ below; the true root call passes `ancestorUnlimitedRecursion:
null`, since no branch decision exists yet at that point), and assembles the root container box
with the interior content nested as that box's own `Children` (mirroring the nesting `MakePartBox`
already uses for a container part, so the root box is never a bare sibling of its own content) into
the `LayoutTree`. When `FindRoot` selects no root, falls back to the **no-single-root scoped
fallback** described below when the resolved scope directly includes one or more top-level `part`
feature usages; otherwise returns a minimal 200×100 empty `LayoutTree`.

###### Per-Branch Depth-Limited Recursion (`DetermineBranchUnlimitedRecursion`, `MatchesUnlimitedSubject`)

Recursion depth is decided **once per composition branch**, not once for the whole diagram. At
depth 0 — the only point where a feature's own qualified name is checked against the resolved
scope at all (see _Cross-boundary resolution_ / `CollectParts` below) — `CollectParts` (and,
identically, each independently-scoped feature visited by `CollectTopLevelScopedParts`) computes
`ancestorUnlimitedRecursion ?? DetermineBranchUnlimitedRecursion(feature, scope)`: when an ancestor
branch decision has already been made (always `null` only at the true root), it is reused unchanged
(a branch decision, once made, is final for every descendant); otherwise a fresh decision is made
for this depth-0 feature. `DetermineBranchUnlimitedRecursion` returns `true` unconditionally when
`scope` is `null` (no `expose` statement resolved for this view, including the synthesized `--auto`
view — unchanged from before this fix); otherwise, when the feature carries a qualified name, it
delegates to `ExposeScopeResolver.MatchesUnlimitedSubject(qualifiedName, scope)` — a narrower
sibling of `IsInSubjectScope` that returns `true` only when the feature matches a subject whose
recursion kind is `ExposeRecursionKind.MembershipRecursive` or `ExposeRecursionKind.NamespaceRecursive`
(an `expose X::**;` or `expose X::*::**;` form), never via `ExplicitMembers` (which is always
exact-only, regardless of any subject's recursion kind) and never merely because some _other_,
unrelated subject in the same scope happens to be recursive. When a feature matches both a
recursive and a non-recursive subject simultaneously (e.g. two overlapping `expose` statements both
naming an ancestor of the same feature with different recursion kinds), the branch is still decided
`true` — most-permissive-wins, mirroring `IsInSubjectScope`'s own inclusive-OR semantics across
subjects. As a rare fallback — a depth-0 feature with no qualified name at all — the decision falls
back to the diagram-wide `HasUnlimitedRecursion(scope)` predicate (see below), which cannot
distinguish branches but is the best available signal when no qualified name exists to match
against a specific subject.

Once decided at depth 0 (or independently, per top-level feature, in `CollectTopLevelScopedParts`),
the resulting `bool` is threaded **unchanged** through every descendant call — `BuildPartItem`'s own
recursive `LayOutInterior` call passes its own already-decided `unlimitedRecursion` value straight
through as the child's `ancestorUnlimitedRecursion` — exactly as the pre-fix diagram-wide boolean
was threaded, except the value is now decided **per branch** rather than **once per diagram**.
Re-deriving the decision by qualified-name matching at any depth past 0 would be fundamentally
unsound: composition-path depth (how many `part` containment levels deep a box is nested inside its
rendered parent) and namespace/file-declaration depth (`::`-qualified-name nesting, what
`IsInSubjectScope`/`MatchesUnlimitedSubject` actually match against) are independent axes in
SysML v2 — a deeply nested composition part can have a namespace-shallow qualified name and vice
versa — so only the depth-0 decision (where the two axes still coincide, by construction of how
`CollectParts` is first invoked) is ever qualified-name-matched; every deeper level inherits it
verbatim.

When a branch's decision is `false`, `BuildPartItem` still includes every part reached at that
branch's depth 0 as its own node, but never recurses into a container part's own interior past that
point: a deeper container renders as an intrinsic-sized leaf box (its own `«part» name : Type` box,
with no nested children drawn), rather than always expanding fully. When a branch's decision is
`true` (including every `scope is null` case), behavior for that branch is completely unchanged
from before per-branch tracking was introduced — recursion is unconditional at every depth within
that branch.

**Supersedes the earlier global-boolean simplification.** An earlier iteration of this feature
computed a single diagram-wide `unlimitedRecursion` boolean via `HasUnlimitedRecursion(scope)` once
in `BuildLayout`, then threaded that one value unchanged through every branch of the diagram. That
approach had a known, documented limitation: a scope combining a recursive subject with a
non-recursive subject — e.g. `expose SystemDef::**; expose OtherDef;` in the same view — applied
**unlimited recursion to the entire diagram**, even to parts reached only via the non-recursive
subject, which should have stayed depth-limited to themselves. The per-branch tracking described
above resolves that limitation: each top-level branch now gets its own independently-decided
recursion flag, so `SystemDef`'s branch fully recurses while `OtherDef`'s branch stays depth-limited
to its own direct children, in the same diagram. `HasUnlimitedRecursion` itself is retained, but
demoted to the rare depth-0-no-qualified-name fallback described above; it is no longer the primary
recursion gate for any feature that carries a qualified name.

###### No-single-root scoped fallback (`CollectTopLevelScopedParts`, `ResolveTopLevelConnections`)

Not every resolved `expose` scope names a single `part def` worth treating as "the" subject of the
diagram: per SysML v2 spec §8.3.26.11 (an InterconnectionView's subject need not be one definition)
and §9.2.20.2.6 ("exposed features as nodes, nested features as nested nodes"), the exposed content
can be one or more concrete `part` feature usages directly, with no enclosing definition of its own
— e.g. `expose PublishingSubsystem::*;` where `PublishingSubsystem` is itself only a namespace-like
`part def` whose sole nested `part markdownFormatter : MarkdownFormatter;` is the only thing worth
drawing. Before this fallback existed, `FindRoot` returning `null` always produced a totally empty
canvas in this shape, even though the scope named something concrete.

When `FindRoot` returns `null` and a scope is resolved, `BuildLayout` calls
`CollectTopLevelScopedParts(workspace, scope, theme, defsByName)`, which scans
`workspace.Declarations` for every non-standard-library `SysmlFeatureNode` with
`FeatureKeyword == "part"` whose qualified name satisfies `ExposeScopeResolver.IsInSubjectScope`,
excludes any matched feature that is itself nested (`"::"`-prefixed) under another matched
feature's own qualified name (it is already reachable as that ancestor's own nested part, so must
not also be duplicated as a separate top-level node), and builds a `PartItem` for each survivor via
`BuildPartItem` — the same container-vs-leaf recursion `CollectParts` uses for every other nested
part, extracted so the logic is never duplicated, gated by that same feature's own independently
decided `DetermineBranchUnlimitedRecursion(feature, scope)` (see _Per-Branch Depth-Limited
Recursion_ above) so this boxless fallback path applies the identical per-branch depth-limiting
decision as the normal container-rooted path — each top-level feature here is its own branch root,
exactly as a depth-0 feature is in `CollectParts`. Each survivor is returned as a `TopLevelPart(Part,
OwnerQualifiedName)` — pairing the built `PartItem` with its owning definition's qualified name
(everything before the feature's own qualified name's final `"::"` segment) — rather than a bare
`PartItem`, so `ResolveTopLevelConnections` (below) can later scope endpoint resolution correctly
per owning definition. When at least one top-level part is found, `ResolveTopLevelConnections` (a
`ResolveConnections` analogue that scans every definition's own connections, since a top-level
connection may be declared inside any definition in the workspace, keeping only a connection whose
own qualified name is itself in scope and whose endpoints both resolve into that connection's own
owning definition's top-level parts) resolves any connections between the top-level parts, and
`LayOutInteriorWithConnections` is called directly with `boxDepth: 0` and `reserveTitleArea: false`
— producing boxless top-level nodes placed side by side by the same `LayeredPlacement.PlaceWithPorts`
containment-packing algorithm used everywhere else, with no enclosing frame/title reserved. The
resulting `LayoutTree.Nodes` therefore holds the placed part boxes (and any ports/lines) directly as
top-level siblings, instead of the usual single root container box. When `CollectTopLevelScopedParts`
returns no parts (no scope, or a scope matching no `part` feature), the original minimal 200×100
empty canvas is preserved unchanged.

**Owner-scoped connection resolution (`ResolveTopLevelConnections`, `BuildPartIndex` vs. a flat
cross-workspace index).** `BuildPartIndex(parts)` — used by the single-root `ResolveConnections`
path — indexes by simple name only, which is safe there because one containing `part def` can never
have two direct children sharing a simple name. That assumption does **not** hold for the top-level
fallback: `CollectTopLevelScopedParts` can pull matching top-level features from many different
containing definitions across the entire workspace scan, and two different definitions can each
happen to declare a same-named part (e.g. both `part logger : Logger;`). A single flat
`Dictionary<string, int>` built from every collected top-level part would let `TryAdd` silently keep
only the first same-named occurrence, so a connection declared inside the _other_ definition would
incorrectly resolve against the first definition's part index — producing a bogus edge (or a
silently dropped connection, if the mis-resolved indices happened to coincide). `ResolveTopLevelConnections`
avoids this by grouping the received `IReadOnlyList<TopLevelPart>` into a `Dictionary<string,
Dictionary<string, int>>` keyed first by each part's own `OwnerQualifiedName` (from
`CollectTopLevelScopedParts`, not by re-deriving ownership from simple-name matching), then, for
each definition in `defsByName.Values.Distinct()`, resolving that definition's own
`SysmlConnectionNode` children's endpoints against **only** the name→index sub-map for that
definition's own qualified name — never the flat union across all definitions. A definition that
owns no collected top-level part (its own qualified name has no entry in the owner grouping) is
skipped entirely, since none of its connections could possibly resolve. This keeps every endpoint
resolution scoped to "this connection's own containing definition's direct children", matching the
same meaning `ResolveConnections`'s per-root `BuildPartIndex` already has for the single-root path,
without needing to change that unrelated, unaffected code path at all.

###### `BuildPartItem(feature, theme, depth, defsByName, visited, unlimitedRecursion)`

Extracted from `CollectParts`'s per-feature loop body: resolves the feature's `FeatureTyping`
against `defsByName` via `TryResolveContainer`, recursing into `LayOutInterior` at `depth + 1` for a
container part **only when `unlimitedRecursion` is `true`** (with the resolved child's qualified
name added to a copy of `visited`, `scope: null` — nested composition structure's own membership is
never re-derived from its own qualified name, matching `CollectParts`'s own documented recursion
behavior — and `ancestorUnlimitedRecursion: unlimitedRecursion`, propagating this branch's
already-decided flag unchanged to every descendant; see _Per-Branch Depth-Limited Recursion_ above)
or computing an intrinsic leaf size via `ComputePartSize` otherwise — which happens both
when the feature's type does not resolve to a container, and, unconditionally, whenever
`unlimitedRecursion` is `false` (a container part still renders as its own node; only its interior
expansion is suppressed). Here, `unlimitedRecursion` is always the concrete, already-decided
per-branch `bool` computed once at that branch's own depth 0 (never the ambient `bool?` — by the
time `CollectParts`/`CollectTopLevelScopedParts` call this method, the branch decision has always
already been resolved to a concrete value). This method is only ever invoked at `depth == 0`
directly (from `CollectParts` or `CollectTopLevelScopedParts`) or via its own
`unlimitedRecursion`-gated recursive `LayOutInterior` call, so gating solely on `unlimitedRecursion`
(without separately re-checking `depth`) is sufficient to limit expansion to "depth 0 only" under a
non-recursive branch decision.
Both `CollectParts` (depth > 0 or an already-scope-filtered depth-0 feature) and
`CollectTopLevelScopedParts` (a scope-matched top-level feature, always at `depth: 0`) call this one
method, so the container-vs-leaf recursion is defined exactly once.

###### `LayOutInteriorWithConnections(parts, pairs, theme, boxDepth, reserveTitleArea = true)`

Places `parts` and routes `pairs` via `LayeredPlacement.PlaceWithPorts`, unchanged from before this
feature except for two now-parameterized behaviors that previously assumed an enclosing container
box always exists: `boxDepth` is stamped directly onto each placed part's own `LayoutBox.Depth`
(the existing `LayOutInterior` call site passes `depth + 1`, preserving today's exact depth
numbering; the no-single-root fallback passes `0` directly, since there is no enclosing container
box in that path at all), and `reserveTitleArea` (default `true`, unchanged for every existing
caller) gates whether a title band is reserved above the placed content — `false` only for the
boxless fallback, where there is no enclosing frame/title to make room for, so the returned size is
just the bounding box of the placed content plus normal padding on every side.

###### Recursive nested layout (`LayOutInterior`, `CollectParts`, `BuildDefinitionIndex`)

The strategy supports genuine two-level (and deeper) nested block diagrams using a **recursive
bottom-up** scheme equivalent to ELK's `SEPARATE_CHILDREN` hierarchy mode: inner structure is laid
out first through flat layered placement, and each container is then treated as an atomic fixed-size
node by its parent, which is laid out with the **same** flat placement.

- **Container detection.** `BuildDefinitionIndex` builds a `Dictionary<string, SysmlDefinitionNode>`
  of candidate containers — non-standard-library `part def`s that have at least one nested `part`
  usage — keyed by both qualified and simple name (qualified preferred). For each part,
  `CollectParts` resolves its `FeatureTyping` against that index by qualified-then-simple name
  (`TryResolveContainer`); a part whose type resolves to a container, and whose type is not already
  on the recursion path, is a container, and every other part is a leaf.
- **Recursion.** A container part is laid out by calling `LayOutInterior` on its type definition at
  `depth + 1`, with the type's qualified name added to a `visited` set — but only when this
  branch's own decided `unlimitedRecursion` value is `true` (see _Per-Branch Depth-Limited
  Recursion_ above); when `false`, expansion never proceeds past that branch's own depth 0 and a
  deeper container is instead sized as an intrinsic leaf. The
  returned interior size becomes the part's atomic box size, and the returned interior content
  becomes its `InnerContent`. A `visited` qualified-name set guards against self- or
  mutually-referential types (cycle parts are treated as leaves), guaranteeing termination.
- **Sizing.** Each level reserves the same title area and insets used by the root:
  `offsetX = LabelPadding × 2`, `offsetY = TitleAreaHeight(hasLabel, hasKeyword) + LabelPadding × 2`,
  `containerWidth = TotalWidth + offsetX × 2`, and
  `containerHeight = TotalHeight + offsetY + LabelPadding × 2`. A container box therefore bounds its
  laid-out children plus its title and insets, and the parent treats that size as atomic. Box
  height itself is no longer scaled by hand from a per-port minimum-slot heuristic: each part's
  ports are modeled as named `LayoutGraphPort`s on `LayeredPlacement.PlaceWithPorts`'s input graph,
  so the layered algorithm itself grows a box, when needed, to keep every incident connection's
  port visually distinct. Every part is also passed to `PlaceWithPorts` with `HasLabel: true,
  HasKeyword: true` (every `PartItem` always carries a non-empty name and a `"part"` keyword,
  mirroring the `hasLabel: true, hasKeyword: true` convention already used for `TitleAreaHeight`
  above), which activates the engine's automatic title-vs-side-port reservation so no port is ever
  placed across the box's own title band.
- **Positioning.** `MakePartBox` builds a leaf box with empty `Children` (unchanged), and a
  container box whose `Children` are its `InnerContent` translated from the child's local origin
  `(0, 0)` to the box's absolute top-left by `TranslateNodes`, which recursively shifts box
  positions (and their nested children), port centres, and connector waypoints. The interior was
  laid out reserving its own title area, so the inner part boxes land below the container's
  "name : Type" title, inside its border. Box `Depth` increases by one per level (the renderer
  indexes `DepthFillColors` by modulo, so any depth is safe). The root container box mirrors this
  same pattern one level up: `BuildLayout` nests the root's own interior content as the root box's
  `Children` rather than as flat top-level siblings of the root box — the root sits at the same
  origin `(0, 0)` its interior content is already positioned relative to, so no translation is
  needed there.
- **No-op invariant.** When no part is a container, every `PartItem.InnerContent` is `null`,
  `MakePartBox` emits exactly the non-recursive leaf box with empty `Children`, and the placement
  call, offsets, ports, and lines are identical to the single-level layout — single-level output is
  byte-identical.
- **Cross-boundary resolution.** `ResolveEndpoint` resolves the **full** dotted endpoint reference,
  not just its head segment: the head segment still identifies the parent-level part index, and any
  remaining dotted segment(s) (e.g. `cpu` in `board.cpu`, or `encoder` in `StepperMotorX.encoder`) is
  captured as that endpoint's port-name label and flows through `ConnPair.LabelA`/`LabelB` into the
  emitted `LayoutPort.ExternalLabel`. This means a reference into a nested part (`connect psu to
  board.cpu`) now shows the true target name (`cpu`) on the port, rather than the pre-fix behavior of
  silently discarding it. The connector itself is still **routed only to the container's own
  boundary** — it does not continue into the container's interior to physically terminate on the
  inner `cpu` box. Achieving that (a genuine boundary/delegation-port anchor shared between the
  outer connector and an inner one, via the companion library's `HierarchyHandling.Recursive`
  support) would require restructuring `LayOutInterior`'s per-level independent
  `LayeredPlacement.PlaceWithPorts` calls into one connected nested `LayoutGraph`/
  `LayoutGraphNode.Children` for the affected subtree, which is a materially larger architectural
  change than this feature makes; it remains a known, documented limitation. `ResolveEndpoint`
  captures **everything** after the first dotted segment as the label, however many levels deep
  (e.g. `board.sub.cpu` yields the label `sub.cpu`, not just `sub`); only the _routing_ — never the
  label text — stops at the container's boundary regardless of path depth.

##### Port Labeling

Each connection endpoint that resolves to a nested part is requested as a named
`LayeredPlacement.EdgePortRef` on that endpoint's `PortEdge`, carrying the real SysML port-name
segment (from `ConnPair.LabelA`/`LabelB`; see _Cross-boundary resolution_ above and
`ResolveEndpoint` below) as the port's `ExternalLabel`. `LayeredPlacement.PlaceWithPorts` creates
the corresponding `LayoutGraphPort` and returns the engine-placed `LayoutPort` with that label
already attached — the strategy only translates the returned port's `CentreX`/`CentreY` by the
container offset before adding it to the interior's content. A bare endpoint reference with no
dotted port segment (e.g. `connect psu to board`) yields a `null` label (a port is still created
and placed; only its label is absent), preserving the pre-fix rendering (no label shown) for that
connector end.

##### Parallel Connection Preservation

`LayOutInterior` calls `LayeredPlacement.PlaceWithPorts` (which unconditionally disables
parallel-edge merging — see `docs/design/sysml2-tools-core/layout/internal/layered-placement.md`),
so multiple distinct SysML connections between the same two parts (e.g. separate
`power`/`encoder`/`sensor` connections wired between the same controller and motor, as seen in a
real 3-axis-gantry wiring model) are never collapsed onto one shared routed polyline: each
connection keeps its own independently-routed connector with distinct waypoints (a separate
parallel lane), and its own pair of labeled ports.

###### `FindRoot(workspace, scope)`

Chooses the non-standard-library `part def` with the most connection usages (breaking ties by the
most part usages) as the definition whose interior to render, restricted — when a scope is
resolved — to candidates for which `ExposeScopeResolver.IsRootRelevantToScope` returns `true`;
returns `null` when no candidate is relevant to a non-null scope (an empty canvas results), and
falls back to considering every candidate when `scope` is `null`. When multiple candidates are
relevant to a non-null scope (possible because a nested definition and its ancestor can both be
relevant), the most specific (deepest/longest qualified name) relevant candidate is preferred via
`ExposeScopeResolver.IsMoreSpecificCandidate`, with the connections/parts tie-break used only to
break ties among equally specific candidates; this ordering does not apply when `scope` is `null`.

###### `CollectParts` and `ResolveConnections(root, partIndex)`

`CollectParts` gathers the root's nested `part` usages, sizing each box from its `name : Type`
label, additionally excluding — when a scope is resolved and `depth == 0` — any part feature whose
qualified name fails `ExposeScopeResolver.IsInSubjectScope`. `ResolveConnections` maps each binary
connection's dotted endpoint references to nested-part indices and port-name labels via
`ResolveEndpoint` (matching the first dotted segment against the (possibly narrowed) part names and
capturing any remaining segment as the port label — see _Cross-boundary resolution_ above), keeping
only distinct, resolvable pairs; a connection whose endpoint was excluded by scoping simply fails to
resolve and is dropped by this existing endpoint-lookup logic — no separate edge-side scoping is
needed.

##### Expose Scoping

Because this strategy renders exactly one selected root's interior, scoping cannot narrow a
workspace-wide collection the way `GridViewLayoutStrategy` and `BrowserViewLayoutStrategy` do;
instead it restricts **which root is selected**, then narrows **which of that root's parts are
shown**, and finally — via `DetermineBranchUnlimitedRecursion`/`MatchesUnlimitedSubject` (see
_Per-Branch Depth-Limited Recursion_ above) — governs **how deep into each shown part's own
interior the diagram recurses, decided independently per branch**.
`FindRoot` only considers candidates `ExposeScopeResolver.IsRootRelevantToScope` accepts,
so exposing the current heuristic root itself, an inner part of it, or a definition that itself
contains the heuristic default all correctly select a root, while exposing an unrelated
definition yields no root and thus the minimal empty canvas. When more than one candidate is
relevant (a nested definition and an ancestor definition can both be relevant to the same exposed
subject), `ExposeScopeResolver.IsMoreSpecificCandidate` prefers the most deeply nested candidate,
so exposing an inner part of a nested definition correctly selects that nested definition rather
than its ancestor, even when the ancestor has more connections/parts. `CollectParts` then narrows
the selected root's own part features to those within the resolved scope (via
`ExposeScopeResolver.IsInSubjectScope`), and `ResolveConnections`'s existing "skip connections
whose endpoint did not resolve" behavior transparently drops any connection touching an excluded
part — no new edge-side logic was required. A view with no `expose` statement (including the
synthesized `--auto` view, whose `ViewNode` is `null`) resolves no scope, so `FindRoot` considers
every candidate, `CollectParts` keeps every part, and every branch's decided recursion flag is
always `true` — completely unchanged from the pre-scoping behavior.

Once a root is selected and its direct parts are narrowed, each depth-0 feature's own independently
decided recursion flag determines whether the diagram continues to recurse into that specific
feature's own composed interior at all: when the feature matches a subject with unlimited-depth
recursion (`expose X::**;` / `expose X::*::**;`), or there is no scope at all, that feature's
branch fully expands its own nested parts, unbounded in depth — identical to today's behavior
before this depth-limiting was introduced. When the feature matches only non-recursive subjects
(`expose X;` and/or `expose X::*;` forms), that specific branch's expansion stops after its own
direct part children: a deeper container part within that branch still renders as its own box, but
with no nested children of its own drawn inside it — while a sibling branch matched by a different,
recursive subject in the very same scope fully recurses, independently of this one.

###### Placement and routing

Placement and routing are delegated to `LayeredPlacement.PlaceWithPorts` with a left-to-right flow
direction; parallel-edge merging is unconditionally disabled by that method (see _Parallel
Connection Preservation_ above). Nested-container recursion is driven at the strategy level (see
_Recursive nested layout_ above); each level calls the same flat placement helper. The strategy
passes the collected part boxes and resolved connection pairs — each carrying an `EdgePortRef` for
every endpoint — as plain geometric input. `LayeredPlacement` delegates to the off-the-shelf
`DemaConsulting.Rendering.Layout` layered algorithm and returns placed rectangles, connector
waypoints, and correlated placed ports, with disconnected components packed without overlap and
port spacing/box growth resolved by the engine itself. The total canvas extent is derived from the
placed box and waypoint geometry.

The strategy then shifts the placed content to sit inside the container box and extends the
container so every connector waypoint is enclosed, without ever moving a box.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. The absence of an eligible
part definition or of nested parts is not an error: the method returns the minimal empty canvas,
unless the resolved scope directly includes one or more top-level `part` feature usages, in which
case the no-single-root scoped fallback renders those instead (see above). Connectors that cannot
be routed cleanly are still drawn; this strategy does not itself construct `LayoutWarnings`
diagnostics, so the returned `LayoutTree` carries no layout-quality warnings.

##### Dependencies

- `ILayoutStrategy` and `ViewContext` (Rendering subsystem) — the strategy contract and view input.
- `RenderOptions`, `Theme`, and `BoxMetrics` (`DemaConsulting.Rendering.Abstractions`) — render
  options and sizing metrics.
- `LayeredPlacement` (Layout Internal subsystem) — placement and routing through
  `DemaConsulting.Rendering.Layout`.
- `StdlibFilter` (Rendering Internal subsystem) — standard-library exclusion.
- `ExposeScopeResolver` (Layout Internal subsystem) — `ResolveExposedScope`,
  `IsRootRelevantToScope`, and `IsInSubjectScope` supply the shared `expose`-scoping used by
  `BuildLayout`, `FindRoot`, and `CollectParts`; `ExposedScope.Subjects`' recursion kinds also drive
  `HasUnlimitedRecursion`'s depth-limiting decision.
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlFeatureNode`, `SysmlConnectionNode` (Semantic subsystem) — model input.
- The `LayoutTree`, `LayoutBox`, `LayoutPort`, and `LayoutLine` data types
  (`DemaConsulting.Rendering`).

##### Callers

The Rendering subsystem selects `InterconnectionViewLayoutStrategy` when rendering an
Interconnection View. No other unit calls it directly.
