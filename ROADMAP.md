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
- A variant/specialization view showing `abstract` definitions and variants alongside the
  subsetting and redefinition that gallery model 14 already shows on a drive unit.

This also exercises `--filter`'s metadata classification-test path (e.g. tagging parts with
`@Critical`/`@Requirement` metadata) rather than only the bracket/expose-subtree path the
gallery currently shows.

**Scope:** `docs/gallery/models/01-drone-general.sysml` (or a new model file), regenerated SVGs,
`docs/gallery/README.md`.

### General View notation: follow-ups

The General View now draws usage nodes and the specification's relationship glyphs top-to-bottom.
Remaining work, each independently scoped:

- **Public node-policy option.** Which usages are nodes (`Pilot` default, every named usage, or rows
  only) is an internal setting of `GeneralViewLayoutStrategy`. Expose it through `RenderOptions` and the
  `render` command, with help text, requirements, and user-guide text.
- **Rendering engine (`DemaConsulting.Rendering`) items.** Rich-text rows with bold keywords (issue
  #99); a source-end
  marker on graph edges (so the strategy no longer reverses connectors after layout, and the strategy's
  `ClearEndDecorations` pass, which keeps a clean straight leg at an owner-end glyph, can be retired); a
  tight-tree layering that keeps a wide drawing compact; control of the shape of the
  unconnected-node bucket (the strategy's `TryTuckUnconnected` pass currently places those boxes in the
  free space of the drawing instead); and alignment of the two ports of a connector that ends on a shared
  box face (the engine spreads the ports evenly, which leaves 2.5 to 3.4 px steps in some typing and
  specialization connectors of `DriveUnitGeneralView`).
- **Gallery model for requirements and imports.** No gallery model shows `satisfy`, `verify`, `import`,
  or `::>`; they are covered by unit tests only. A model with requirement usages renders crossing,
  overlapping `satisfy` and `verify` connectors and tangled labels; a model with requirement
  definitions as `satisfy` and
  `verify` targets renders cleanly but is not valid SysML (the specification defines these relationships
  for requirement usages), and import labels still clip. Add the model once the connector routing is
  fixed.
- **Package-heavy drawings.** The vehicle packages drawing (`VehiclePackagesGeneralView`) is tall
  because the engine places a folder's externally connected single box after the connected group, and
  one connector crosses a specialization line; the leaf-block share of the canvas of the package views is
  below the checklist threshold. Both need engine control of the interior order and spacing of
  folders.
- **Owned and unowned membership glyphs** (`⊕` and `○`) when a member is drawn outside its owner.
- **Comment details.** The comment keyword, name, locale, and `about` target need these fields on
  `SysmlAnnotation` first.
- **Annotations on compartment rows.** Under the default node policy, an attribute, plain reference,
  or port without nested features stays a compartment row, and its `doc` and `comment` are not drawn
  (as in the OMG Pilot implementation). Draw them as a note joined to the row by a dashed link, which
  the specification permits.
- **Import label clipping.** The `«import»*` label is cut off when an import connector routes around the
  outside of a folder, because the canvas fit shifts waypoints but not label extents.
- **More relationships.** `perform`, `require`, `frame`, `exhibit`, and `include` need their data in the
  syntax tree or semantic edges before they can be drawn.

### Action Flow View: remaining control-node/shape gaps

Fork/join/decision/merge/terminate control nodes, accept/send actions with their corner icons and
payload text, guarded and `else` successions, explicit `first start`/`then done` markers, typed
action labels, and the compact `action a1; then a2;` succession idiom already render correctly.
Remaining gaps, each independently scoped:

- Swim-lanes via `LayoutBand`.
- Item-flow edge annotations (no item-flow/payload capture exists in the AST yet).
- `assignmentNode`/`ifNode`/`whileLoopNode`/`forLoopNode` AST support (still
  entirely unmodeled).
- An `actionUsage`-level (rather than `action def`-level) nested `actionBody` —
  `VisitActionUsage` still doesn't collect nested action-body children, so control nodes/
  successions nested inside an `action x : T { ... }` *usage* (as opposed to an
  `action def X { ... }` body) remain invisible.

### Sequence View dynamics

- **Sequence dynamic-view compatibility check (known limitation of `DynamicViewSynthesizer`,
  the Core Rendering Internal subsystem behind `render --view-type <kind> --view-target
  <qualified-name>`):** its `--view-type sequence` pre-check accepts
  any target with at least one nested `message` usage (the cheap, necessary-but-not-sufficient
  approximation of "at least one lifeline and message" — a full message-edge-walk validation was
  deliberately not implemented). A target with lifelines but
  zero resolvable messages passes this pre-check yet still renders the near-blank canonical
  `LayoutTree` sentinel. Closing this gap requires either a full message-edge-walk validation in
  `DynamicViewSynthesizer` or surfacing `SequenceViewLayoutStrategy`'s own lifeline-resolution
  result back to the synthesizer.
- **Proxies, found and lost messages, and successions (issue #106).** The SysML v2 sequence notation
  also has proxies (a dot on a lifeline for an event), dashed succession arrows, and messages from or to
  nowhere; the Sequence View draws lifelines and messages only. Sequence ports on a lifeline are not drawn
  either, and the engine needs a proxy marker for the dot.
- **Dropped: combined fragments, activation bars, and async/reply message styling.** These are UML sequence-diagram
  constructs, not part of the SysML v2 sequence notation, so the Sequence View does not draw them and they are
  no longer on this roadmap (earlier roadmap text listed fragment boxes and async/reply styling).

**Scope:** `SequenceViewLayoutStrategy`; the event-occurrence data is already in the syntax tree.
**Visual gate:** a lifeline shows a proxy dot for each event, and a found message starts at a dot.

### Interconnection View: remaining notation

Ports, connector end labels, and the connector kinds are drawn in the SysML v2 notation. Remaining work,
each independently scoped (issue #100 stays open for the port items; the end labels of issue #101 are
delivered for binary connections):

- **Proxy ports and nested ports.** A proxy port (a port that stands for a part inside the box) and a
  port with nested ports are drawn as plain ports with no proxy marker and no nested glyphs; the engine
  needs the proxy glyph and nested-port placement (upstream issue to be filed; repro prepared).
- **N-ary connection dot.** A connection with more than two ends is not drawn with the junction dot of the
  notation; the engine needs a connector junction.
- **Root-frame delegation connectors.** A connector to a port of the frame of the root is not routed; the
  ports of the frame are drawn but no connector reaches them.
- **Shared-port fan-out (engine).** A port that several connectors name is drawn as one glyph per
  connector, because the engine splits a shared port per edge; the strategy labels it once (upstream issue to
  be filed; repro prepared).
- **Unconnected ports (engine).** The engine does not emit a port that no edge names, so the strategy
  places those itself after the layout (upstream issue to be filed; repro prepared).
- **Connector label corridor (engine).** The layer corridor reserves room for end labels and outward port
  labels but not for the midpoint label of a connector, so a connector label can land on a port label and
  the strategy widens the corridor by hand; the reserved room also leaves the diagrams below the
  checklist's share of the canvas for blocks (B5). Upstream issue to be filed for the label corridor (repro
  prepared); the fixed layer spacing is tracked as
  [demaconsulting/Rendering#60](https://github.com/demaconsulting/Rendering/issues/60).

**Scope:** `InterconnectionViewLayoutStrategy`, `LayeredPlacement`; most items need `DemaConsulting.Rendering`
support first.
**Visual gate:** a port shared by two connectors is one glyph, and a connector reaches a frame port.

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

**Engine support:** `DemaConsulting.Rendering` 0.3.0-beta.2 now routes an edge declared on a graph
scope between nodes that sit at any depth beneath it (a cross-hierarchy edge, anchored to the actual
nested nodes rather than their containers), which removes the engine-side blocker for this item.
`GeneralViewLayoutStrategy` already uses it for edges across packages. The Interconnection
strategy's restructuring described above is still required, so this item remains open.
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
the rendering pipeline and each view's layout strategy are exercised on every OS:

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
against the packed Tool `.nupkg` right after `dotnet pack`, guarding against the historical 435 MB
size bloat and verifying the main assembly/README are present with no stray native symbols. Still
open:

1. **Tool smoke test:** install the packed tool from a local feed into a clean directory → run
   `--version`, render a sample to **both SVG and PNG** (PNG exercises the managed CanvasNet
   renderer), and `--licenses`.
2. **Library-consumer smoke test:** a throwaway project referencing each library package from the
   local feed → restore → exercise parse→layout→render-to-SVG-in-memory and render-to-PNG.

### Licensing, docs, gallery, publish

- **Licensing/attribution:** `--licenses` output covering Noto Sans (SIL OFL 1.1) and other OTS;
  per-package README notes.
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
- **Loadable theme files** (YAML/JSON) and a `render --theme` option — the `Theme` record is
  forward-compatible. The `render` command now always uses the built-in monochrome
  `SysmlThemes.Plain`; library callers can already pass any `Theme`.
- **Nested state regions** (composite and parallel states drawn with their nested states; a
  composite state is drawn as an ordinary box today and its nested states are ignored) and other
  advanced behavioral notation.
