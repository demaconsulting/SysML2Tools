# Introduction

<!-- cspell:ignore parenthesization istype hastype -->

SysML2Tools is a free, open-source .NET CLI tool and library that parses SysML v2 textual
model files and renders them as professional nested block diagrams. It is designed for .NET
teams in regulated industries who author SysML v2 models as part of a Model-Based Systems
Engineering (MBSE) practice and need to generate diagram images programmatically — without
a paid GUI tool or a non-.NET runtime dependency.

A secondary audience is AI agents iterating on SysML v2 models: the `lint` command provides
structured diagnostic output (file, line, column, severity) that enables a model-fix loop
without requiring a rendered diagram, and the `query` command lets an AI agent answer
architecture and traceability questions (dependencies, requirement trace links, structure,
behavior) directly from the semantic model instead of reading raw `.sysml` files.

## Purpose

This guide covers the installation, configuration, and use of SysML2Tools. It describes
the `lint`, `render`, `query`, and `help` commands, the global CLI flags, view selection
behavior, output formats, and depth limiting.

## Scope

This user guide covers:

- Installation via `dotnet tool install`
- Linting SysML v2 workspaces with the `lint` command
- Rendering diagrams with the `render` command
- Querying the semantic model with the `query` command
- Getting command and verb-specific help with the `help` command
- Global CLI options
- View selection and depth limiting
- Self-validation for tool qualification evidence

# Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.SysML2Tools.Tool
```

# Workspaces

SysML2Tools operates on a **workspace** — a set of `.sysml` files loaded together. The OMG
standard library (`stdlib`) is always implicitly included. You specify workspace files as
glob patterns on the command line:

```bash
# Single file
sysml2tools lint model.sysml

# All .sysml files under a directory
sysml2tools lint "src/**/*.sysml"

# Multiple patterns
sysml2tools render "common/**/*.sysml" "system/**/*.sysml" --output out

# Recursive match with an exclusion (files under src/generated are skipped)
sysml2tools lint "src/**/*.sysml" "!src/generated/**"
```

# Linting

The `lint` command loads a workspace, resolves the semantic model, and reports all
diagnostics. It exits with a non-zero code if any errors are present.

```bash
sysml2tools lint "src/**/*.sysml"
```

Diagnostic output includes file path, line, column, severity, and message:

```text
model.sysml:12:5: error: unresolved reference 'VehicleSystem'
model.sysml:34:1: warning: view 'Overview' references unsupported viewpoint kind
```

This structured output is suitable for:

- CI/CD pipelines that fail the build on model errors
- AI-assisted model authoring loops that parse diagnostics and propose fixes

# Rendering

The `render` command loads a workspace, resolves a view, and renders it to SVG or PNG.
`--output` names an output *directory* (default: current directory); `--format` selects
`svg` (default) or `png`.

```bash
# Render to SVG
sysml2tools render model.sysml --output out --format svg

# Render to PNG
sysml2tools render model.sysml --output out --format png

# Render a named view from a multi-view workspace
sysml2tools render "src/**/*.sysml" --view SystemContext --output out --format svg

# Auto-render the top-level part def when no view is defined
sysml2tools render model.sysml --auto --output out --format svg
```

## View Selection

| Condition | Behavior |
| --- | --- |
| Exactly one view in workspace | Render it |
| Zero views, `--auto` specified | Auto-render BDD of top-level `part def` silently |
| Zero views, no `--auto` | Informational message; no output files written |
| Multiple views, none specified | Render every declared view (one output file per view) |
| Multiple views, `--view <name>` | Render only the named view |
| `--view <name>` names a view that does not exist | Error: lists available view names, exits non-zero |

## Dynamic (Ad-Hoc) Views

Rendering normally requires the SysML source to declare a `view`. `--view-type`/`--view-target`
(with an optional `--filter`) instead render **any resolvable element** on demand, without
requiring any model changes:

```bash
# Render an interconnection-style view of a part def, with no view def in the model
sysml2tools render model.sysml --view-type interconnection --view-target Pkg::Engine --output out

# Render a general view narrowed to elements carrying a @Safety metadata annotation
sysml2tools render model.sysml --view-type general --view-target Pkg::Vehicle --filter @Safety --output out
```

- `--view-type <kind>` — one of `general`, `interconnection`, `state`, `action`, `sequence`,
  `grid`, `browser`. Selects the same layout strategy `DiagramTypeRouter` would select for a
  declared view's `render asGeneralDiagram;`/`asInterconnectionDiagram;`/etc. member — see
  "View Body Statements" below.
- `--view-target <qualified-name>` — the element to render. Must resolve in the workspace, must
  not be a standard-library element, and must not be a `view`/`viewpoint`/`import`/`metadata`/
  `transition`/`connection` node (these kinds cannot serve as a dynamic view's rendered content).
- `--view-type` and `--view-target` must be supplied together; `--filter` is valid only alongside
  both of them; none of the three may be combined with `--view` or `--auto`. Violating any of
  these rules reports a specific error and a non-zero exit code, rather than silently picking one
  option over another.
- Each `--view-type` kind runs a cheap, **necessary-but-not-sufficient** structural compatibility
  pre-check against the target before rendering, so an obviously incompatible target reports a
  clear diagnostic instead of an empty or broken diagram:

  | `--view-type` | Compatibility pre-check |
  | --- | --- |
  | `general`, `grid`, `browser` | None — any resolvable, non-stdlib definition or usage is accepted |
  | `interconnection` | Target must be a `part def` with at least one nested `part` feature |
  | `state` | Target must have at least one nested state transition or `state` feature |
  | `action` | Target must have at least one succession or nested `action` feature |
  | `sequence` | Target must have at least one nested `message` usage |

  > **Known limitation — sequence view.** The AST has no dedicated "lifeline" node;
  > `SequenceViewLayoutStrategy` derives lifelines purely from each `message` usage's endpoint
  > references. The `sequence` pre-check therefore approximates "at least one lifeline" as "at
  > least one nested `message` usage" — necessary (zero messages guarantees zero lifelines) but
  > **not sufficient**: a target whose message endpoints fail to resolve to any lifeline still
  > passes this pre-check yet still renders the near-blank canonical `LayoutTree` sentinel. A
  > full message-edge-walk validation was deliberately not implemented for this check (see
  > `ROADMAP.md`'s "View dynamics refinements" item); this is a documented gap, not a silent
  > omission.

## View Body Statements

A `view def`/`view` declaration's body may contain `render <target>;` and `filter [<expr>];`
statements; a named `view` usage's body may additionally contain `expose <name>;` statements
(per the SysML v2 grammar, `expose` is only valid inside a `view` usage's body, not a `view def`
definition's body). For the General View strategy (the diagram produced when no more specialized
view kind applies), `expose` now scopes the rendered diagram instead of always rendering the
entire workspace:

- `expose <name>;` (valid only inside a named `view` usage's body, not a `view def`
  definition's body) — per the SysML v2 grammar, `expose` has four distinct forms with
  independent scoping behavior, driven by whether the grammar alternative is
  MembershipExpose or NamespaceExpose and whether a trailing `::**` requests recursion:
  - `expose X;` (bare MembershipExpose) — scopes to **`X` itself only**, not its containment
    subtree. If `X` resolves to a usage (e.g. `part myVehicle : Vehicle;`) rather than a
    definition, its resolved type (`Vehicle`) is also included, itself only (not the type's
    subtree either).
  - `expose X::**;` (recursive MembershipExpose) — scopes to `X` **and its entire containment
    subtree**: `X` plus every declaration whose qualified name is `X` or is contained within it.
  - `expose X::*;` (bare NamespaceExpose) — scopes to only `X`'s **direct (one-level) children**,
    not `X` itself and not deeper descendants.
  - `expose X::*::**;` (recursive NamespaceExpose) — like `expose X::*;`, still **excludes `X`
    itself** (a NamespaceExpose only ever exposes `X`'s Memberships — its members — never `X` as
    a member of itself), but additionally includes descendants beyond direct children, at any
    depth — unlike `NamespaceDirectChildren`, which stops at one level.

  If `X` does not resolve to any declaration in the workspace (for example, a typo), the tool
  falls back to rendering the full workspace for that view — but now also reports a diagnostic
  identifying the unresolved name, so the mistake is visible instead of silently rendering
  everything with no signal. The bracket-filter form `expose <path>::**[<expr>];` is now
  (Phase 2a) **evaluated** using the same supported subset described below for standalone
  `filter <expr>;`: when the bracketed expression parses and evaluates successfully, that entry
  narrows to only the matched descendant definitions within `<path>`'s own containment
  subtree, instead of the whole subtree — each `expose` entry in a view is evaluated
  independently, so one bracket-filtered entry's narrowing never affects any other `expose`
  entry in the same view. A bracket expression that fails to parse or falls outside the
  supported subset degrades gracefully to whole-subtree inclusion for that entry (regardless of
  whether the entry's own form was otherwise non-recursive), with a diagnostic identifying the
  failed expression and reason.
- `render <target>;` — per the SysML v2 grammar, this names a rendering style/format (e.g.
  `asTreeDiagram`, `asElementTable`). `render asTreeDiagram;`, `render
  asInterconnectionDiagram;`, `render asGeneralDiagram;`, `render asStateTransitionDiagram;`,
  `render asActionFlowDiagram;`, `render asSequenceDiagram;`, and `render asGridDiagram;` now
  each select their corresponding layout strategy, taking precedence over the name/supertype
  heuristic `DiagramTypeRouter` otherwise applies (the same tokens the `--view-type` dynamic-view
  flag maps to — see "Dynamic (Ad-Hoc) Views" above). Every other rendering-style name
  (`asElementTable`, `asTextualNotation`, or an unrecognized name) — and a view declaring no
  `render` member at all — has **no effect** on which strategy renders the view; see
  `ROADMAP.md` for further rendering-style selectors that may be added in future.
- `filter <expr>;` — a standalone view-body filter statement is now **evaluated** for a
  supported subset of SysML v2 filter-expression syntax (Phase 1): metadata classification
  tests (`@Type`, `@Pkg::Type`), boolean connectives (`and`, `or`, `not`, `xor`, `&`, `|`),
  parenthesization, and `(as Type).attribute` reads (bare, or compared with `==`/`!=` against a
  scalar literal). When the expression parses and evaluates successfully, the rendered scope is
  narrowed to the definitions the predicate matches. Any construct outside this subset
  (`istype`/`hastype`/`all`, arithmetic, conditional expressions, general feature-chain
  navigation, etc.) — or any syntax error — produces an explicit "unsupported filter construct"
  (or syntax-error) diagnostic and falls back to rendering the resolved (`expose`) scope
  unfiltered, exactly as before. The bracketed `expose <path>::**[<expr>]` filter form (Phase 2a)
  is evaluated using this identical supported subset, per `expose` entry — see above. Full
  evaluation of the remaining Phase 1-excluded constructs (`istype`/`hastype`/`all`, arithmetic,
  conditional expressions, general feature-chain navigation) is planned future work — see
  `ROADMAP.md`.
- A view with **no** `expose` statement (including the `--auto`-synthesized view) renders the
  full workspace, exactly as before this scoping behavior was introduced.

Every view kind honors `expose` scoping: General, Grid, and Browser Views apply the resolved
scope directly as a filter over their full applicable content. Interconnection, State
Transition, Action Flow, and Sequence Views each render exactly one selected root's contents, so
they instead use the resolved scope in two steps: first, restricting which root the view's own
heuristic selects to one relevant to the scope (the current heuristic root itself, an inner
element of it, or a definition that contains it) — an `expose` statement naming an unrelated
definition yields no root and an empty diagram; second, narrowing that selected root's own
children (parts, states, actions, or lifelines) to those within the resolved scope. A view with
no `expose` statement (including the `--auto`-synthesized view) renders unchanged, exactly as
before this scoping behavior was introduced, for every view kind.

For an Interconnection View specifically, a scope that names no single root definition is not
always an empty diagram: when the scope directly includes one or more concrete top-level `part`
feature usages instead — for example `expose Subsystem::*;` where `Subsystem` is itself only a
namespace-like `part def` whose only nested content is a single `part` feature usage, so no
single `part def` qualifies as "the" root — those feature usages render directly, side by side,
with no enclosing frame around them. A scope that matches neither a root definition nor any
top-level `part` feature usage still renders the empty diagram described above.

Also specifically for the Interconnection View: when a nested part's own type is itself a
container (a `part def` with its own nested parts), how deep the diagram recurses into that
part's own interior depends on whether the resolved `expose` scope is recursive. A scope
containing at least one recursive form (`expose X::**;` or `expose X::*::**;`), or no `expose`
statement at all, recurses fully — every nested container's own interior is shown, at any depth.
A scope containing **only** non-recursive forms (`expose X;` and/or `expose X::*;`, with no
recursive form present anywhere in the view) limits expansion to the selected root's (or
top-level scoped feature's) own direct part children: a deeper nested part still renders as its
own box, but its own interior is not drawn. For example, `expose System; expose System::*;`
shows `System`'s direct part children as boxes, but does not expand into any of those parts' own
nested structure, even if their types have one — whereas `expose System::**;` shows every level.

Named `view Name { ... }` usages (not just `view def` declarations) are also now recognized as
their own renderable declarations: a workspace containing both `view def` declarations and named
`view` usages surfaces both kinds as views the `render` command discovers and renders.

### Interconnection View Connector Detail

An Interconnection View's connector endpoints now show the SysML port name from the connection's
endpoint reference (for example a connection between `StepperMotorX.encoder` and
`LBO3AxisGantry.J40` labels its two ports `encoder` and `J40`), instead of leaving every port
unlabeled. When several distinct connections wire the same two parts (for example separate
`power`, `encoder`, and `sensor` connections between one controller and one motor), each
connection now renders as its own independently-routed connector line, rather than visually
collapsing onto a single shared line. A connection whose endpoint reaches into a part nested
inside a container (for example `connect psu to board.cpu`) shows the port label for the true
nested target (`cpu`), but the connector line itself still terminates at the containing `board`
box's own boundary rather than continuing on to the inner part — routing a connector all the way
into a nested container remains a known limitation.

## Expose vs. Render: Worked Examples

The three view body statements look similar but do very different jobs. This is a common point
of confusion, so it is worth stating plainly:

> **`render <target>;` looks like it should select what's shown, but it does not — use
> `expose` for that.**

| Statement | What it actually does |
| --- | --- |
| `expose <name>;` | The **only** mechanism scoping which model content appears in the diagram (see above). |
| `render <renderingKind>;` | Selects a rendering style — see "View Body Statements" above. Never scopes content. |
| `filter <expr>;` | Narrows scope (Phase 1, and Phase 2a per bracketed `expose`); unsupported falls back unfiltered. |

### Example A: exposing a definition — exact vs. recursive

```sysml
package Vehicle {
    part def Engine {
        part cylinder[4];
    }
    part def Vehicle {
        part engine : Engine;
        part wheel[4];
    }
    part myVehicle : Vehicle;

    view EngineOnlyView {
        expose Engine;
        render asTreeDiagram;
    }

    view EngineRecursiveView {
        expose Engine::**;
        render asTreeDiagram;
    }
}
```

Both views declare `render asTreeDiagram;`, so they render via `BrowserViewLayoutStrategy` as an
indented tree of rows rather than the General View's nested boxes. `render` never narrows the
scope, only `expose` does — `BrowserViewLayoutStrategy` honors `expose` scoping identically to
every other layout strategy.

- `EngineOnlyView`'s bare `expose Engine;` is non-recursive (`MembershipExact`): it renders
  **only the `Engine` definition itself** — `cylinder` is **not** included, since a bare
  `expose X;` no longer implies the whole containment subtree. Confirmed by hand-rendering this
  exact fixture: the tree contains a single row, `Engine`.
- `EngineRecursiveView`'s `expose Engine::**;` is recursive (`MembershipRecursive`): it renders
  `Engine`'s **entire containment subtree** — `Engine` and its `cylinder` part (two rows) — the
  unchanged whole-subtree behavior from before this fix.

In both views, `Vehicle`, `myVehicle`, and `wheel` are excluded entirely, since neither `Engine`
nor its subtree contains them. Removing the `expose` statement (leaving only `render
asTreeDiagram;`, or an empty view body) renders the **full workspace** instead.

> **Note:** `expose` targets are qualified names (`::`-separated), not dotted member-access
> chains. `expose myVehicle.engine;` is a **syntax error**, not merely an unresolved reference —
> the grammar's `qualifiedName` rule does not accept `.`. To scope to a specific part *usage*,
> expose the usage itself by its own name (as in Example B below), not a dotted path into it.

### Example B: exposing a usage vs. exposing a definition

```sysml
package Vehicle {
    part def Engine {
        part cylinder[4];
    }
    part def Vehicle {
        part engine : Engine;
        part wheel[4];
    }
    part myVehicle : Vehicle;

    view UsageExposeView {
        expose myVehicle;
        render asTreeDiagram;
    }
}
```

Here `expose myVehicle;` names a **usage** (`myVehicle : Vehicle`), not a `def`, and is
non-recursive (`MembershipExact`). The tool resolves `myVehicle`'s own `Typing` edge to find the
definition it is typed by (`Vehicle`), and adds that resolved type to the scope too — using the
**same** exact-match (not whole-subtree) recursion kind, since the usage's own expose was itself
non-recursive. Confirmed by hand-rendering this exact fixture: the tree contains the `myVehicle`
row and the `Vehicle` row, but neither `Vehicle`'s own `engine`/`wheel` parts nor `Engine`'s
`cylinder` are included, because exact-match scoping does not pull in either exposed name's
descendants.

To render `myVehicle`'s and `Vehicle`'s full nested structure instead, expose recursively —
`expose myVehicle::**;` — which scopes to the union of `myVehicle`'s and `Vehicle`'s entire
containment subtrees (unchanged whole-subtree behavior), including `engine`, `wheel`, and (via
`engine`'s own type) `Engine`'s `cylinder` part.

Contrast this with `expose Vehicle;` (exposing the **definition** directly): that scopes to just
`Vehicle` itself (exact match), without needing to resolve any `Typing` edge, since a
definition's own qualified name is already the exact-match subject. Exposing a usage takes one
extra hop — through the usage's type reference — to add the same kind of definition to the scope
that exposing a `def` reaches directly; in both cases, recursion (`::**`) is what controls
whether descendants are included, independent of whether the initial target was a usage or a
definition.

## Depth Limiting

Use `--depth <n>` to limit the nesting depth rendered. Parts beyond the limit are replaced
with an ellipsis footer (`+N more…`). Silent omission is never used — truncation is always
visible in the output.

```bash
sysml2tools render model.sysml --output out --depth 3
```

## Output Formats

| Extension | Format | Notes |
| --- | --- | --- |
| `.svg` | SVG | Zero external dependencies |
| `.png` | PNG | SkiaSharp (MIT); pixel-identical across platforms |

PNG output uses an embedded Noto Sans font to guarantee pixel-identical output across
Windows, Linux, and macOS.

# Querying

The `query` command loads a workspace, resolves the semantic model, and answers
model-comprehension and analysis questions via 11 verbs. Every verb accepts
`--format markdown` (default) or `--format json`, and `--include-stdlib` to include
standard-library elements (excluded by default). Output is always sorted alphabetically by
qualified name, regardless of format, for stable and reproducible results.

`query <verb> --help` (and `help query <verb>`) shows a real example invocation for that
verb and a schema hint describing the Markdown/JSON output shape; `query --help` (and
`help query`, with no verb) shows a "typical workflow" note recommending `list`/`find` first
to discover exact qualified names before using an element-scoped verb.

```bash
# What does this element depend on? (outgoing edges: supertypes, typing, imports)
sysml2tools query uses --element Model::Vehicle "src/**/*.sysml"

# What depends on this element? (incoming edges)
sysml2tools query used-by --element Model::Engine "src/**/*.sysml"

# Transitive blast radius of a change, optionally bounded
sysml2tools query impact --element Model::Engine --depth 2 "src/**/*.sysml"

# A single-element "fact sheet": kind, supertypes, typing, annotations, children
sysml2tools query describe --element Model::Vehicle "src/**/*.sysml"

# Supertype/subtype tree
sysml2tools query hierarchy --element Model::Vehicle --direction both "src/**/*.sysml"

# Requirement satisfy/verify/allocate relationships
sysml2tools query requirements --element Model::Requirements::TopSpeed "src/**/*.sysml"

# Ports and typed features exposed by a definition
sysml2tools query interface --element Model::Vehicle "src/**/*.sysml"

# Resolved connection endpoints (including dotted feature chains)
sysml2tools query connections --element Model::Vehicle "src/**/*.sysml"

# States and guarded transitions
sysml2tools query states --element Model::VehicleStates "src/**/*.sysml"

# Enumerate elements matching a kind and/or name substring
sysml2tools query list --kind requirement "src/**/*.sysml"
sysml2tools query find --name Engine "src/**/*.sysml" --format json
```

## Query Output Formats

| Format | Flag | Notes |
| --- | --- | --- |
| Markdown | default, or `--format markdown` | Heading, summary bullets, table — readable by humans and LLMs |
| JSON | `--format json` | Source-generated (AOT-safe) serialization of the same result shape |

Markdown and JSON renderings of the same query always contain the same qualified names in
the same order, so either format can be relied on for automated comparisons.

## Verb Reference

| Verb | Requires `--element` | Answers |
| --- | --- | --- |
| `uses` | yes | What does this element depend on? |
| `used-by` | yes | What depends on this element? |
| `impact` | yes | What is transitively affected by a change (`--depth` to bound)? |
| `describe` | yes | What is this element (kind, supertypes, typing, annotations, children)? |
| `hierarchy` | yes | What is the supertype/subtype tree (`--direction up`\|`down`\|`both`)? |
| `requirements` | yes | What satisfy/verify/allocate relationships involve this element? |
| `interface` | yes | What ports/typed features does this definition expose? |
| `connections` | yes | What is this element connected to? |
| `states` | yes | What states and transitions does this element contain? |
| `list` | no | Enumerate elements, optionally filtered by `--kind`/`--name` |
| `find` | no | Search elements — requires `--kind` and/or `--name` |

# Exporting

The `export` command loads a workspace, resolves the semantic model, and dumps the *entire*
model — every declaration, every semantic edge, and every diagnostic — as a single JSON
document or as JSON Lines (JSONL). Unlike `query`, which answers a targeted analysis
question about one element, `export` is a lossless, bulk dump intended for offline/AI-
assisted analysis of a whole workspace at once (e.g., loading it into a separate tool,
`jq`-based scripting, or feeding an entire workspace's facts to an LLM in one shot).

```bash
# Export the whole workspace as a single indented JSON document (default format)
sysml2tools export "src/**/*.sysml"

# Export as JSON Lines (one compact JSON object per declaration/edge/diagnostic)
sysml2tools export "src/**/*.sysml" --format jsonl

# Write to a file instead of stdout
sysml2tools export "src/**/*.sysml" --format jsonl --output model.jsonl

# Include OMG standard library declarations/edges in the export
sysml2tools export "src/**/*.sysml" --include-stdlib

# Restrict output to a containment subtree, then narrow it with a filter expression
sysml2tools export "src/**/*.sysml" --target Vehicle::Engine --filter "@Deprecated"
```

## `export` Options

| Option | Description |
| --- | --- |
| `<globs>` | One or more glob patterns for `.sysml` input files |
| `--format json\|jsonl` | Output format (default: `json`) |
| `--output <file>` | Write to this **file** (default: stdout); `render`'s `--output` is a *directory* instead |
| `--include-stdlib` | Include OMG stdlib decls/edges (excluded by default); diagnostics are never stdlib-filtered |
| `--target <qualified-name>` | Restrict output to the containment subtree rooted at this element |
| `--filter <expr>` | Narrow output using a Phase 1 filter expression |

## Target Scoping and Filter Narrowing

`--target <qualified-name>` and `--filter <expr>` compose the same way `render`'s dynamic-view
`--view-target`/`--filter` pair does (see "Dynamic (Ad-Hoc) Views" above), but for `export`
instead of rendering: `--target` scopes the export to one element's containment subtree,
`--filter` narrows the declaration/edge set using the same Phase 1 filter-expression subset
(classification tests, boolean connectives, `(as Type).attribute` reads) — and, when both are
supplied, `--target` is applied **first**, with `--filter` narrowing the already-scoped result
**second**:

```bash
# Only the Engine subtree
sysml2tools export "src/**/*.sysml" --target Vehicle::Engine

# The whole workspace, narrowed to elements carrying @Deprecated
sysml2tools export "src/**/*.sysml" --filter @Deprecated

# The Engine subtree, further narrowed to elements carrying @Deprecated
sysml2tools export "src/**/*.sysml" --target Vehicle::Engine --filter @Deprecated
```

- If `--target` names a usage/feature (e.g. `part myEngine : Engine;`) rather than a
  definition, its resolved type's subtree is included too, so scoping to a usage still yields
  useful content instead of a near-empty result.
- An unresolvable `--target` (not present in the workspace, or a standard-library element
  without `--include-stdlib`) reports a clean `export: --target '<name>' was not found in the
  workspace.` error and produces no export — both cases share the same message.
- An unparsable or unsupported `--filter` expression does **not** abort the export: it falls
  back to the unfiltered (still `--target`-scoped, if applicable) result, appending a synthetic
  warning diagnostic (`"FilePath": "<--filter>"`) to the output's `Diagnostics` array and
  printing a matching `export: warning: ...` console message — the same graceful-degradation
  behavior views apply to a non-evaluatable `filter [<expr>];` statement.
- Exported edges always require both endpoints (source and target, when the source is
  non-null) to survive every active narrowing step (stdlib filtering, `--target` scoping, and
  `--filter` matching) — never just one endpoint.

## Export Output Shape

**`--format json`** (default) is a single indented document:

```json
{
  "Declarations": {
    "Model::Vehicle": { "$type": "definition", "Kind": "part def", "...": "..." },
    "Model::Engine": { "$type": "definition", "Kind": "part def", "...": "..." }
  },
  "Edges": [
    { "SourceQualifiedName": "Model::Engine", "TargetQualifiedName": "Model::Vehicle", "Kind": "Composition" }
  ],
  "Diagnostics": []
}
```

- `Declarations` is a JSON object keyed by qualified name (not an array), so a caller can
  look up a specific element directly instead of scanning an array.
- Each declaration is serialized using its own existing polymorphic `$type` discriminator
  (the same node types used internally by the parser/semantic model), so the export is a
  faithful, round-trip-capable dump — not a separate, narrower summary shape like `query`'s
  result.
- `Edges` and `Diagnostics` are plain JSON arrays.

**`--format jsonl`** emits one compact JSON object per line, each tagged with a `"Kind"`
discriminator so a line-oriented consumer (`grep`, `jq -c`, streaming parsers) can process
records without buffering the whole document:

```jsonl
{"Kind":"declaration","QualifiedName":"Model::Vehicle","Node":{"$type":"definition","...":"..."}}
{"Kind":"edge","SourceQualifiedName":"Model::Engine","TargetQualifiedName":"Model::Vehicle","EdgeKind":"Composition"}
{"Kind":"diagnostic","FilePath":"model.sysml","Line":1,"Column":1,"Severity":"Error","Message":"..."}
```

Declarations are emitted first, then edges, then diagnostics. Both output shapes exclude
OMG standard-library declarations and edges by default (mirroring `query`'s
`--include-stdlib` convention exactly); diagnostics are always included, since
`WorkspaceLoader` diagnostics only ever come from the user's own supplied files (the stdlib
symbol table is a pre-resolved seed, never re-parsed).

# Global Options

The following global options are accepted before the verb:

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation tests |
| `--results <file>`, `--result <file>` | Write validation results to `.trx` or `.xml` |
| `--depth <#>` | Set heading depth for validation output (default: 1) |
| `--log <file>` | Write all output to a log file |

## Getting Help

In addition to the global `-?`/`-h`/`--help` flag (see the table above), `help` is also a
first-class top-level command: `sysml2tools help [command] [verb]`. Both forms produce
identical output for the same target — `help <command>` and `<command> --help` share a
single source of truth for each command's help text, so neither can drift out of sync with
the other.

```bash
# Top-level help (same as bare --help)
sysml2tools help

# Command-specific help (identical to `lint --help`/`render --help`/`export --help`)
sysml2tools help lint
sysml2tools help render
sysml2tools help export

# Query verb overview (identical to `query --help`)
sysml2tools help query

# Query verb-specific help (identical to `query <verb> --help`)
sysml2tools help query hierarchy
```

An unrecognized command or verb (e.g., `sysml2tools help bogus`, `sysml2tools help query
bogus-verb`) reports a clear error naming the invalid token rather than crashing. Note that
`--silent` suppresses `help`'s console output exactly as it suppresses every other command's
output — there is no special case that lets `help` bypass `--silent`.

# Self-Validation

Self-validation exercises the tool against embedded test models and produces a structured
report. This provides tool qualification evidence for regulated environments.

```bash
sysml2tools --validate
sysml2tools --validate --results results.trx
sysml2tools --validate --results results.xml
```

The results file format is determined by the extension: `.trx` for MSTest TRX format,
`.xml` for JUnit XML format.

Use `--depth <#>` to embed the validation report at a specific heading level within a
larger markdown document:

```bash
sysml2tools --validate --depth 2
```

# NuGet Library Packages

SysML2Tools is structured as four NuGet packages. Library consumers can take a dependency
on the core library alone, without pulling in the full CLI tool:

| Package | Contents |
| --- | --- |
| `DemaConsulting.SysML2Tools.Language` | Library: SysML v2/KerML parser, AST, semantic model |
| `DemaConsulting.SysML2Tools.Stdlib` | Library: pre-compiled SysML v2 standard library |
| `DemaConsulting.SysML2Tools.Core` | Library: parser, semantic model, layout, `IRenderer` interface |
| `DemaConsulting.SysML2Tools.Tool` | CLI tool: `lint`, `render`, `query`, and `help` commands |

Consumers who need only the parsed semantic model, `LayoutTree`, or rendering interfaces
take a dependency on `DemaConsulting.SysML2Tools.Core` only, which automatically pulls in
`DemaConsulting.SysML2Tools.Language` and `DemaConsulting.SysML2Tools.Stdlib` as NuGet
dependencies. Consumers who need the CLI install `DemaConsulting.SysML2Tools.Tool` as a
dotnet tool. Each package ships its own generated Markdown API reference documentation
alongside its assembly.

# Continuous Compliance

This project follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology.
Compliance evidence (requirements, trace matrix, quality reports) is generated automatically
on every CI run.

## References

N/A
