# SysML2Tools Roadmap — Road to 0.1.0

This roadmap supersedes the previous rendering roadmap. The original phases (0–12) that
stood up the parser, semantic model, `LayoutTree`, the five layout engines, and all seven
implemented view types are **complete**. This document defines the work remaining to reach a
**0.1.0 release**: conforming the rendered output to SysML v2 graphical notation, completing
the remaining view dynamics, and finishing release packaging. (The eighth view — Geometry — is
deferred to 0.2.0; see §6.)

---

## 1. Release Goal — Definition of Done for 0.1.0

0.1.0 is reached when all of the following hold:

- **All 7 implemented view types render** with notation fidelity. (The 8th view, Geometry, is
  deferred to 0.2.0 and documented as not yet supported — see §6 and Phase 17.)
- **Graphical-notation conformance**: rendered connectors, node shapes, compartments, and
  annotations match the SysML v2 graphical notation (see §3) closely enough that a diagram is
  recognizable to a SysML v2 practitioner — verified by visual inspection against the OMG
  reference templates and training material.
- **View dynamics complete**: sequence activation bars and combined fragments; action
  fork/join/merge/decision nodes and swim-lanes.
- **Release packaging**: third-party license/attribution output (`--licenses`), per-package
  README notes (incl. SkiaSharp native assets), the `docs/rendering` techniques document, a
  regenerated gallery, version metadata, and release notes.
- **Green CI**: build/tests on net8/9/10, full `lint.ps1`, ReqStream `--enforce`, ReviewMark.

Each phase below is independently reviewable and ships behind the same quality gates.

---

## 2. Completed Foundation (pre-0.1.0)

| Area | Status |
|---|---|
| Parser + OMG stdlib (ANTLR4) | ✅ Complete |
| Semantic model (symbol table, reference resolution, supertype walking) | ✅ Complete |
| `LayoutTree` intermediate representation (9 node types) | ✅ Complete |
| Layout engines: `ContainmentPacker`, `ChannelRouter`, `ForceDirectedEngine`, `PortAssigner`, `LayeredLayoutEngine` | ✅ Complete |
| General View (definitions, compartments, specialization edges, folder packages) | ✅ Complete |
| Interconnection View (force-directed parts, ports, connectors) | ✅ Complete |
| State Transition View (states, guarded transitions, initial marker) | ✅ Complete |
| Action Flow View (layered actions, start/done markers, branches) | ✅ Complete |
| Sequence View (lifelines + messages) | ✅ Complete |
| Grid View (relationship/specialization matrix) | ✅ Complete |
| Browser View (membership tree) | ✅ Complete |
| SVG + PNG renderers; Light/Dark/Print themes | ✅ Complete |
| `--auto`, `--validate`/self-test, depth limiting | ✅ Complete |
| Connector routing quality (clearance-retry, perpendicular stubs, label placement, warnings) | ✅ Complete |
| Diagram gallery (`docs/gallery/`) | ✅ Complete |
| Formal requirements/design/verification + ReviewMark for the rendering subsystem | ✅ Complete |

**Already notation-correct** (verified, not gaps): Definition nodes render as **plain
rectangles** and Usage nodes as **rounded rectangles**; packages render with the **folder-tab**
shape; specialization edges use a **hollow triangle** at the supertype.

---

## 3. SysML v2 Graphical Notation Reference

This is the authoritative notation reference for the conformance phases. It is derived from the
OMG SysML v2 release materials. Detailed source notes: `docs/.../files/omg-notation-research.md`
in session artifacts; primary sources below.

### 3.1 Reference sources (where ground truth lives)

The `Systems-Modeling/SysML-v2-Release` repository's `doc/images/sysml/` directory is a **mix**:

| Location | What it is | Use |
|---|---|---|
| `doc/images/sysml/fig-08/15/22/23/24/30/33…` | Metamodel **class diagrams** (MagicDraw/Batik) | ❌ NOT user notation |
| Higher worked `fig-*` (e.g. `fig-70` requirements group) | Genuine **notation examples** | ✅ Ground truth |
| `doc/images/sysml/clause-8.2.3/` | Concrete-syntax **notation templates** (Inkscape), one per element | ✅ Primary template source |
| `doc/images/sysml/clause-7/` | Per-row **kernel notation tables** with real examples | ✅ Ground truth |
| `doc/Intro to the SysML v2 Language-Graphical Notation.pdf` | Dedicated graphical-notation **training** doc | ✅ Best worked examples |

### 3.2 Node shapes

| Element class | Shape | Status |
|---|---|---|
| Any **Definition** (Part/Action/State/Port/Attribute… def) | Plain rectangle (sharp corners) | ✅ Done |
| Any **Usage** (part/action/state/connection/interface…) | Rounded rectangle (cubic-bezier corners) | ✅ Done |
| **Package** | Rectangle with folder tab | ✅ Done |
| **Documentation / Comment** | Note shape (folded-corner rectangle) | ⬜ `BoxShape.Note` defined, unused |
| **Accept action** | Rounded rectangle + filled concave "receive" pentagon | ⬜ |
| **Fork / Join** | Thick solid bar | ⬜ |
| **Decision / Merge** | Diamond | ⬜ |
| **Actor** (use-case lifeline head) | Stick figure | ⬜ |

The Definition/Usage distinction is encoded **entirely** in box shape — keyword/compartments
distinguish *kinds*, the rounded-vs-sharp corner distinguishes def-vs-usage.

### 3.3 Connector ends and line styles (the conformance core)

End shapes sit at the **owner/target** end as indicated; lines are solid unless noted.

| Relationship | Line | End shape | At end | Status |
|---|---|---|---|---|
| Specialization / subclassification | solid | hollow **triangle** | supertype | ✅ Done |
| Redefinition (usage→usage) | solid | hollow triangle **+ ⊥ crossbar** near tip | redefined | ⬜ |
| Composite feature membership (owned/nested) | solid | **filled diamond** | owner | ⬜ (`ArrowheadStyle.FilledDiamond` defined, unused) |
| Reference feature membership (e.g. requirement `subject`) | solid | **hollow diamond** | owner | ⬜ (`ArrowheadStyle.Diamond` defined, unused) |
| Connection / connector (interconnection) | solid | none (ends may show multiplicity) | — | ✅ Done (plain line) |
| Succession (action flow) | **dashed** | **open V** (stroke-only) | target | ⚠️ Solid + filled today |
| State transition | solid | **open V** (verify vs training PDF) | target | ⚠️ Filled today |
| Sequence message | solid | **open V** (stroke-only) | receiver | ⚠️ Filled today |
| Dependency | dashed | open V | target | ⬜ |

Subsetting / feature-typing in the templates are frequently shown **textually** in
compartments (`:>`, `:>>`) rather than as separate edges — match that convention.

### 3.4 Typography and color

- Body labels Arial/sans **12px**; state entry/do labels **11px**; sequence message labels
  **10px**. (Our themes use an embedded Noto Sans; sizes are theme-driven — verify ratios.)
- Spec diagrams are **black on white**, no fill color. Our themes add subtle fills by depth;
  a **Print** theme already approximates the spec's monochrome look.

### 3.5 Compartments

`«keyword» Name` in the name compartment (bold; keyword in guillemets), then stacked,
separator-lined compartments: e.g. state `entry/`, `do/`, `exit/`; requirement `doc`,
`attributes`, `constraints`, `subject`, `references`; part `attributes`, `ports`, `parts`.
We render attributes/ports/parts today; deeper compartments are a gap (see Phase 15).

---

## 4. Phase Gate (every phase must satisfy)

Each phase is delivered on its own feature branch and merged via PR only after **all** of the
following gates pass. A phase is not "done" until the feature **and** its supporting
documentation ship together in the same PR.

### 4.1 Automated quality gates (all must pass)

- `pwsh ./build.ps1` — solution builds and all unit tests pass on **net8.0, net9.0, net10.0**,
  zero errors, zero warnings (analyzers are warnings-as-errors).
- **Targeted unit tests** added for the phase's new behavior, each linked from a requirement.
- `pwsh ./lint.ps1` exits 0 — markdownlint-cli2, cspell (US English), yamllint, `dotnet format`,
  ReqStream `--lint`, VersionMark, ReviewMark.
- **ReqStream `--enforce`** against fresh test results — every new/changed requirement traces to
  a passing test at its own level.
- **ReviewMark `--lint`** — all review-sets resolve; every new source/doc file is assigned to a
  review-set.

### 4.2 Multimodal LLM visual inspection

Coordinate-arithmetic tests cannot see "the arrowhead is filled instead of open" or "the
connector grazes a box." Every rendering phase therefore includes a visual gate performed by the
implementing agent:

1. Publish the tool and render the affected **gallery models and targeted test models** to PNG
   (and, for SVG-specific behavior, convert the SVG → PNG so the vector output is inspected as
   rendered).
2. The agent reads each image back with the multimodal `view` tool and checks the phase's
   **specific visual criteria** (listed per phase) against the §3 notation reference and the OMG
   `clause-8.2.3` templates / training PDF.
3. Record pass/fail per criterion; fix and re-render until all pass.
4. Temporary `_check/` artifacts are deleted and never committed.

### 4.3 Supporting-documentation updates (in the same PR, as applicable)

| Artifact | Update when | Standard |
|---|---|---|
| **Requirements** (`docs/reqstream/…`) | any new observable behavior | generic WHAT; link to a passing test |
| **Design** (`docs/design/…`) | any new behavior | the HOW (algorithms, shapes, dispatch) |
| **Verification** (`docs/verification/…`) | any new behavior | test scenarios + acceptance criteria |
| **ReviewMark** (`.reviewmark.yaml`) | new units/files | per-unit + subsystem review-sets |
| **Wiring** (`requirements.yaml`, design/verification `definition.yaml`) | new doc files | include the new files |
| **README** | user-visible capability/feature change | keep feature claims accurate |
| **User Guide** (`docs/user_guide/`) | CLI option / behavior / output change | reflect actual usage |
| **Gallery** (`docs/gallery/`) | any visible rendering change | regenerate affected diagrams + captions |
| **Rendering doc** (`docs/rendering/`, from Phase 17) | notation/technique change | update notation + technique sections |

### 4.4 Process gates (run for every phase — not just the last)

Before each phase's PR, in order:

1. **Validate** — automated gates (§4.1) and the multimodal visual gate (§4.2) all pass.
2. **change-review agent** — run the built-in change-review agent on the phase diff and address
   any egregious findings. Running it every phase keeps PR review comments small and catches
   issues while context is fresh.
3. **lint-fix agent** — run the built-in lint-fix agent so `lint.ps1` passes and CI does not fail
   on formatting/spelling on first run.
4. **Re-validate & open the PR** — branch + PR; **no direct commits to `main`**.

**Release notes are generated from commit messages via the build notes** — there is no
`CHANGELOG`/`CHANGES.md`; write clear, descriptive commit messages so the generated notes are
useful.

### 4.5 Execution & model strategy (sub-agent delegation)

Phases are run by an orchestrator that **delegates each task to a sub-agent launched with an
explicitly chosen model**. Default to the cheaper driver; escalate only where deeper reasoning
earns its cost. This is safe because the §4 gates are objective — they catch regressions, back-
driven requirements, and notation slips regardless of which model produced the work.

| Task | Sub-agent | Model |
|---|---|---|
| Feature implementation (shapes/edges/line-styles/strategies) | developer / general-purpose | **Driver** (e.g. Sonnet 4.6) |
| Doc authoring (requirements/design/verification, README, user guide) | developer / general-purpose | Driver |
| Self-validation tests + package-validation script | developer | Driver |
| Multimodal visual inspection (render → `view` → judge vs §3) | general-purpose (multimodal) | Driver; escalate if not converging |
| Layout/geometry debugging that does not converge | general-purpose | **Escalation** (e.g. Opus 4.8) |
| Per-phase change-review gate (§4.4) | code-review | **Strong reviewer** (e.g. Opus 4.8) |
| Lint cleanup (§4.4) | lint-fix | Driver |

Rules:

- The orchestrator **names the model explicitly** when launching each sub-agent (model override).
- Escalate the driver to the stronger model only after ~2 inspect-fix iterations fail to resolve
  a visual/geometry bug on the cheaper model.
- The **change-review gate always runs on the strong model** — it is the safety net for cheaper-
  driver output, and keeps PR review comments minimal.
- **Notation judgment calls where the OMG sources conflict** (e.g. open-V vs filled arrowheads)
  are surfaced to the maintainer for a decision — not resolved autonomously by any model.

---

## 5. Release Phases

Each phase below lists its **scope** and its phase-specific **visual criteria**; all phases
additionally satisfy the §4 Phase Gate (automated + multimodal + docs + process).

### Phase 13 — Connector-end & line-style conformance

Bring routed connectors into line with §3.3 — the highest-value, broadest-impact change.

- Wire the already-defined arrowhead markers to relationships: **filled/hollow diamonds** for
  composite/reference membership; **redefinition** crossbar variant.
- Switch succession/transition/message arrowheads to the spec style (**open V**, stroke-only)
  and make **successions dashed** — pending confirmation against the training PDF; keep a theme
  switch if the training material differs from `clause-8.2.3`.
- Add `LineStyle.Dashed` usage where required; ensure arrowheads render correctly at clearance
  stubs.

**Scope:** `LayoutLine` arrowhead/line-style assignments in the view strategies; renderer
marker defs (already present). No new engines.
**Visual gate:** state/action/sequence/general galleries match §3.3 end shapes; membership
diamonds appear where membership is shown.

### Phase 14 — Layout Engine v2

Replace the ad-hoc placement and heat-expansion logic with a principled, axis-symmetric
layout algorithm. This phase ships before any further notation or view-dynamics work
because every subsequent phase adds edges and shapes that make routing congestion worse;
building on a correct foundation avoids retroactive layout fixes.

The full algorithm is specified in **`docs/layout/`** (compiled to
`docs/generated/SysML2 Tools Layout Guide.pdf`). Summary of changes:

**New engines** (`Layout/Engine/`):
- `ConnectivityAnalyzer` — affinity matrix, layer hints, cluster membership, barycenter
  crossing-minimisation
- `HighwayAssigner` — global routing on coarse grid; channel scoring; highway
  classification and edge assignment; cost-discount map for `ChannelRouter`
- `GravityCompressor` — oversized-to-minimum compression loop; both-axis, monotone,
  clearance-floored
- `GridQuantizer` — G-aligned position/size snapping; column-width and row-height
  unification

**Extended engines**:
- `ForceDirectedEngine` — anisotropic hierarchy gravity `k_hier`; wire-pressure force;
  kinetic energy as termination signal
- `LayeredLayoutEngine` — Monte Carlo multi-seed option; per-seed crossing count
- `ChannelRouter` — per-cell cost-multiplier map for highway discounts

**Strategy changes**:
- `GeneralViewLayoutStrategy` — full replacement of placement phase (remove
  `DetectRows`, `MeasureVerticalBandHeat`, `ApplyPerBandYShifts`, heat loop); wire new
  pipeline (Free 2D mode)
- `ActionFlowViewLayoutStrategy` — adopt Directed Flow mode with strong hierarchy gravity
  and back-edge arc routing
- `StateTransitionViewLayoutStrategy` — same as Action Flow strategy
- `InterconnectionViewLayoutStrategy` — retain existing force-directed placement;
  add `GravityCompressor` and `GridQuantizer` passes

**Documentation**: `docs/layout/` contains the design specification for this phase and
ships as a compiled PDF alongside the implementation. SVG illustrations for each algorithm
stage are committed under `docs/layout/images/`.

**Scope:** four new engines; three extended engines; three strategy rewrites; one strategy
minor update; `docs/layout/` document.
**Visual gate:** DroneGeneralView and all gallery models show compact balanced layout with
no excessive whitespace; TrafficLightStates and OrderActionFlow show clean top-to-bottom
flow; no regression on any existing gallery model.

### Phase 15 — Additional relationship edges (General View)

Render the relationships currently omitted from the General View, each routed via
`ChannelRouter` and carrying the correct §3.3 end shape:

- Redefinition, subsetting (where shown as edges), feature typing, dependency, containment,
  connection/binding, allocation.
- Shared-bus generalization (multiple subtypes merging into one line to a supertype) as an
  optional readability refinement.

**Scope:** `AstBuilder`/semantic exposure of the relationship kinds as needed;
`GeneralViewLayoutStrategy` edge emission; resolver coverage.
**Visual gate:** a model exercising each relationship renders distinct, correctly-headed edges.

### Phase 16 — Annotating elements & compartment depth

- Render **Documentation/Comment** notes as `BoxShape.Note` (folded-corner) nodes attached to
  their annotated element.
- Extend compartments to spec depth: enumeration values, constraint bodies, requirement
  `subject`/`constraints`/`doc`, and a documentation compartment on definitions/usages.

**Scope:** semantic exposure of doc/comment + compartment content; `GeneralViewLayoutStrategy`
and renderers; possibly `LayoutLabel`/compartment tweaks.
**Visual gate:** a documented requirement/part renders its note and full compartments.

### Phase 17 — View dynamics refinements (was Phase 16)

- **Sequence View:** populate `LayoutActivation` execution bars; combined-fragment boxes
  (alt/opt/loop); async/reply message styling.
- **Action Flow View:** **fork/join** thick bars, **decision/merge** diamonds, accept/send
  action shapes; optional **swim-lanes** via `LayoutBand`; item-flow edge annotations.

**Scope:** `SequenceViewLayoutStrategy`, `ActionFlowViewLayoutStrategy`, renderer shape
primitives (bar, diamond, pentagon, note). `LayoutActivation`/`LayoutBand` already defined.
**Visual gate:** sequence shows activation bars + a fragment; action flow shows a fork/join and
a decision/merge with correct shapes.

### Phase 18 — Release readiness (was Phase 17)

**Self-validation suite (expand from 3 to ~12 tests).** Downstream projects run
`sysml2tools --validate` in their own environment as tool-qualification evidence, and the
win/mac/linux integration-test matrix runs it per-OS. Tests follow the DEMA naming convention
`SysML2Tools_{Capability}` (tool prefix + descriptive capability) for instant recognition in
per-OS evidence. Rename the existing three (drop the redundant `SelfTest` suffix) and add the
rest; each render test emits **both `.svg` and `.png`** and asserts output validity, so SkiaSharp
native assets are exercised on every view and every OS:

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

**Package Validation gate (automated, before publish).** `build.ps1`/`lint.ps1` validate the
source but not the produced packages — add a repeatable check that:

1. `dotnet pack` all four packages → unzip each `.nupkg` and assert contents (expected DLLs,
   license file, third-party notices incl. Noto Sans OFL, README, icon, correct dependencies and
   metadata; `dotnet pack` warnings-as-errors).
2. **Tool smoke test:** install the packed tool from a local feed into a clean directory → run
   `--version`, render a sample to **both SVG and PNG** (PNG proves SkiaSharp natives resolve),
   and `--licenses`.
3. **Library-consumer smoke test:** a throwaway project referencing each library package from the
   local feed → restore → exercise parse→layout→render-to-SVG-in-memory and render-to-PNG (again
   proving SkiaSharp natives for `.Png` consumers — documented concern #3).

**Licensing/attribution:** `--licenses` output covering Noto Sans (SIL OFL 1.1) and other OTS;
per-package README notes incl. the SkiaSharp native-assets requirement for
`DemaConsulting.SysML2Tools.Png` consumers.

**Documentation:** the **README and User Guide must state that the Geometry View is not yet
supported** (planned for 0.2.0). Finalise `docs/layout/` (the layout algorithm
reference, authored during Phase 14) — add the §3 notation-conventions table, an
invariants/gotchas section, and any remaining SVG illustrations; wire into CI
(`build.yaml`, `.fileassert.yaml`, `.reviewmark.yaml`).

**Gallery & packaging:** regenerate the gallery against the final notation and refresh
`docs/gallery/README.md`; set version metadata, package descriptions/icons/tags, and 0.1.0
release notes (generated from commit messages via the build notes — no `CHANGELOG`/`CHANGES.md`);
confirm `dotnet tool install` and library-package consumption paths.

**Gate:** the self-validation suite passes on all three OSes; the package-validation script passes
(tool installs and renders SVG + PNG; library consumer renders PNG); `--licenses` lists OFL text;
README/User Guide note Geometry as unsupported; gallery reflects Phase 13–17 notation.

### Phase 19 — 0.1.0 Release (was Phase 18)

Final full-suite validation; tag `v0.1.0`; publish the four NuGet packages; create the GitHub
release with notes and gallery highlights. **Publishing requires maintainer authorization
(credentials, irreversible) — prepared to the edge of publish, then handed off.**

---

## 6. Deferred Beyond 0.1.0

These remain explicitly out of the 0.1.0 scope unless pulled forward:

- **Geometry View (0.2.0)** — the 8th view: 2D spatial placement (3D projected to 2D) of items
  whose spatial coordinates are specified in the model via the SysML geometry/spatial library.
  Deferred because it requires new semantic capability (extracting numeric attribute *values*,
  not just structure) and a coordinate convention plus test models that use it. **0.1.0 must
  document the Geometry View as not yet supported in the README and User Guide** (see Phase 17).
- **SARIF** diagnostic output (`SysmlDiagnostic` is already structurally compatible).
- **Loadable theme files** (YAML/JSON) — the `Theme` record is forward-compatible.
- **`export` verb** / additional output formats.
- **3D Geometry** rendering (2D projection only, even once Geometry ships in 0.2.0).
- **Nested state regions** and other advanced behavioral notation.

---

## 7. Layout Engine Architecture (reference)

Five reusable, stateless engines in `Layout/Engine/` accept plain geometric input (no SysML
model references) and return computed geometry; each is independently unit-tested.

| Engine | Capability | Used by |
|---|---|---|
| `ContainmentPacker` | Bottom-up sizing + bin-packing of children in a container | General, Interconnection |
| `ChannelRouter` | Orthogonal edge routing around obstacles, clearance-retry, perpendicular stubs | General, Interconnection, State, Action |
| `ForceDirectedEngine` | Fruchterman-Reingold spring placement | Interconnection, State |
| `PortAssigner` | Port-side assignment + slot distribution along an edge | Interconnection |
| `LayeredLayoutEngine` | Simplified Sugiyama (layering + barycenter ordering + x-alignment) | Action Flow |

Helper units: `ConnectorLabelPlacer` (collision-aware label placement), `LayoutWarnings`
(layout diagnostics), `BoxMetrics` (sizing). Sequence/Grid/Browser/Geometry layouts are pure
arithmetic in their strategies (no engine).

---

## 8. LayoutTree Vocabulary Coverage (reference)

| Primitive | Status |
|---|---|
| `LayoutBox`, `LayoutLabel`, `LayoutLine`, `LayoutCompartment`, `LayoutPort`, `LayoutLifeline`, `LayoutBadge`, `LayoutGrid` | ✅ Rendered |
| `LayoutActivation` (sequence bars) | ✅ Defined — populated in Phase 16 |
| `LayoutBand` (swim-lanes) | ✅ Defined — populated in Phase 16 |

Arrowhead vocabulary (`ArrowheadStyle`): `None`, `Open`, `Filled`, `Diamond`, `FilledDiamond`,
`Circle`, `Bar` — all marker defs exist in both renderers; `Diamond`/`FilledDiamond` and the
open-V style are wired to relationships in Phases 13–14.
