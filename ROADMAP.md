# SysML2Tools Roadmap

This roadmap defines the work remaining to reach a **0.1.0 release** — conforming the rendered
output to SysML v2 graphical notation, completing the remaining view dynamics, and finishing
release packaging — followed by the **0.2.0** direction: turning SysML2Tools into an
AI-assistable *model query and analysis* tool, not just a renderer.

Completed work (the parser, semantic model, `LayoutTree`, the layout engines, the seven
implemented view types, the SVG/PNG renderers, and Layout Engine v2 including highway routing
and approach-zone connector clarity) has been removed from this document to keep it
forward-looking. That history lives in the git log and the generated build/release notes. The
eighth view — Geometry — is deferred to 0.2.0; see §5.

---

## 1. Release Goal — Definition of Done for 0.1.0

0.1.0 is reached when all of the following hold:

- **All 7 implemented view types render** with notation fidelity. (The 8th view, Geometry, is
  deferred to 0.2.0 and documented as not yet supported — see §5 and Phase 17.)
- **Graphical-notation conformance**: rendered connectors, node shapes, compartments, and
  annotations match the SysML v2 graphical notation (see §2) closely enough that a diagram is
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

## 2. SysML v2 Graphical Notation Reference

This is the authoritative notation reference for the conformance phases. It is derived from the
OMG SysML v2 release materials. Detailed source notes: `docs/.../files/omg-notation-research.md`
in session artifacts; primary sources below.

### 2.1 Reference sources (where ground truth lives)

The `Systems-Modeling/SysML-v2-Release` repository's `doc/images/sysml/` directory is a **mix**:

| Location | What it is | Use |
|---|---|---|
| `doc/images/sysml/fig-08/15/22/23/24/30/33…` | Metamodel **class diagrams** (MagicDraw/Batik) | ❌ NOT user notation |
| Higher worked `fig-*` (e.g. `fig-70` requirements group) | Genuine **notation examples** | ✅ Ground truth |
| `doc/images/sysml/clause-8.2.3/` | Concrete-syntax **notation templates** (Inkscape), one per element | ✅ Primary template source |
| `doc/images/sysml/clause-7/` | Per-row **kernel notation tables** with real examples | ✅ Ground truth |
| `doc/Intro to the SysML v2 Language-Graphical Notation.pdf` | Dedicated graphical-notation **training** doc | ✅ Best worked examples |

### 2.2 Node shapes

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

### 2.3 Connector ends and line styles (the conformance core)

End shapes sit at the **owner/target** end as indicated; lines are solid unless noted.

| Relationship | Line | End shape | At end | Status |
|---|---|---|---|---|
| Specialization / subclassification | solid | hollow **triangle** | supertype | ✅ Done |
| Redefinition (usage→usage) | solid | hollow triangle **+ ⊥ crossbar** near tip | redefined | ⬜ |
| Composite feature membership (owned/nested) | solid | **filled diamond** | owner | ⬜ (`EndMarkerStyle.FilledDiamond` defined, unused) |
| Reference feature membership (e.g. requirement `subject`) | solid | **hollow diamond** | owner | ⬜ (`EndMarkerStyle.HollowDiamond` defined, unused) |
| Connection / connector (interconnection) | solid | none (ends may show multiplicity) | — | ✅ Done (plain line) |
| Succession (action flow) | **dashed** | **open V** (stroke-only) | target | ⚠️ Solid + filled today |
| State transition | solid | **open V** (verify vs training PDF) | target | ⚠️ Filled today |
| Sequence message | solid | **open V** (stroke-only) | receiver | ⚠️ Filled today |
| Dependency | dashed | open V | target | ⬜ |

Subsetting / feature-typing in the templates are frequently shown **textually** in
compartments (`:>`, `:>>`) rather than as separate edges — match that convention.

### 2.4 Typography and color

- Body labels Arial/sans **12px**; state entry/do labels **11px**; sequence message labels
  **10px**. (Our themes use an embedded Noto Sans; sizes are theme-driven — verify ratios.)
- Spec diagrams are **black on white**, no fill color. Our themes add subtle fills by depth;
  a **Print** theme already approximates the spec's monochrome look.

### 2.5 Compartments

`«keyword» Name` in the name compartment (bold; keyword in guillemets), then stacked,
separator-lined compartments: e.g. state `entry/`, `do/`, `exit/`; requirement `doc`,
`attributes`, `constraints`, `subject`, `references`; part `attributes`, `ports`, `parts`.
We render attributes/ports/parts today; deeper compartments are a gap (see Phase 15).

---

## 3. Phase Gate (every phase must satisfy)

Each phase is delivered on its own feature branch and merged via PR only after **all** of the
following gates pass. A phase is not "done" until the feature **and** its supporting
documentation ship together in the same PR.

### 3.1 Automated quality gates (all must pass)

- `pwsh ./build.ps1` — solution builds and all unit tests pass on **net8.0, net9.0, net10.0**,
  zero errors, zero warnings (analyzers are warnings-as-errors).
- **Targeted unit tests** added for the phase's new behavior, each linked from a requirement.
- `pwsh ./lint.ps1` exits 0 — markdownlint-cli2, cspell (US English), yamllint, `dotnet format`,
  ReqStream `--lint`, VersionMark, ReviewMark.
- **ReqStream `--enforce`** against fresh test results — every new/changed requirement traces to
  a passing test at its own level.
- **ReviewMark `--lint`** — all review-sets resolve; every new source/doc file is assigned to a
  review-set.

### 3.2 Multimodal LLM visual inspection

Coordinate-arithmetic tests cannot see "the end marker is filled instead of open" or "the
connector grazes a box." Every rendering phase therefore includes a visual gate performed by the
implementing agent:

1. Publish the tool and render the affected **gallery models and targeted test models** to PNG
   (and, for SVG-specific behavior, convert the SVG → PNG so the vector output is inspected as
   rendered).
2. The agent reads each image back with the multimodal `view` tool and checks the phase's
   **specific visual criteria** (listed per phase) against the §2 notation reference and the OMG
   `clause-8.2.3` templates / training PDF.
3. Record pass/fail per criterion; fix and re-render until all pass.
4. Temporary `_check/` artifacts are deleted and never committed.

### 3.3 Supporting-documentation updates (in the same PR, as applicable)

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

### 3.4 Process gates (run for every phase — not just the last)

Before each phase's PR, in order:

1. **Validate** — automated gates (§3.1) and the multimodal visual gate (§3.2) all pass.
2. **change-review agent** — run the built-in change-review agent on the phase diff and address
   any egregious findings. Running it every phase keeps PR review comments small and catches
   issues while context is fresh.
3. **lint-fix agent** — run the built-in lint-fix agent so `lint.ps1` passes and CI does not fail
   on formatting/spelling on first run.
4. **Re-validate & open the PR** — branch + PR; **no direct commits to `main`**.

**Release notes are generated from commit messages via the build notes** — there is no
`CHANGELOG`/`CHANGES.md`; write clear, descriptive commit messages so the generated notes are
useful.

### 3.5 Execution & model strategy (sub-agent delegation)

Phases are run by an orchestrator that **delegates each task to a sub-agent launched with an
explicitly chosen model**. Default to the cheaper driver; escalate only where deeper reasoning
earns its cost. This is safe because the §3 gates are objective — they catch regressions, back-
driven requirements, and notation slips regardless of which model produced the work.

| Task | Sub-agent | Model |
|---|---|---|
| Feature implementation (shapes/edges/line-styles/strategies) | developer / general-purpose | **Driver** (e.g. Sonnet 4.6) |
| Doc authoring (requirements/design/verification, README, user guide) | developer / general-purpose | Driver |
| Self-validation tests + package-validation script | developer | Driver |
| Multimodal visual inspection (render → `view` → judge vs §2) | general-purpose (multimodal) | Driver; escalate if not converging |
| Layout/geometry debugging that does not converge | general-purpose | **Escalation** (e.g. Opus 4.8) |
| Per-phase change-review gate (§3.4) | code-review | **Strong reviewer** (e.g. Opus 4.8) |
| Lint cleanup (§3.4) | lint-fix | Driver |

Rules:

- The orchestrator **names the model explicitly** when launching each sub-agent (model override).
- Escalate the driver to the stronger model only after ~2 inspect-fix iterations fail to resolve
  a visual/geometry bug on the cheaper model.
- The **change-review gate always runs on the strong model** — it is the safety net for cheaper-
  driver output, and keeps PR review comments minimal.
- **Notation judgment calls where the OMG sources conflict** (e.g. open-V vs filled end markers)
  are surfaced to the maintainer for a decision — not resolved autonomously by any model.

---

## 4. Release Phases (0.1.0)

Each phase below lists its **scope** and its phase-specific **visual criteria**; all phases
additionally satisfy the §3 Phase Gate (automated + multimodal + docs + process).

### Phase 13 — Connector-end & line-style conformance

Bring routed connectors into line with §2.3 — the highest-value, broadest-impact change.

- Wire the already-defined end markers to relationships: **filled/hollow diamonds** for
  composite/reference membership; **redefinition** crossbar variant.
- Switch succession/transition/message end markers to the spec style (**open V**, stroke-only)
  and make **successions dashed** — pending confirmation against the training PDF; keep a theme
  switch if the training material differs from `clause-8.2.3`.
- Add `LineStyle.Dashed` usage where required; ensure end markers render correctly at clearance
  stubs.

**Scope:** `LayoutLine` end-marker/line-style assignments in the view strategies; renderer
marker defs (already present). No new engines.
**Visual gate:** state/action/sequence/general galleries match §2.3 end shapes; membership
diamonds appear where membership is shown.

### Phase 15 — Additional relationship edges (General View)

Render the relationships currently omitted from the General View, each routed via
`ChannelRouter` and carrying the correct §2.3 end shape:

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

### Phase 17 — View dynamics refinements

- **Sequence View:** populate `LayoutActivation` execution bars; combined-fragment boxes
  (alt/opt/loop); async/reply message styling.
- **Action Flow View:** **fork/join** thick bars, **decision/merge** diamonds, accept/send
  action shapes; optional **swim-lanes** via `LayoutBand`; item-flow edge annotations.

**Scope:** `SequenceViewLayoutStrategy`, `ActionFlowViewLayoutStrategy`, renderer shape
primitives (bar, diamond, pentagon, note). `LayoutActivation`/`LayoutBand` already defined.
**Visual gate:** sequence shows activation bars + a fragment; action flow shows a fork/join and
a decision/merge with correct shapes.

### Phase 18 — Release readiness

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
reference) — add the §2 notation-conventions table, an
invariants/gotchas section, and any remaining SVG illustrations; wire into CI
(`build.yaml`, `.fileassert.yaml`, `.reviewmark.yaml`).

**Gallery & packaging:** regenerate the gallery against the final notation and refresh
`docs/gallery/README.md`; set version metadata, package descriptions/icons/tags, and 0.1.0
release notes (generated from commit messages via the build notes — no `CHANGELOG`/`CHANGES.md`);
confirm `dotnet tool install` and library-package consumption paths.

**Gate:** the self-validation suite passes on all three OSes; the package-validation script passes
(tool installs and renders SVG + PNG; library consumer renders PNG); `--licenses` lists OFL text;
README/User Guide note Geometry as unsupported; gallery reflects Phase 13–17 notation.

### Phase 19 — 0.1.0 Release

Final full-suite validation; tag `v0.1.0`; publish the four NuGet packages; create the GitHub
release with notes and gallery highlights. **Publishing requires maintainer authorization
(credentials, irreversible) — prepared to the edge of publish, then handed off.**

---

## 5. 0.2.0 — AI-Assisted Model Analysis

SysML v2 is emerging as a substrate for AI-verified engineering: a machine-readable, semantically
resolved model of a system's structure, behavior, requirements, and traceability. An AI agent
changing or reviewing code could re-derive all of that by reading raw source — or it could ask
SysML2Tools targeted questions and get small, authoritative, token-cheap answers. The 0.2.0 theme
turns SysML2Tools from a *renderer* into a **model query and analysis engine** that an AI (or a
human) drives from the command line.

The semantic model already parsed today (packages, definitions, features with typing and
multiplicity, imports, views, viewpoints, connections with endpoints, transitions with guards,
all indexed by qualified name in the `SymbolTable`, with supertype resolution) is a strong
substrate for this. The main new machinery is **reverse/relationship indexes** (today the symbol
table only resolves name→node forward) and, for the requirements story, first-class
**satisfy/verify/allocate** edges and **doc**-body capture.

### Phase 20 — CLI architecture: per-verb command model

**Motivation.** Today the CLI has two simple commands (`lint`, `render`) served by a single
`Context`/`ArgumentParser` and one flat arguments record. The 0.2.0 work introduces a `query`
command with **many sub-verbs** (`uses`, `used-by`, `impact`, `describe`, `hierarchy`,
`requirements`, `interface`, `connections`, `states`, `list`, `find`), plus dynamic-view options
on `render`. A single flat record where every option is a nullable property on one shared object
does not scale: verbs have different required/optional arguments, different validation, and
different defaults, and a flat record cannot express "`--element` is required for `describe` but
meaningless for `list`."

**Scope.**

- Introduce a **command/verb abstraction**: each command (and each `query` verb) owns its own
  strongly-typed **options record** and its own parse+validate logic, rather than one monolithic
  `ArgumentParser` populating one flat `Context`.
- Keep a small shared **global options** record (`--silent`, `--log`, `--format`, `--help`,
  `--version`) that all commands inherit; verb-specific records compose it.
- A top-level dispatcher parses the global options and the command/verb token, then hands the
  remaining tokens to the selected verb's parser, which returns its own validated options record.
- Per-verb records make required-argument enforcement, defaults, and help text **local to the
  verb** — each verb can render its own focused `--help`.
- Preserve backward compatibility for `lint` and `render` invocations (same flags, same
  behavior); this phase is a **refactor + extension point**, not a behavior change for existing
  commands.

**Design notes.**

- Verb-as-positional matches the existing `lint`/`render` parsing; the dispatcher recognizes
  `query` and reads the next token as the verb.
- `--element`/`-e` (qualified name) is an **option**, not a positional, so `::`-containing
  qualified names never collide with the file-glob positional bucket.
- Every result element is emitted as its **stable qualified name**, so an AI can chain one query's
  output straight into the next `--element`.

**Gate:** existing `lint`/`render` behavior unchanged (regression tests); the new command model
carries at least one non-trivial verb end-to-end (Phase 21); per-verb `--help` renders.

### Phase 21 — `query` command: model analysis for AI and humans

Add a `query` command that answers structural, dependency, requirements, and behavioral questions
against a loaded workspace, in an LLM-friendly format. General form:

```text
sysml2tools query <verb> --element <qualified-name> [options] <files...>
```

**Verbs (initial set).**

| Verb | Answers | Backed by |
|---|---|---|
| `uses` | What does X depend on? (supertypes, typed features, imports, outbound connections) | forward edges |
| `used-by` | Who depends on X? (the "blast radius") | **reverse index** |
| `impact` | Transitive dependency/impact closure of X, bounded by `--depth` | edge walk |
| `describe` | Summary card: kind, supertypes, features, docs, requirements | node + edges |
| `hierarchy` | Specialization chain up/down (`--direction up\|down\|both`) | `SupertypeWalker` |
| `requirements` | Requirements satisfied/verified by X | satisfy/verify edges (see below) |
| `interface` | Public interface: ports and typed features (with multiplicity) | features |
| `connections` | Connections touching an element or port | connection endpoints |
| `states` | States and guarded transitions of X | transitions |
| `list` / `find` | Enumerate/search elements by `--kind` or `--name` | symbol table scan |

**Output formatting.** One internal query-result model, two renderers, selected by
`--format markdown\|json` (default **markdown**, mirroring the existing `AstSerializer`
JSON path). Markdown is the default because it is more token-efficient and LLMs reason better
over it; JSON is offered for agent harnesses that parse results programmatically. Consider
`jsonl` for large streaming results (e.g. a full usage index). Requirements:

- **Stable qualified names as IDs in every format** — identical across markdown/json so results
  are chainable.
- **Deterministic ordering** (sort by qualified name) regardless of format — reproducible,
  diff-friendly, cache-friendly.
- Markdown rendering prefers **compact tables/indented outlines** over prose to minimize tokens.
- `--include-stdlib` (default off, matching `StdlibFilter`) to exclude standard-library noise.

**Model enrichment (enables the highest-value queries).**

- **Reverse/relationship indexes** — a `usedBy` / `specializedBy` / `satisfiedBy` layer over the
  `SymbolTable`; this is the single biggest enabler and unblocks `used-by`/`impact`.
- **Satisfy / verify / allocate edges** — SysML v2's requirement-trace relationships are not yet
  first-class nodes; capturing them turns `requirements` into the "AI-verified coding" feature
  (what contract must this module honor; which requirements are unverified).
- **Doc / comment bodies** — surface element documentation in `describe` output so the AI sees
  human intent, not just structure.

**Scope:** `query` verb parsers (Phase 20 model); query engine over the semantic model; reverse
index; markdown + json result renderers; new requirements/design/verification + ReviewMark.
**Gate:** each verb has targeted tests over fixture models; markdown and json outputs carry
identical IDs; deterministic ordering verified; `--help` per verb.

### Phase 22 — Dynamic (ad-hoc) views

**Motivation.** Today rendering requires the SysML source to declare a `view`. That means a
consumer cannot get a diagram of an element the model author did not pre-declare a view for — an
AI (or a reviewer) must edit the SysML files first. Dynamic views let a caller request **any view
type of any element** entirely from the command line, without modifying the model.

**Scope.**

```text
sysml2tools render --view-type interconnection --view-target SystemsModel::Engine --depth 2 <files...>
```

- `--view-type <kind>` — select a viewpoint/layout strategy (general, interconnection, state,
  action, sequence, grid, browser) explicitly, bypassing model-declared views.
- `--view-target <qualified-name>` — the element to render the view of.
- Reuse the existing `--depth`, `--format`, `--output`, and theme options.
- Internally, synthesize an in-memory view node (the same mechanism `--auto` already uses to
  inject a synthetic `GeneralView`) targeting the requested element, then route it through the
  existing `DiagramTypeRouter` → `ILayoutStrategy` → `IRenderer` pipeline. No new rendering
  engines — this is an **input path**, not a new renderer.
- Validate that the requested view type is compatible with the target (e.g. a state view requires
  states); emit a clear diagnostic when it is not, rather than an empty diagram.

**Why this is appropriate.** It generalizes the already-proven `--auto` synthesis path, requires
no model edits (critical for read-only AI review workflows), and composes naturally with the
`query` command: an AI can `query describe` an element, then `render --view-type` the most
relevant view of it — all without touching source files.

**Scope:** `render` option parsing (Phase 20 model); synthetic view construction for each view
type; target/type compatibility validation; requirements/design/verification + ReviewMark.
**Gate:** each view type renders for a suitable ad-hoc target with no model-declared view;
incompatible type/target combinations produce a clear diagnostic, not a broken diagram.

### Phase 23 — Additional AI-analysis options (candidates)

Lower-priority options that further support AI analysis of a model; each is independently
scoped and gated, and any may be pulled forward or deferred:

- **`--format sarif` for `lint`** — `SysmlDiagnostic` is already structurally SARIF-compatible;
  emitting SARIF lets AI/CI toolchains consume lint findings through standard tooling.
- **`export` verb** — dump the resolved semantic model (symbol table + edges + diagnostics) as
  JSON/JSONL for an agent harness to index locally and run its own queries offline.
- **Metrics / summary query** — workspace-level counts and hotspots (most-depended-on elements,
  unverified requirements, orphan elements, cyclic specialization) to orient an AI before it
  starts, and to flag model-health issues in review.
- **`diff` of two workspaces** — structural/semantic delta between two model revisions (added /
  removed / changed elements, edges, and requirement traces) so an AI reviewing a change sees the
  *model-level* impact of a PR, not just the text diff.
- **Machine-readable `--format json` everywhere** — extend the shared formatter so every command
  (not just `query`) can emit JSON, keeping the CLI uniformly scriptable.

**Gate (per option):** the §3 Phase Gate applies; each option ships with tests, docs, and stable,
deterministic output.

---

## 6. Deferred Beyond 0.1.0

These remain explicitly out of the 0.1.0 scope unless pulled forward:

- **Geometry View (0.2.0)** — the 8th view: 2D spatial placement (3D projected to 2D) of items
  whose spatial coordinates are specified in the model via the SysML geometry/spatial library.
  Deferred because it requires new semantic capability (extracting numeric attribute *values*,
  not just structure) and a coordinate convention plus test models that use it. **0.1.0 must
  document the Geometry View as not yet supported in the README and User Guide** (see Phase 17).
- **SARIF** diagnostic output (`SysmlDiagnostic` is already structurally compatible) — see
  Phase 23.
- **Loadable theme files** (YAML/JSON) — the `Theme` record is forward-compatible.
- **`export` verb** / additional output formats — see Phase 23.
- **3D Geometry** rendering (2D projection only, even once Geometry ships in 0.2.0).
- **Nested state regions** and other advanced behavioral notation.

---

## 7. Layout Engine Architecture (reference)

Reusable, stateless engines in `Layout/Engine/` accept plain geometric input (no SysML
model references) and return computed geometry; each is independently unit-tested. Layered
views (General, State Transition, Action Flow) drive their geometry through the reusable
`LayeredLayoutPipeline` and its single-responsibility stages; `InterconnectionLayoutEngine`
is a thin façade over that same pipeline.

| Engine | Capability | Used by |
|---|---|---|
| `ContainmentPacker` | Bottom-up sizing + bin-packing of children in a container | General, Interconnection |
| `ChannelRouter` | Orthogonal edge routing around obstacles, clearance-retry, perpendicular stubs | General, Interconnection, State |
| `InterconnectionLayoutEngine` | Façade assembling the layered pipeline (`RIGHT`, recursive nesting) for the interconnection view | Interconnection |
| `LayeredLayoutPipeline` | Reusable ELK-style Sugiyama pipeline of single-responsibility stages | General, State, Action Flow, Interconnection |

Helper units: `ConnectorLabelPlacer` (collision-aware label placement), `LayoutWarnings`
(layout diagnostics), `BoxMetrics` (sizing). Sequence/Grid/Browser/Geometry layouts are pure
arithmetic in their strategies (no engine).

---

## 8. LayoutTree Vocabulary Coverage (reference)

| Primitive | Status |
|---|---|
| `LayoutBox`, `LayoutLabel`, `LayoutLine`, `LayoutCompartment`, `LayoutPort`, `LayoutLifeline`, `LayoutBadge`, `LayoutGrid` | ✅ Rendered |
| `LayoutActivation` (sequence bars) | ✅ Defined — populated in Phase 17 |
| `LayoutBand` (swim-lanes) | ✅ Defined — populated in Phase 17 |

End-marker vocabulary (`EndMarkerStyle`): `None`, `OpenChevron`, `HollowTriangle`,
`HollowTriangleCrossbar`, `FilledArrow`, `HollowDiamond`, `FilledDiamond`, `Circle`, `Bar` — all
marker defs exist in both renderers; `HollowDiamond`/`FilledDiamond` and the open-chevron style are
wired to relationships in Phase 13.
