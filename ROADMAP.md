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

### Additional relationship edges (General View) — delivered

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

### Annotating elements & compartment depth — delivered

**Delivered.** `AstBuilder` now captures every content kind this item scoped: `VisitEnumeratedValue`
builds an `"enum value"`-keyword feature for each `enum def` literal (bare, value-assignment, and
redefinition-body forms), via a dedicated `CollectEnumerationBodyChildren` helper (needed because
`enumerationBody` uniquely alternates `annotatingMember`/`enumerationUsageMember` directly rather
than wrapping them in a single rule). `VisitRequirementDefinition`/`VisitConcernDefinition` and —
extending beyond a literal reading of this item, since the dominant real-corpus idiom nests these
inside a requirement/concern *usage* rather than a definition — `VisitRequirementUsage`/
`VisitConcernUsage` now capture `subject`/`actor`/`stakeholder` members (`VisitSubjectUsage`/
`VisitActorUsage`/`VisitStakeholderUsage`) and `require constraint`/`assume constraint` members
(`VisitRequirementConstraintMember`/`BuildConstraintFeatureNode`, capturing the raw expression text
into a new `SysmlFeatureNode.ExpressionText` field mirroring `SysmlTransitionNode.Guard`).
`VisitConstraintUsage` (previously entirely absent) and a rewritten `VisitConstraintDefinition`
capture standalone/definition-level constraint expressions the same way. A newly-introduced
`VisitRequirementVerificationMember`/`VisitFramedConcernMember` null-suppression safeguard
prevents a `verify`/`frame` member's own nested body from being spuriously hoisted onto the
enclosing requirement's `Children` now that body-collection reaches into `requirementBodyItem`.

`GeneralViewLayoutStrategy` now reads `Annotations`: `AddAnnotationNote` emits one `BoxShape.Note`
box per annotated definition (concatenating multiple `Documentation`/`Comment` annotations into a
single note, documented as a deliberate choice) connected by a plain solid line with no end
marker — implemented as ordinary `LayoutGraph` nodes/edges added during the existing single-pass
`BuildGraph` traversal, a documented deviation from a literal post-layout marker-decoration
approach (no equivalent decoration phase exists in this strategy). `Pluralize` titles
`"subject"`/`"assume constraint"`/`"require constraint"`/`"constraint"` compartments with the
guillemet stereotype form (reusing the pre-existing `«allocate»` edge-label convention), and
`FormatFeatureRow` renders a constraint-kind feature's raw expression text in place of the generic
`name : Type [multiplicity]` row shape.

The Quadcopter Drone gallery model (`docs/gallery/models/01-drone-general.sysml`) was extended
with a `doc`-annotated `Battery`, an `enum def FlightMode` with literal values, and a
`FlightTimeRequirement` with a `subject`/`require constraint` body, and regenerated.

**Explicitly deferred (not delivered here):** full expression-tree modeling of constraint bodies
(raw text capture only); a nested `doc` annotation inside a constraint's own `calculationBody`
(e.g. `assume constraint { doc /* ... */ ... }`, seen in the OMG training corpus) is intentionally
not captured — an accepted scope boundary, not an oversight; any other view kind besides General
View.

### Action Flow View: control-node/successor AST correctness + fork/join/decision/merge shapes — delivered

**Delivered.** `AstBuilder.VisitActionBodyItem` now synthesizes a `SysmlTransitionNode` for both
combined-shape `actionBodyItem` alternatives — `initialNodeMember (actionTargetSuccessionMember)*`
and `(sourceSuccessionMember)? actionBehaviorMember (actionTargetSuccessionMember)*` — reusing the
`MultiNodeCapture` sentinel introduced for the State Transition View fix, so the compact `action
a1; then a2;` idiom and multiple attached successions now resolve correctly (previously both the
node and its successor were silently dropped). `VisitMergeNode`/`VisitDecisionNode`/
`VisitJoinNode`/`VisitForkNode`/`VisitAcceptNode`/`VisitSendNode` build minimal `SysmlFeatureNode`s
with `FeatureKeyword` values `"merge"`/`"decide"`/`"join"`/`"fork"`/`"accept"`/`"send"`, giving
anonymous control nodes (the dominant idiom in the OMG training corpus) a synthesized `$<keyword>
<n>` internal name so their successions still wire correctly.
`ActionFlowViewLayoutStrategy` now renders fork/join as a `LayoutBadge(BadgeShape.HorizontalBar)`
and decision/merge as a `LayoutBadge(BadgeShape.Diamond)`; accept/send keep the rounded-rectangle
box shape (no pentagon primitive exists in the referenced `DemaConsulting.Rendering` package) but
now show their own `Keyword` instead of the hard-coded `"action"` text. Ordinary-action rendering
is unchanged. The CI/CD Pipeline gallery model (`docs/gallery/models/04-pipeline-action-flow.sysml`)
was extended with a named `fork`/`join` pair and the compact succession idiom, and regenerated.

**Explicitly deferred (separate future items, not delivered here):** a true pentagon accept/send
shape (needs a `DemaConsulting.Rendering` package change); swim-lanes via `LayoutBand`; item-flow
edge annotations (no item-flow/payload capture exists in the AST yet); `assignmentNode`/
`terminateNode`/`ifNode`/`whileLoopNode`/`forLoopNode` AST support (still entirely unmodeled); an
`actionUsage`-level (rather than `action def`-level) nested `actionBody` — `VisitActionUsage`
still doesn't collect nested action-body children, so control nodes/successions nested inside a
`action x : T { ... }` *usage* (as opposed to an `action def X { ... }` body) remain invisible;
the Sequence View dynamics item below (different subsystem, separate branch).

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

**Phase 2b — narrowed (bare `istype`/`hastype` confirmed not applicable):** the grammar
(`SysMLv2Parser.g4`'s `ownedExpression`) only defines `istype`/`hastype` as **binary** postfix
operators requiring an explicit left operand (`ownedExpression (ISTYPE|HASTYPE) typeReference`,
e.g. the corpus's `engine istype '6CylEngine'`) — there is no bare/self-referential form, unlike
`@Type`'s genuine prefix production (`(AT_SIGN|AT_AT) typeReference`). A `filter`/bracket-filter
predicate has no bound variable to serve as that left operand, so `istype`/`hastype` are not
expressible in filter-expression position at all under the actual SysML v2 grammar — confirmed
by grep across the full 251-file OMG corpus finding zero `filter`/`expose[...]` usages of either
keyword. This part of Phase 2b remains correctly deferred (not a gap — there is nothing to
implement). Arithmetic operators, conditional (`if`/`else`) expressions, and general feature-chain
navigation remain deferred unchanged (zero corpus evidence in filter position).

**Phase 2d — delivered.** Both coupled fixes landed:

1. **Metaclass/kind classification-test matching.** `FilterExpressionEvaluator` now matches a
   classification test (`@Type`/`@Pkg::Type`) when *either* the existing applied-annotation match
   (`FindMetadata`) succeeds *or* the candidate's own AST node kind —
   `SysmlDefinitionNode.DefinitionKeyword`/`SysmlFeatureNode.FeatureKeyword` — maps to the
   requested metaclass, via a new `MetaclassNames` keyword→bare-metaclass-name table covering
   every keyword this project's `AstBuilder` models that has a corresponding stdlib
   `metadata def` declaration (part/attribute/item/port/action/state/requirement/constraint/
   interface, and their `def`-suffixed definition counterparts). `allocation`/`view`/`viewpoint`
   keywords are NOT in this table — they are captured by dedicated `SysmlConnectionNode`/
   `SysmlViewNode`/`SysmlViewpointNode` node types rather than `SysmlDefinitionNode`/
   `SysmlFeatureNode`, so they remain out of scope by construction (see known gaps below), even
   though their stdlib metaclasses (`AllocationDefinition`/`ViewDefinition`/`ViewpointDefinition`
   etc.) do exist. Both bare (`@PartUsage`) and `SysML::`-qualified
   (`@SysML::PartUsage`) spellings match — note the table stores *bare* names and compares the
   requested `typeName` against `"SysML::" + name` directly, since the stdlib's actual declared
   qualified name (`SysML::Systems::PartUsage`) does not literally match the corpus's
   two-segment convention, an investigation finding that shaped the table's design. The
   specialization-conformance stretch goal (Investigation §2) was also delivered: a filter naming
   an ancestor metaclass (e.g. `@ConstraintUsage`) also matches a more specific stdlib-derived
   kind (e.g. a `requirement` usage, via `RequirementUsage specializes ConstraintUsage`) — walked
   via each stdlib metaclass declaration's raw, unresolved `SupertypeNames` text (not the
   resolved `Supertype` edge originally envisioned, since stdlib-only nodes are never passed
   through `ReferenceResolver.ResolveAll` and so never have resolved edges), resolved to a
   declaring stdlib node by a same-simple-name suffix lookup in `SysmlWorkspace.Declarations`, and
   cycle-guarded with a visited-name set. This is a deliberately narrow heuristic, not a general
   reference-resolution mechanism, and is documented as such in code.

   **Known, documented gaps** (a modeled keyword with no stdlib metaclass counterpart, so no entry
   exists in `MetaclassNames` for it): `individual def`; raw KerML classifier keywords
   (`datatype`/`class`/`struct`/`assoc`/`assoc struct`/`function`/`predicate`);
   `subject`/`actor`/`stakeholder`; bare `enum value` members; control-node keywords
   (`merge`/`decide`/`join`/`fork`); `assume constraint`/`require constraint` (deliberately not
   folded into the generic `ConstraintUsage`, to avoid over-claiming semantics not evidenced in the
   stdlib); `entry`/`do`/`exit` (deliberately not mapped to `ActionUsage`). Also out of scope by
   construction, since these use dedicated node types rather than
   `SysmlDefinitionNode`/`SysmlFeatureNode`: `SysmlConnectionNode` (`connection`/`allocation`/
   `binding`/`message`) and `SysmlViewNode`/`SysmlViewpointNode` (`view def`/`view`/
   `viewpoint def`/`viewpoint`).

2. **Usage-level filter candidates.** `GeneralViewLayoutStrategy.CollectDefinitions` — which
   doubles as both the filter-candidate source and the box-rendering function for the entire
   General View diagram — now admits named `SysmlFeatureNode` usages alongside
   `SysmlDefinitionNode`s (`BuildCompartments`/`CollectMemberships` were generalized from
   `SysmlDefinitionNode` to the common `SysmlNode` base type to support this, mechanically, with
   no logic change). `ExposeScopeResolver`'s bracket-filter candidate query was widened
   identically. This unconditional admission **was** a real, unguarded regression in its initial
   form: rendering every admitted usage as a standalone box, regardless of nesting depth, more
   than doubled the box count of real corpus models (e.g. the gallery's
   `01-drone-general.sysml`'s `DroneGeneralView`, 21 → 47 `<rect>` boxes) by duplicating nested
   usages already shown as compartment rows inside their owning definition/usage's box — found by
   quality re-validation and fixed in Retry 1 (see below). The OMG Safety feature-views fixture's
   exposed-vehicle-subtree scope, previously documented as intentionally empty, now renders the
   vehicle's part usages (still excluding `Safety`/`Security`).

   **Retry 1 fix (quality regression).** Added `GeneralViewLayoutStrategy.RemoveRedundantNestedUsages`,
   called in `BuildLayout` after the standalone `filter [<expr>];` narrowing and before
   `GroupByPackage`: it excludes a usage-level box from standalone rendering when its immediate
   parent's qualified name is also present in the final, fully scope-and-filter-narrowed set (i.e.,
   the usage would duplicate content already shown in the parent's compartment row).
   Definitions are never excluded by this rule. Re-verified `docs/gallery/models/01-drone-general.sysml`'s
   `DroneGeneralView` renders exactly 21 boxes again, matching the checked-in
   `docs/gallery/svg/DroneGeneralView.svg` (now a standing automated regression-guard test), while
   the `filter @SysML::PartUsage;`/bare `@PartUsage` metaclass-kind filter tests and the OMG Safety
   fixture test continue to pass unchanged.

   **Retry 2 fix (quality regression).** A second quality re-validation found that Retry 1's
   single-pass, snapshot-based dedup silently dropped a usage nested **two or more levels** deep
   (e.g. `part def A { part b { part c; } }`): `c`'s immediate parent `b` was tested against the
   pre-removal snapshot rather than the actually-surviving set, so `c` was excluded alongside `b`
   even though `b` no longer rendered anywhere and `BuildCompartments` never shows a grandchild's
   row — `c` vanished from the diagram entirely, not shown as its own box nor inside any surviving
   compartment. `RemoveRedundantNestedUsages` was reworked to process usage-level boxes in
   ascending qualified-name-depth order (fewest `::` occurrences first) while incrementally
   building the `excluded` set, so a usage's immediate-parent presence test reflects whether the
   parent itself survives dedup rather than merely whether it appeared in the pre-dedup snapshot.
   This cascades correctly through any nesting depth in a single ordered pass: `b` is excluded
   before `c` is tested, so `c`'s immediate parent is no longer "present" by the time `c` is
   considered, and `c` correctly survives as its own standalone box. Added a new 3-level regression
   test (`GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersDeeplyNestedGrandchildUsageWhenIntermediateParentExcluded`)
   asserting exactly 2 rendered boxes (`A` and `c`, with `b` excluded and never silently lost).
   Re-confirmed the existing 21-box gallery regression guard, all three Retry 1 tests, and the
   primary `@SysML::PartUsage`/`@Safety`/OMG Safety fixture tests still pass unchanged.

**Scope:** `DemaConsulting.SysML2Tools.Core.Filtering.FilterExpressionEvaluator` (new
`MetaclassNames`/`MatchesMetaclassKind`/`MetaclassNameMatches`/`ConformsToMetaclass`, workspace
threaded through the private evaluation recursion); `GeneralViewLayoutStrategy.CollectDefinitions`/
`BuildCompartments`/`CollectMemberships`; `ExposeScopeResolver.ResolveExposedScope`'s bracket-filter
candidate query.
**Visual gate — met.** `filter @SysML::PartUsage;` (or bare `@PartUsage`) against a model with
mixed part/requirement/other usages renders only the `PartUsage` elements — matching the OMG's own
`42.Views/ViewsExample.sysml` corpus pattern (regression-tested in
`GeneralViewLayoutStrategyTests`). Existing definition-level domain-metadata filtering
(`filter @Safety;`, `filter @Safety and (as Safety).isMandatory;`) continues to work identically
(unaffected by the new OR-combined metaclass-kind match path).

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

### Tool package size (confirmed: 435 MB, over NuGet.org's ~250 MB limit)

Packing `DemaConsulting.SysML2Tools.Tool` locally confirmed a **434.94 MB** `.nupkg` — well past
NuGet.org's hard size limit. Inspecting the archive found three compounding, independently
fixable causes:

1. **Native SkiaSharp debug symbols ship in the package.** `761.7 MB` (57%) of the uncompressed
   content is `libSkiaSharp.pdb` (Windows x86/x64/arm64) — third-party native debug symbols nobody
   debugs into. These arrive as ordinary content files from the transitive `SkiaSharp` package (via
   `DemaConsulting.Rendering.Skia`) and are not excluded today. **Fix:** add an MSBuild target
   (e.g. `BeforeTargets="_GetPackageFiles"` or the tool-packing equivalent) that filters
   `**/native/*.pdb` out of the packed file list.
2. **Full triplication from multi-targeting.** `Tool` targets `net8.0;net9.0;net10.0`, and
   `PackAsTool` stages a complete, independent `tools/{tfm}/any/runtimes/**` tree per TFM — tripling
   every native asset. **.NET 9 is STS and already out of support**; `.NET 8` (LTS, supported to
   Nov 2026) and `.NET 10` (current LTS) are the only versions worth shipping. **Decision: target
   `net10.0` only** for the `Tool` package (single TFM, maximal size reduction; `Core`/`Language`/
   `Stdlib` remain multi-targeted `net8.0;net9.0;net10.0` since they're ordinary libraries consumed
   by projects on any of those TFMs — this change is scoped to `Tool` only).
3. **Untrimmed RID matrix.** The transitive `SkiaSharp` native-asset packages bundle every RID
   SkiaSharp supports, including ones this project doesn't need to support (`linux-loongarch64`,
   `linux-bionic-*`, `linux-musl-loongarch64`, etc.). The `Tool.csproj`'s own OS-conditioned
   `SkiaSharp.NativeAssets.{Win32,Linux.NoDependencies,macOS}` references are effectively
   redundant/dead — the full matrix arrives unconditionally via `DemaConsulting.Rendering.Skia`
   regardless of host OS. **Fix:** pin an explicit, curated RID list (Windows x64/x86/arm64, Linux
   x64/arm64 + musl x64/arm64, macOS x64/arm64) so only supported platforms are packaged.

**Scope:** `src/DemaConsulting.SysML2Tools.Tool/DemaConsulting.SysML2Tools.Tool.csproj` only.
**Visual/verification gate:** `dotnet pack` the `Tool` project and confirm the resulting
`.nupkg` is comfortably under NuGet.org's limit (target: well under 100 MB); the packed tool must
still install and render correctly (SVG + PNG) on at least the currently-tested OS matrix
(Windows/Linux/macOS) — this is exactly what the "Package validation gate" tool smoke test above
already covers, so it doubles as the regression check for this fix.

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
