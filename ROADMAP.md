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

### Gallery: expand drone model with expose/filter-narrowed multi-view showcase (follow-up)

The drone gallery model already has one `expose`-narrowed view (`BatterySubsystemView { expose
Battery; }`, documented in `docs/gallery/README.md` section 1b). This follow-up adds several
more `expose`/`filter`-narrowed views spotlighting one relationship kind each, instead of one
flat whole-workspace General View plus a single subsystem view:

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

### Action Flow View: remaining control-node/shape gaps

Fork/join/decision/merge control nodes and the compact `action a1; then a2;` succession idiom
already render correctly. Remaining gaps, each independently scoped:

- A true pentagon accept/send shape (needs a `DemaConsulting.Rendering` package change; boxes
  currently use the ordinary rounded-rectangle shape with their own keyword label instead).
- Swim-lanes via `LayoutBand`.
- Item-flow edge annotations (no item-flow/payload capture exists in the AST yet).
- `assignmentNode`/`terminateNode`/`ifNode`/`whileLoopNode`/`forLoopNode` AST support (still
  entirely unmodeled).
- An `actionUsage`-level (rather than `action def`-level) nested `actionBody` —
  `VisitActionUsage` still doesn't collect nested action-body children, so control nodes/
  successions nested inside an `action x : T { ... }` *usage* (as opposed to an
  `action def X { ... }` body) remain invisible.

### Sequence View dynamics

- Populate `LayoutActivation` execution bars; combined-fragment boxes (alt/opt/loop); async/reply
  message styling.
- **Sequence dynamic-view compatibility check (known limitation of `DynamicViewSynthesizer`,
  the Core Rendering Internal subsystem behind `render --view-type <kind> --view-target
  <qualified-name>`):** its `--view-type sequence` pre-check accepts
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

---

## Release & packaging

### Self-validation suite (expand render coverage beyond General View)

Downstream projects run `sysml2tools --validate` in their own environment as tool-qualification
evidence, and the win/mac/linux integration-test matrix runs it per-OS. `Validation.cs` already
runs 9 tests following the `SysML2Tools_{Capability}SelfTest` naming convention: version, help,
lint, SVG render, PNG render, dynamic-view SVG/PNG render, dynamic-view filtered render, and
export — but every render test (SVG, PNG, and dynamic-view) exercises only the **General View**
(`SelfTestModel`'s `GeneralView`). Add one SVG+PNG-validating test per remaining view kind so
SkiaSharp native assets and each view's layout strategy are exercised on every OS:

| Test | Proves |
|---|---|
| `SysML2Tools_RenderInterconnectionViewSelfTest` | ports/connectors → SVG + PNG |
| `SysML2Tools_RenderStateTransitionViewSelfTest` | states → SVG + PNG |
| `SysML2Tools_RenderActionFlowViewSelfTest` | layered actions → SVG + PNG |
| `SysML2Tools_RenderSequenceViewSelfTest` | lifelines → SVG + PNG |
| `SysML2Tools_RenderGridViewSelfTest` | matrix → SVG + PNG |
| `SysML2Tools_RenderBrowserViewSelfTest` | tree → SVG + PNG |

Validity is asserted (well-formed SVG root; PNG signature + non-zero dimensions), not exact
bytes, so the evidence is robust across environments. `SelfTestModel` will likely need extending
with the minimal constructs each view kind requires (ports/connectors, states, actions,
messages, etc.) alongside its existing `part def`s.

### Package validation gate (automated, before publish)

`build.yaml` already runs `dotnet pack` for all packages and installs the packed tool as part of
CI. It also runs FileAssert `[package]`-tagged size and zip-content assertions (`.fileassert.yaml`)
against every packed `.nupkg` right after `dotnet pack`, guarding against regressions such as the
historical 435 MB Tool package bloat and verifying expected DLLs/README are present. Still open:

1. **Tool smoke test:** install the packed tool from a local feed into a clean directory → run
   `--version`, render a sample to **both SVG and PNG** (PNG proves SkiaSharp natives resolve),
   and `--licenses`.
2. **Library-consumer smoke test:** a throwaway project referencing each library package from the
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
extends SysML2Tools's model query and analysis capabilities beyond the existing `query` and
`export` commands, and the `DemaConsulting.SysML2Tools.Core` public Query library API.

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
