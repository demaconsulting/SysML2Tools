# SysML2Tools Roadmap

This document lists planned work — what we intend to change and notes on how to change it. It is
deliberately limited to work to be done.

The work falls into three themes:

- **Notation & view conformance** — bring rendered output in line with SysML v2 graphical notation
  and finish the remaining view dynamics.
- **Release & packaging** — self-validation coverage, package validation, and licensing/attribution.
- **Model query & analysis** — further AI-analysis options beyond the completed dynamic
  (ad-hoc) views and `export` verb features.

---

## Notation & view conformance

### Additional relationship edges (General View)

Render the relationships currently omitted from the General View, each routed via
`ChannelRouter` and carrying the correct spec end shape:

- Subsetting (where shown as edges), dependency, connection/binding, allocation.
- Fix `ref` usages, currently drawn as a hollow-diamond Membership edge: SysML v2 removed
  "shared aggregation" (the UML/SysML v1 hollow-diamond concept), so `ref` should render as a
  dependency-style edge (dashed, open arrowhead) instead.
- Shared-bus generalization (multiple subtypes merging into one line to a supertype) as an
  optional readability refinement.

Per the OMG SysML v2 spec's Graphical Notation chapter (§8.2.3), `item`, `occurrence`,
`action`, `state`, and `requirement` usages are **not** edge-connected boxes — they are
canonically rendered as compartment rows on their owning box (same mechanism as
`attribute`), which `GeneralViewLayoutStrategy.BuildCompartments` already does generically
for any feature keyword. No new edge kind or "containment broadening" is needed for these;
earlier roadmap phrasing implying a `containment` edge kind was based on secondary-source
notation tables that conflated other relationships (item flow, action succession,
requirement satisfy/derive) with containment, and has been corrected here.

**Scope:** `AstBuilder`/semantic exposure of the relationship kinds as needed;
`GeneralViewLayoutStrategy` edge emission; resolver coverage. Also extend the drone gallery
model (`docs/gallery/models/01-drone-general.sysml`, which today has no real `connect`/`bind`,
`allocate`, `dependency`, or `subsets` usages) with a minimal real example of each new
relationship kind, and regenerate its gallery SVG(s), so the visual gate below is demonstrated
in the shipped gallery rather than only in unit-test fixtures.
**Visual gate:** a model exercising each relationship renders distinct, correctly-headed edges —
demonstrated both by test fixtures and by the regenerated gallery drone model/SVG.

### Gallery: expand drone model with expose/filter-narrowed multi-view showcase (follow-up)

The relationship-edges item above adds minimal real examples of `connect`/`allocate`/
`dependency`/`subsets` to the drone model to prove the edges render correctly. This follow-up
goes further: build out a richer, real-world-sized drone model and add several
`expose`/`filter`-narrowed views spotlighting one relationship kind each, instead of one flat
whole-workspace General View:

- A power/data subsystem view (`expose PowerSubsystem;`) showing `connect` edges between ports.
- A requirements-traceability view (`filter @Requirement;` or an `expose`d requirement) showing
  `allocate` edges from requirements to parts.
- A subsystem-boundary view showing cross-subsystem `dependency` edges without full
  implementation detail.
- A variant/specialization view showing `subsets` alongside the existing redefinition example.

This also exercises `--filter`'s metadata classification-test path (e.g. tagging parts with
`@Critical`/`@Requirement` metadata) rather than only the bracket/expose-subtree path the
gallery currently shows.

**Scope:** `docs/gallery/models/01-drone-general.sysml` (or a new model file), regenerated SVGs,
`docs/gallery/README.md`.
**Depends on:** "Additional relationship edges (General View)" above (needs the new edge kinds
implemented first).

### Annotating elements & compartment depth

- Render **Documentation/Comment** notes as `BoxShape.Note` (folded-corner) nodes attached to
  their annotated element.
- Extend compartments to spec depth: enumeration values, constraint bodies, requirement
  `subject`/`constraints`/`doc`, and a documentation compartment on definitions/usages.

**Scope:** semantic exposure of doc/comment + compartment content; `GeneralViewLayoutStrategy`
and renderers; possibly `LayoutLabel`/compartment tweaks.
**Visual gate:** a documented requirement/part renders its note and full compartments.

### Action Flow View: control-node/successor AST correctness + fork/join/decision/merge shapes

Investigation (mirroring the pattern found in the State Transition View item above — same grammar
family, same class of bug) found `actionBodyItem`'s combined shape has the identical AST gap, plus
control nodes are entirely uncaptured today:

1. **Attached/implicit-source successions are silently dropped (both the action AND its successor).**
   `actionBodyItem: (sourceSuccessionMember)? actionBehaviorMember (actionTargetSuccessionMember)*`
   has no `AstBuilder` visitor, so the compact, common idiom `action a1; then a2;` (successor
   implied by adjacency, no explicit `succession`/`first` statement) falls through to ANTLR's
   default aggregation and loses both nodes — the same failure mode fixed for `stateBodyItem` in
   the State Transition View branch.
2. **Fork/join/decide/merge/accept/send control nodes are entirely unmodeled.** The grammar's
   `actionNode: controlNode | sendNode | acceptNode | assignmentNode | terminateNode | ifNode |
   whileLoopNode | forLoopNode` (`controlNode: mergeNode | decisionNode | joinNode | forkNode`) has
   zero `AstBuilder` visitors — not even a bare `fork f1;` registers today, so every Action Flow
   View renders every node as an identical rounded-rectangle "action" box regardless of its real
   control-node kind, and successions through them are lost too (compounding problem 1).
3. **Renderer shape primitives already exist for the two structural kinds, just unused.** The
   `DemaConsulting.Rendering` package already defines `BadgeShape.HorizontalBar`/`VerticalBar`
   (usable for fork/join thick bars) and `BadgeShape.Diamond` (usable for decision/merge) —
   `ActionFlowViewLayoutStrategy` just never produces a `LayoutBadge` for anything but the
   start/done markers today. There is **no pentagon primitive** in the Rendering package's
   `BoxShape`/`BadgeShape` enums for a true UML accept/send action shape — adding one requires a
   change to the separate `DemaConsulting.Rendering` package, out of scope for this repo/branch.

**Scope (this branch):** `AstBuilder` (new handling for the `actionBehaviorMember
(actionTargetSuccessionMember)*` combined shape, mirroring the state-body fix; new visitors for
`controlNode`/`sendNode`/`acceptNode` registering a distinguishable node kind);
`ActionFlowViewLayoutStrategy` (render fork/join as `LayoutBadge(BadgeShape.HorizontalBar |
VerticalBar)`, decision/merge as `LayoutBadge(BadgeShape.Diamond)`, keeping accept/send as
keyword-labelled rounded-rectangle boxes for now, pending a future pentagon primitive).
**Explicitly deferred (separate future items, not this branch):** a true pentagon accept/send
shape (needs a `DemaConsulting.Rendering` package change); swim-lanes via `LayoutBand`; item-flow
edge annotations (no item-flow/payload capture exists in the AST yet); the Sequence View dynamics
item below (different subsystem, separate branch).
**Visual gate:** an action flow with a fork feeding two parallel actions that rejoin renders a
thick bar in each direction; a guarded decision/merge renders diamonds; the compact `action a;
then b;` idiom renders correctly with no dropped nodes.

### Sequence View dynamics

- Populate `LayoutActivation` execution bars; combined-fragment boxes (alt/opt/loop); async/reply
  message styling.
- **Sequence dynamic-view compatibility check (known limitation, carried over from "Dynamic
  (ad-hoc) views", done):** `DynamicViewSynthesizer`'s `--view-type sequence` pre-check accepts
  any target with at least one nested `message` usage (the cheap, necessary-but-not-sufficient
  approximation of "at least one lifeline" — the AST has no dedicated lifeline node, so a full
  message-edge-walk validation was deliberately not implemented). A target with lifelines but
  zero resolvable messages passes this pre-check yet still renders the near-blank canonical
  `LayoutTree` sentinel. Closing this gap requires either a full message-edge-walk validation in
  `DynamicViewSynthesizer` or surfacing `SequenceViewLayoutStrategy`'s own lifeline-resolution
  result back to the synthesizer.

**Scope:** `SequenceViewLayoutStrategy`, renderer shape primitives (note). `LayoutActivation`
already defined.
**Visual gate:** sequence shows activation bars + a fragment.

### State Transition View: attached-transition states, entry/exit actions, inherited pseudostate features

Investigation for this item (cross-checking the real OMG corpus fixture
`training/25.Transitions/TransitionActions.sysml` and the OMG spec's own worked example, `formal-26-03-02.md`
Annex A.7 "States") found the actual gap is significantly larger than originally scoped here, and is a
correctness bug, not just a missing notation refinement:

1. **Attached-transition state bodies are silently dropped (both the state AND its transition).**
   SysML v2's most common compact state-machine idiom writes a state's outgoing transition directly after it
   with no `transition`/`first` keyword at all, e.g. `state off; accept Sig then starting;` — grammatically
   `stateBodyItem: (sourceSuccessionMember)? behaviorUsageMember (targetTransitionUsageMember)*`, where the
   transition's source is *implicitly* the immediately preceding state usage in the same body item. The
   parallel shape `entryActionMember (entryTransitionMember)*` (e.g. `entry action initial; then off;`) has
   the identical problem. `AstBuilder` has no visitor override for either shape, so ANTLR's default
   `VisitChildren` (which returns only the last child's result, discarding earlier ones) causes **both** the
   preceding usage and its attached transition(s) to vanish — confirmed: `vehicleStates` in the fixture above
   should register 4 states and 4 transitions, but exporting it today yields 0 states and 1 transition, plus
   "Unresolved reference" warnings for the never-registered state names.
2. **Entry/do/exit action features are entirely unmodeled.** `entryActionMember`, `doActionMember`,
   `exitActionMember` (and their nested `statePerformActionUsage`/`stateAcceptActionUsage`/etc.) have no
   `AstBuilder` visitor at all — not even the well-formed, spec-preferred style of declaring a named entry
   action and referencing it from a separate `transition` statement (OMG spec Annex A.7: `entry action
   initial; ... transition initial then off;`) currently registers a resolvable `initial` feature.
3. **Inherited pseudostate-like features don't resolve.** The training corpus's explicit form of an initial
   transition, `first start then off;`, references `start` — a real feature every state definition/usage
   inherits from `Action` (`Stdlib/SystemsLibrary/Actions.sysml`'s `action start: Action :>> startShot`), not
   a special keyword. `ReferenceResolver`'s feature-chain walk only looks up local/imported names, not
   inherited members, so `start` (and `done`, its counterpart) fail to resolve even once (1)/(2) are fixed.
4. **Initial-pseudostate marker rendering is already partially implemented but purely heuristic.**
   `StateTransitionViewLayoutStrategy.AddInitialMarker` already draws the conventional filled-circle marker
   with an arrow into the *first declared* state, unconditionally, regardless of whether a real initial
   transition resolves. Once (1)-(3) let `start`/`initial`-sourced transitions resolve, the layout strategy
   needs to: (a) prefer the semantically-resolved initial-transition target over the first-declared-state
   guess when one exists, and (b) exclude pseudostate/entry-action source features (e.g. `start`, `initial`)
   from being drawn as ordinary state boxes — today `CollectStates`'s "states referenced only by transition
   endpoints" fallback would add them as a spurious extra box.

**Scope:** `AstBuilder` (new handling for the `behaviorUsageMember (targetTransitionUsageMember)*` and
`entryActionMember (entryTransitionMember)*` state-body shapes, producing the preceding usage node plus one
`SysmlTransitionNode` per attached transition with an implicit `Source`; minimal feature-node support for
entry/do/exit action declarations so their names are resolvable); `ReferenceResolver`/`TryResolveFeatureChain`
(inherited-member lookup so `start`/`done` resolve); `StateTransitionViewLayoutStrategy` (prefer a resolved
initial transition over the first-declared-state heuristic; exclude pseudostate/entry-action sources from
ordinary state-box rendering).
**Visual gate:** a state machine using the `state X; accept ... then Y;` idiom renders every state and every
attached transition correctly; a `first start then InitialState;`-shaped (or `entry action initial; ...
transition initial then X;`-shaped) entry transition renders a filled-circle initial marker with an edge into
the correct (resolved) state, with no spurious `start`/`initial` box.

### Interconnection View: genuine cross-boundary connector routing

`InterconnectionViewLayoutStrategy` now resolves a connection endpoint's full dotted reference
(e.g. `board.cpu`) for its port **label**, so a cross-boundary reference shows the true nested
target's name instead of discarding it. The connector line itself, however, still terminates at
the containing part's own box boundary rather than routing all the way into the nested container
to the inner part — genuine cross-boundary routing would require restructuring
`LayOutInterior`'s per-level independent `LayeredPlacement.Place` calls into one connected nested
`LayoutGraph`/`LayoutGraphNode.Children` for the affected subtree, using the companion
`DemaConsulting.Rendering` package's boundary/delegation-port (`HierarchyHandling.Recursive`)
support end-to-end, instead of the strategy's current two-independent-layouts-stitched-together
recursion.

**Scope:** `InterconnectionViewLayoutStrategy`'s `LayOutInterior`/`CollectParts` recursion;
possibly a new `LayeredPlacement` entry point that builds a genuinely nested `LayoutGraph`.
**Visual gate:** `connect psu to board.cpu;` renders a connector line that visually terminates on
the inner `cpu` box, not the `board` container's boundary.

### View `filter [<expr>];` expression evaluation

**Phase 1 — done.** `GeneralViewLayoutStrategy` scopes a rendered diagram to a view's
`expose <...>;` subject subtree, and now also evaluates a standalone view `filter <expr>;` body
statement (via the new `DemaConsulting.SysML2Tools.Core.Filtering` subsystem —
`FilterExpression`/`FilterExpressionParser`/`FilterExpressionEvaluator`) for a defined Phase 1
construct subset, narrowing the rendered scope by the resulting predicate:

- Metadata classification-test atoms (`@Type`, `@Pkg::Type`), matched against a new
  `SysmlMetadataNode` semantic-model type capturing each definition's applied metadata
  annotations (`{@Type{attr = value;}}`/`@Type;`/`@Type{}`), resolved via `ReferenceResolver`.
- Boolean connectives: `and`, `or`, `not`, `xor`, `&`, `|`, and parenthesization.
- `(as Type).attribute` reads, bare or compared with `==`/`!=` against a scalar (boolean, number,
  or string) literal.

Any construct outside this subset — `istype`/`hastype`/`all`, arithmetic, conditional
expressions, general feature-chain navigation, or a syntax error — produces an explicit
"unsupported filter construct" (or syntax-error) diagnostic and falls back to rendering the
resolved (`expose`) scope unfiltered, exactly as Phase 0 did for every filter expression.

**Phase 2a — done.** The bracketed `expose <path>::**[<expr>]` filter form is now evaluated too,
reusing the identical Phase 1 parser/evaluator unchanged. Fixed a Phase 1 pairing defect first:
`SysmlViewNode` previously captured a view's `expose` entries as two flattened, unpaired parallel
lists (`ExposedNames`/`ExposeBracketFilterTexts`), making it impossible to tell which bracket
filter belonged to which exposed path once a view declared more than one `expose` member; both
are now replaced by a single `ExposeMembers` list of paired `ExposeMember(QualifiedName,
BracketFilterExpressionText)` records. `ExposeScopeResolver` re-pairs each resolved `Expose` edge
with its originating `ExposeMember` and, for an entry carrying a bracket filter, parses and
evaluates it against a candidate set restricted to that entry's own target's containment subtree
of definitions (mirroring `GeneralViewLayoutStrategy.CollectDefinitions`'s existing restriction);
a successfully-evaluated filter narrows that entry's contribution to only the matched
definitions, while every other `expose` entry in the same view continues to contribute its whole
subtree unaffected. A bracket expression that fails to parse or evaluate degrades gracefully to
the previous whole-subtree behavior for that entry, with `LayoutWarnings.ForUnevaluatedExposeBracketFilter`
now warning only on that failure (mirroring `ForUnevaluatedFilter`'s existing failure-only
pattern) instead of unconditionally warning whenever any bracket filter was merely present.

**Phase 2b — deferred (zero corpus evidence):** the Phase 1-excluded construct list —
`istype`/`hastype`/`all`, arithmetic operators, conditional (`if`/`else`) expressions, and general
feature-chain navigation (attribute/feature reads not anchored by an `(as Type)` cast). Each
currently produces a clear, non-crashing "unsupported filter construct" diagnostic rather than
silently doing nothing. Across all 251 OMG corpus files sampled during Phase 2 planning, every
real `filter`/bracket-form-`expose` expression already fell within the Phase 1/2a supported
subset — there is no observed real-world need to implement these constructs yet, so they remain
deferred until a concrete corpus example demonstrates a need.

**Phase 2c — deferred (no current consumer):** metadata annotations on **usages** (as opposed to
definitions) are captured in the semantic model (`SysmlMetadataNode` is attached wherever
`metadataFeature` appears), but filter/bracket-filter narrowing only evaluates classification
tests/attribute reads against `SysmlDefinitionNode` candidates (matching
`CollectDefinitions`'s/`ExposeScopeResolver`'s existing definition-only restriction) — extending
evaluation to usage-level candidates is future work. Today, only `GeneralViewLayoutStrategy` uses
definition-scoped filter candidates at all; no other view kind or consumer currently needs
usage-level candidate filtering, so there is no concrete driver to implement it yet.

**Scope:** `SysmlNode.cs`/`AstBuilder.cs`/`ReferenceResolver.cs`/`SysmlEdge.cs` (metadata capture,
paired `ExposeMember` model); `DemaConsulting.SysML2Tools.Core.Filtering` (Phase 1 subsystem,
reused unchanged for Phase 2a); `ExposeScopeResolver` (Phase 2a bracket-filter evaluation, shared
by all 7 layout strategies); `GeneralViewLayoutStrategy`/`LayoutWarnings` (filter application,
failure-only bracket-filter warning).
**Visual gate:** a view with a standalone `filter @Type;`-style Phase 1 statement, or a bracketed
`expose <path>::**[<expr>]` Phase 2a statement, renders only the elements satisfying the
predicate, with no "unevaluated"/"not yet evaluated" warning for that statement; an unsupported
construct still falls back to the resolved scope with an explicit diagnostic.

---

## Release & packaging

### Self-validation suite (expand from 3 to ~12 tests)

Downstream projects run `sysml2tools --validate` in their own environment as tool-qualification
evidence, and the win/mac/linux integration-test matrix runs it per-OS. Tests follow the DEMA
naming convention `SysML2Tools_{Capability}` (tool prefix + descriptive capability) for instant
recognition in per-OS evidence. Rename the existing three (drop the redundant `SelfTest` suffix)
and add the rest; each render test emits **both `.svg` and `.png`** and asserts output validity,
so SkiaSharp native assets are exercised on every view and every OS:

| Test | Proves |
|---|---|
| `SysML2Tools_VersionDisplay` | `--version` |
| `SysML2Tools_HelpDisplay` | `--help` |
| `SysML2Tools_Lint` | clean model → 0 errors (parser + stdlib + semantic) |
| `SysML2Tools_LintDiagnostics` | model with a known error → expected diagnostic |
| `SysML2Tools_RenderGeneralView` | General view → SVG + PNG valid |
| `SysML2Tools_RenderInterconnectionView` | ports/connectors → SVG + PNG |
| `SysML2Tools_RenderStateTransitionView` | states → SVG + PNG |
| `SysML2Tools_RenderActionFlowView` | layered actions → SVG + PNG |
| `SysML2Tools_RenderSequenceView` | lifelines → SVG + PNG |
| `SysML2Tools_RenderGridView` | matrix → SVG + PNG |
| `SysML2Tools_RenderBrowserView` | tree → SVG + PNG |
| `SysML2Tools_AutoRender` | `--auto` path |

Validity is asserted (well-formed SVG root; PNG signature + non-zero dimensions), not exact
bytes, so the evidence is robust across environments.

### Package validation gate (automated, before publish)

`build.ps1`/`lint.ps1` validate the source but not the produced packages — add a repeatable check
that:

1. `dotnet pack` all four packages → unzip each `.nupkg` and assert contents (expected DLLs,
   license file, third-party notices incl. Noto Sans OFL, README, icon, correct dependencies and
   metadata; `dotnet pack` warnings-as-errors).
2. **Tool smoke test:** install the packed tool from a local feed into a clean directory → run
   `--version`, render a sample to **both SVG and PNG** (PNG proves SkiaSharp natives resolve),
   and `--licenses`.
3. **Library-consumer smoke test:** a throwaway project referencing each library package from the
   local feed → restore → exercise parse→layout→render-to-SVG-in-memory and render-to-PNG (again
   proving SkiaSharp natives for `.Png` consumers).

### Licensing, docs, gallery, publish

- **Licensing/attribution:** `--licenses` output covering Noto Sans (SIL OFL 1.1) and other OTS;
  per-package README notes incl. the SkiaSharp native-assets requirement for
  `DemaConsulting.SysML2Tools.Png` consumers.
- **Documentation:** README and User Guide state that the Geometry View is not yet supported.
  Finalise the layout-algorithm reference in `docs/` and wire it into CI (`build.yaml`,
  `.fileassert.yaml`, `.reviewmark.yaml`).
- **Gallery & packaging:** regenerate the gallery against the final notation and refresh
  `docs/gallery/README.md`; set version metadata, package descriptions/icons/tags, and release
  notes (generated from commit messages via the build notes).
- **Publish:** final full-suite validation; tag; publish the four NuGet packages; create the
  GitHub release with notes and gallery highlights. **Publishing requires maintainer authorization
  (credentials, irreversible) — prepared to the edge of publish, then handed off.**

---

## Model query & analysis

SysML v2 is emerging as a substrate for AI-verified engineering: a machine-readable, semantically
resolved model of a system's structure, behavior, requirements, and traceability. An AI agent
changing or reviewing code could re-derive all of that by reading raw source — or it could ask
SysML2Tools targeted questions and get small, authoritative, token-cheap answers. This theme
extends SysML2Tools's model query and analysis capabilities beyond the existing `query` command.

**Done:** Dynamic (ad-hoc) views — `render --view-type <kind> --view-target
<qualified-name> [--filter <expr>]` renders any view type of any resolvable element without
requiring the model to declare a `view`. Implemented by `DynamicViewSynthesizer` (Core Rendering
Internal subsystem), wired through `RenderCommand`'s new flags. See its design/requirements/
verification documentation for the full per-kind compatibility rules; the sequence-view
compatibility-check gap is carried forward as a known limitation in the "View dynamics
refinements" item above.

**Done:** `export` verb — `export [--format json|jsonl] [--output <file>] [--include-stdlib]
[--target <qualified-name>] [--filter <expr>] <patterns...>` dumps the resolved semantic
model (declarations, edges, diagnostics) as a single indented JSON document or as JSON
Lines, for an agent harness to index locally and run its own queries offline. `--target`
restricts output to a single element's containment subtree (expanding a usage/feature
target to its resolved type's subtree too), and `--filter` narrows the (optionally
`--target`-scoped) declarations/edges using the same Phase 1 filter-expression subset
`render`'s dynamic-view `--filter` uses, composing `--target`-then-`--filter` — a graceful,
non-aborting warning/diagnostic is surfaced for an unparseable `--filter` expression rather
than failing the export. Implemented by the new `Export` subsystem (`ExportCommand`,
`ExportResult`, `ExportResultSerializerContext`/`ExportLineSerializerContext`), reusing
`SysmlNode`/`SysmlEdge`/`SysmlDiagnostic` directly rather than a fourth parallel result shape.
See its design/requirements/verification documentation for the full JSON/JSONL output shape.

### Additional AI-analysis options (candidates)

Lower-priority options that further support AI analysis of a model; each is independently
scoped and gated, and any may be pulled forward or dropped:

- **`--format sarif` for `lint`** — `SysmlDiagnostic` is already structurally SARIF-compatible;
  emitting SARIF lets AI/CI toolchains consume lint findings through standard tooling.
- **Metrics / summary query** — workspace-level counts and hotspots (most-depended-on elements,
  unverified requirements, orphan elements, cyclic specialization) to orient an AI before it
  starts, and to flag model-health issues in review.
- **`diff` of two workspaces** — structural/semantic delta between two model revisions (added /
  removed / changed elements, edges, and requirement traces) so an AI reviewing a change sees the
  *model-level* impact of a PR, not just the text diff.
- **Machine-readable `--format json` everywhere** — extend the shared formatter so every command
  (not just `query`/`export`) can emit JSON, keeping the CLI uniformly scriptable.

---

## Deferred / out of scope (for now)

Not planned yet; listed so the boundary is explicit:

- **Geometry View** — the 8th view: 2D spatial placement (3D projected to 2D) of items whose
  spatial coordinates are specified in the model via the SysML geometry/spatial library. Requires
  new semantic capability (extracting numeric attribute *values*, not just structure) and a
  coordinate convention plus test models that use it. Until it ships, the README and User Guide
  document it as not yet supported.
- **3D Geometry** rendering (2D projection only, even once Geometry ships).
- **Loadable theme files** (YAML/JSON) — the `Theme` record is forward-compatible.
- **Nested state regions** and other advanced behavioral notation.
