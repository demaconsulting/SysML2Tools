# SysML2Tools Roadmap

This document lists planned work — what we intend to change and notes on how to change it. It is
deliberately limited to work to be done.

The work falls into three themes:

- **Notation & view conformance** — bring rendered output in line with SysML v2 graphical notation
  and finish the remaining view dynamics.
- **Release & packaging** — self-validation coverage, package validation, and licensing/attribution.
- **Model query & analysis** — dynamic (ad-hoc) views and further AI-analysis options.

---

## Notation & view conformance

### Connector-end & line-style conformance

Bring routed connectors into line with the SysML v2 notation — the highest-value, broadest-impact
change.

- Wire the already-defined end markers to relationships: **filled/hollow diamonds** for
  composite/reference membership; **redefinition** crossbar variant.
- Switch succession/transition/message end markers to the spec style (**open V**, stroke-only)
  and make **successions dashed** — pending confirmation against the training PDF; keep a theme
  switch if the training material differs from `clause-8.2.3`.
- Add `LineStyle.Dashed` usage where required; ensure end markers render correctly at clearance
  stubs.

**Scope:** `LayoutLine` end-marker/line-style assignments in the view strategies; renderer
marker defs (already present). No new engines.
**Visual gate:** state/action/sequence/general galleries match the spec end shapes; membership
diamonds appear where membership is shown.

### Additional relationship edges (General View)

Render the relationships currently omitted from the General View, each routed via
`ChannelRouter` and carrying the correct spec end shape:

- Redefinition, subsetting (where shown as edges), feature typing, dependency, containment,
  connection/binding, allocation.
- Shared-bus generalization (multiple subtypes merging into one line to a supertype) as an
  optional readability refinement.

**Scope:** `AstBuilder`/semantic exposure of the relationship kinds as needed;
`GeneralViewLayoutStrategy` edge emission; resolver coverage.
**Visual gate:** a model exercising each relationship renders distinct, correctly-headed edges.

### Annotating elements & compartment depth

- Render **Documentation/Comment** notes as `BoxShape.Note` (folded-corner) nodes attached to
  their annotated element.
- Extend compartments to spec depth: enumeration values, constraint bodies, requirement
  `subject`/`constraints`/`doc`, and a documentation compartment on definitions/usages.

**Scope:** semantic exposure of doc/comment + compartment content; `GeneralViewLayoutStrategy`
and renderers; possibly `LayoutLabel`/compartment tweaks.
**Visual gate:** a documented requirement/part renders its note and full compartments.

### View dynamics refinements

- **Sequence View:** populate `LayoutActivation` execution bars; combined-fragment boxes
  (alt/opt/loop); async/reply message styling.
- **Action Flow View:** **fork/join** thick bars, **decision/merge** diamonds, accept/send
  action shapes; optional **swim-lanes** via `LayoutBand`; item-flow edge annotations.

**Scope:** `SequenceViewLayoutStrategy`, `ActionFlowViewLayoutStrategy`, renderer shape
primitives (bar, diamond, pentagon, note). `LayoutActivation`/`LayoutBand` already defined.
**Visual gate:** sequence shows activation bars + a fragment; action flow shows a fork/join and
a decision/merge with correct shapes.

### View `filter [<expr>];` expression evaluation

`GeneralViewLayoutStrategy` now scopes a rendered diagram to a view's `expose <...>;` subject
subtree, but a view's `filter [<expr>];` body statement is only parsed into
`SysmlViewNode.FilterExpressionText` (raw source text) — it is never evaluated, and a
layout warning ("parsed but not yet evaluated") is emitted in its place. SysML v2 view filtering
is part of the standard's view/viewpoint mechanism for selectively including/excluding elements
from a rendered diagram by predicate, beyond simple subject-subtree containment scoping (for
example, "only elements satisfying a given requirement" or "only elements with a given
stereotype"); without evaluation, a modeler's `filter` statement is silently ineffective beyond
the warning.

- Design and implement an expression evaluator for the bracketed filter expression grammar
  (boolean/membership predicates over the resolved scope's elements), reusing/aligning with
  existing expression-parsing infrastructure where practical.
- Apply the evaluated predicate as an additional filter over the resolved (expose) scope in
  `GeneralViewLayoutStrategy`, removing the "not yet evaluated" warning once a filter
  expression is present and successfully evaluated.
- Surface a diagnostic for filter expressions that fail to parse or evaluate, mirroring the
  unresolved-reference diagnostic pattern already used for `expose`.

**Scope:** `AstBuilder`/`SysmlViewNode` (expression AST capture, if warranted, beyond raw text);
new expression-evaluation component; `GeneralViewLayoutStrategy` filter application.
**Visual gate:** a view with a `filter [<predicate>];` statement renders only the elements
satisfying the predicate, with no "not yet evaluated" warning.

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

### Dynamic (ad-hoc) views

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

**Scope:** `render` option parsing (per-verb command model); synthetic view construction for each
view type; target/type compatibility validation; requirements/design/verification + ReviewMark.
**Gate:** each view type renders for a suitable ad-hoc target with no model-declared view;
incompatible type/target combinations produce a clear diagnostic, not a broken diagram.

### Additional AI-analysis options (candidates)

Lower-priority options that further support AI analysis of a model; each is independently
scoped and gated, and any may be pulled forward or dropped:

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
