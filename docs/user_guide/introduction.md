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

The workflow below shows how SysML2Tools is used: author SysML v2 models as `.sysml` text with
views and metadata annotations, run the `sysml2tools` `lint`, `render`, `query`, and `export`
commands locally or in a CI pipeline, and use the resulting diagnostics, diagrams, query reports,
and model exports as a CI quality gate, in design documents, and to let AI agents fix and question
the model.

![SysML2Tools workflow: author .sysml model files with views and metadata annotations, run sysml2tools lint,
render, query, or export, get diagnostics, SVG or PNG diagrams, Markdown or JSON query reports, and JSON or
JSONL model exports, then use them as a CI quality gate, in design documents, with AI agents, or for bulk
analysis](workflow.svg)

For library consumers, the architecture diagram below shows how the NuGet packages fit together:
the Tool's commands call into the Core library, which builds on the Language library seeded with
the pre-compiled Stdlib, to turn model files into diagnostics, diagrams, query results, and model
JSON.

![SysML2Tools architecture: model files flow from the Tool commands through the Core library and the Language library, which is seeded by the Stdlib, to produce diagnostics, diagrams, query results, and model JSON](architecture.svg)

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

Install the tool globally using the .NET CLI. Installing requires the .NET SDK, and running the
tool requires the .NET 10 runtime; the
`DemaConsulting.SysML2Tools.Core`, `.Language`, and `.Stdlib` library packages support .NET 8, 9,
10, and 11.

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

# Diagnostic Format

Every command that prints diagnostics — `lint`, `render`, and `query` — prints each one as a
single line in the same compiler-style form, produced by one shared formatter:

```text
path(line,col): severity: message
```

- `path` is the file the diagnostic is about, as it was resolved from your patterns.
- `line` and `col` are both one-based: the first character of a file is line 1, column 1. This is
  the convention of MSBuild, GCC, editor problem matchers, and GitHub annotations.
- `severity` is `error`, `warning`, or `info`, in lowercase.
- A diagnostic that is not tied to a file has no `path(line,col):` prefix and prints as just
  `severity: message`.

Errors are written to standard error and everything else to standard output, so the lines are
suitable for CI problem matchers, editor navigation, and AI-assisted model-fix loops that parse
them.

For an `Unresolved reference` warning the position is that of the reference text as written. For
example, with `part def X :> DoesNotExist;` indented four spaces on line 2 of `c.sysml`, the
reference `DoesNotExist` starts at column 19:

```text
c.sysml(2,19): warning: Unresolved reference: 'DoesNotExist'
```

The position is that of the first character of the reference (`DoesNotExist`) for a supertype,
subsetting, typing, redefinition, import, `expose`, view-definition, connector (`connect`/`bind`)
endpoint, transition source or target, `satisfy`, `verify`, `allocate`, `dependency`, or metadata
reference. A name that is synthesized rather than written (for example the implied source of a
`then` succession) is reported at the position of the declaration that contains it. A circular
import is reported at the `import` statement that closes the cycle, and a cyclic specialization
at the supertype reference that closes it.

If the same name is unresolved several times in one file it is reported once, at its first use.

`render` and `query` print the same lines for the diagnostics found while loading your model.
For `query --format json`, those lines are still written to standard output before the JSON
document, exactly as before.

The diagnostics in the JSON produced by `export` carry `FilePath`, `Line`, `Column`, `Severity`,
and `Message` as separate fields, using the same one-based `Line` and `Column` as the console
lines above (`0` when the position is unknown). Syntax-error and unresolved-reference columns
changed from zero-based to one-based in this release.

# Linting

The `lint` command loads a workspace, resolves the semantic model, and reports all
diagnostics. It exits with a non-zero code if any errors are present.

```bash
sysml2tools lint "src/**/*.sysml"
```

Diagnostic output includes file path, line, column, severity, and message, in the format
described under [Diagnostic Format](#diagnostic-format):

```text
model.sysml(12,19): warning: Unresolved reference: 'VehicleSystem'
model.sysml(34,4): error: mismatched input ';' expecting IDENTIFIER
```

This structured output is suitable for:

- CI/CD pipelines that fail the build on model errors
- AI-assisted model authoring loops that parse diagnostics and propose fixes

References are resolved by the SysML v2 / KerML name-resolution rules, so an
`Unresolved reference` warning also reports a name that exists but is not reachable from
where it is used:

- a **private** (or, outside a specialization, **protected**) member of another package
  referenced through a qualified name (`P::Hidden`), short name, or alias;
- a candidate a **filtered import** (`import P::*[@Safety];`) rejects;
- a name that is only reachable through an `import` declared in a *different* package — an
  import applies to the package that declares it (and the packages nested in it), while an
  import written at the root of a file applies to that whole file.

Add an import to the package that needs the name, qualify the reference, make the member
public (or use `import all`), or adjust the filter. Such a reference is reported once, by the
`Unresolved reference` warning, and is **not drawn** by the layout views: a General view draws no
edge for it, an Interconnection view does not expand the part, and a Grid view does not mark the
specialization (compartment rows still show the written `: Type` text). The layouts never match
it to a same-named element elsewhere, and add no second warning. A package-level `filter`
statement is not honored when resolving imports.

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

  > **Known limitation — sequence view.** A lifeline is a declared `part`, `item`, or `occurrence`
  > usage, or else the first segment of a `message` usage's endpoint reference. The `sequence`
  > pre-check approximates "at least one lifeline and message" as "at least one nested `message`
  > usage" — necessary (zero messages guarantees an empty diagram) but
  > **not sufficient**: a target whose message endpoints fail to resolve to any lifeline still
  > passes this pre-check yet still renders the near-blank canonical `LayoutTree` sentinel. A
  > full message-edge-walk validation was deliberately not implemented for this check (see
  > `ROADMAP.md`'s "View dynamics refinements" item); this is a documented gap, not a silent
  > omission.

## Choosing the Diagram Type

The standard SysML v2 way to choose a diagram type is to type the view by one of the standard
view definitions from the `StandardViewDefinitions` library package:

```sysml
package MyDesignViews {
    private import StandardViewDefinitions::*;

    view SystemStructure : GeneralView {
        expose MySystem::**;
        filter not @SysML::Documentation;
    }

    view SystemInternals : InterconnectionView {
        expose MySystem::**;
    }
}
```

`GeneralView`, `InterconnectionView`, `ActionFlowView`, `StateTransitionView`, `SequenceView`,
`GridView`, and `BrowserView` select the matching diagram. The name must resolve, so either
import `StandardViewDefinitions::*` as above or write it qualified
(`view SystemStructure : StandardViewDefinitions::GeneralView { ... }`). A view may also be typed
by your own `view def` that specializes a standard one (`view def SystemsView :> InterconnectionView;`);
it then inherits that definition's diagram type, its `render` member, and its `filter` conditions.
`GeometryView` is not supported and renders as a General View.

The diagram type is chosen in this order:

1. The view's own `render` member: `render asTreeDiagram;` (browser) or
   `render asInterconnectionDiagram;`, plus the tool-defined `asGeneralDiagram`,
   `asStateTransitionDiagram`, `asActionFlowDiagram`, `asSequenceDiagram`, and `asGridDiagram`.
   `Views::asTreeDiagram` is accepted too. `asElementTable` and `asTextualNotation` have no
   matching diagram and are ignored.
2. The view's view definition, nearest first: a definition's `render` member, or a standard view
   definition.
3. Otherwise, a tool-defined fallback that looks for `Interconnection`, `StateTransition`/`State`,
   `ActionFlow`/`Action`, `Grid`/`Matrix`/`Tabular`, `Browser`/`Tree`, or `Sequence` in the view's
   name or declared supertypes, and renders a General View when none matches. Prefer typing the
   view, since this fallback depends on naming.

An Interconnection View that exposes several independent systems (for example
`expose SystemA::**; expose SystemB::**;`) draws every system's frame side by side, in declaration
order.

### General View Notation

A General View follows the SysML v2 graphical notation and the look of the OMG reference
implementation:

- A **definition** is a box with square corners and a `«part def»`-style keyword; a **usage** is a
  box with rounded corners and a keyword such as `«part»`. The keyword carries the prefix words
  `abstract`, `variation`, `individual`, `variant`, and `ref` (for example `«abstract part def»`,
  `«ref part»`). The name line shows the short name, the name, and the declared specialization,
  typing, multiplicity, and subsetting or redefinition (`motors : Motor [2]`, `frontMotor :> motors`).
- By default each part, item, action, state, requirement, and similar usage is drawn as a node of its
  own, placed beside its owner and joined to it. A port is drawn as a node only when it owns nested
  features. **Attributes, plain references, and ports without nested features stay rows** in the
  owner's compartments (`attributes`, `ports`, ...), which have centered italic titles.
- An abstract definition has an italic name; an abstract usage is marked only by its keyword. A
  reference usage has a dashed outline.
- A package is a folder with its name in the tab; a package nested in another package is drawn inside its
  owner's folder.
- The diagram flows **top to bottom**: owners and general types are on top, the elements they own or
  specialize below them, and the definition that types a usage below the usage.

Lines are solid, except imports, dependencies, and comment links, which are dashed.

| Line end or label | Meaning |
| --- | --- |
| Filled diamond (◆) at the owner | Composite usage: the owner owns the part |
| Hollow diamond (◇) at the owner | Reference usage: the owner refers to it without owning it |
| Hollow triangle with two dots at the definition | Feature typing (`part wheel : Wheel`) |
| Hollow triangle | Specialization of definitions; subsetting of usages (`:>`) |
| Hollow triangle with four dots | Reference subsetting (`::>`) |
| Hollow triangle with a bar | Redefinition (`:>>`) |
| Open arrowhead, `«satisfy»` or `«verify»` | A design element satisfies, or a verification verifies, a requirement |
| Dashed, open arrowhead, `«import»` | Package import; `«import»*` for `::*` and `«import»**` for `::**` |
| Dashed, no arrowhead, to a folded note | A `comment` on the element (documentation is a `doc` compartment) |
| Plain solid line, no end marker | `connect` between two features |
| Plain solid line labeled `=` | `bind` between two features |
| Dashed line with an open arrowhead | `dependency` |
| Solid line with an open arrowhead, `«allocate»` | `allocate` |

The documentation and comments of a usage that is drawn as its own box (a part, item, action, or
similar usage, or a port with nested features) appear on that usage's box. Attributes, plain
references, and ports without nested features stay rows in their owner's compartments, and their
documentation and comments are not drawn; this follows the OMG reference implementation, and drawing
them (for example as a dashed link to the row) is a planned follow-up (see the `ROADMAP.md` file).
Satisfy, verify, and import lines are drawn only when both ends are drawn in the view, and the
standard library is never drawn. The choice of which usages are nodes is fixed to this default; an
option to draw every usage as a node, or to list every usage as a row, is planned (see the
`ROADMAP.md` file).

A General View draws a package nested inside another package as a folder inside its owner's
folder, and it draws every relationship between two drawn elements — including a specialization
that crosses from one package into another — as a connector between the two boxes themselves, not
between the enclosing folders. A typing that crosses packages is drawn from the usage's own rounded
box (for example `engine : Powertrain::Engine` inside `Car`) to the box of the definition that
types it, not from the owning definition to the type definition. The repository's diagram gallery
shows these in the examples
`11-nested-packages-general.sysml` (nested packages and cross-package edges),
`14-usages-typing-general.sysml` (usages, typing, subsetting, redefinition, reference and abstract
styling), `15-doc-comment-general.sysml` (documentation and comments on nested parts), and
`16-nested-packages-typing-general.sysml` (nested packages with typed usages, such as the `Vehicle`
package).

### State Transition and Action Flow View Notation

The State Transition View and the Action Flow View draw a behavior with the notation of SysML v2
(graphical notation, clause 8.2.3). Every construct below comes from the textual model; the
repository's diagram gallery shows them in `03-elevator-state.sysml`,
`04-pipeline-action-flow.sysml`, and the two small showcase models `12-heater-state.sysml` and
`13-order-action-flow.sysml`.

State Transition View:

| Textual form | Drawn as |
| --- | --- |
| `state s { entry action a; }` | an `entry`/`do`/`exit` compartment per kind, with a centered italic title |
| `state s : Type;` | `s : Type` as the state's name line |
| `accept t if g do action e then s2;` | a transition labeled `t [g] / e`; each part appears only when declared |
| `first start then s;` | the initial state: a filled circle and an open arrow into `s` |
| `then done;` | a final state: one bullseye that every `done` transition reaches |

An undeclared initial transition draws no initial marker. A composite state (one with nested states)
is drawn as an ordinary box and its nested states are not drawn.

Action Flow View:

| Textual form | Drawn as |
| --- | --- |
| `fork f;` / `join j;` | a thick bar with no label, long enough for its connections |
| `decide d;` / `merge m;` | a diamond |
| `if g then a;` | the succession labeled `[g]` |
| `else a;` | the succession labeled `else` |
| `terminate;` | a crossed circle; a flow that ends in it has no done marker |
| `action a accept Signal;` | a box with the accept icon in its corner and the payload `accept Signal` in its body |
| `action a send msg to target;` | a box with the send icon in its corner and the payload in its body |
| `action a : Type [2];` | `«action» a : Type [2]` |
| `first start then a;` / `then done;` | the start marker enters `a` / the done marker follows the action |

Without an explicit `first start` or `then done` the start marker enters the actions that have no
predecessor and the done marker follows the actions that have no successor (not counting a terminate
node). A model that uses `then done;` reports the lint warning `Unresolved reference: 'done'`;
the diagram is unaffected. Nested actions, action parameters, swim-lanes, and control structures
(`if`, `while`, `for`) are not drawn.

### Interconnection View Notation

The Interconnection View draws the internal structure of a part definition or part usage with the
notation of SysML v2 (graphical notation, clauses 8.2.3.11 to 8.2.3.16). The repository's diagram gallery
shows it in `02-computer-interconnection.sysml`, `08-nested-interconnection.sysml`, and
`09-motor-controller-multi-port.sysml`.

Nodes (a rounded box with the keyword in guillemet marks above the name line):

| Textual form | Drawn as |
| --- | --- |
| `part cpu : Cpu;` | `«part»` over `cpu : Cpu` |
| `part memory : Ram [2];` | `«part»` over `memory : Ram [2]` |
| `ref part monitor : Display;` | a dashed outline, `«ref part»` over `monitor : Display` |
| `item firmware : Firmware;` | `«item»` over `firmware : Firmware`; `action`, `state`, `occurrence` likewise |
| `part cpu { doc /* ... */ attribute clock : Frequency; }` | `doc` and `attributes` compartments in the box |
| `part outer { part a; part b; }` | a container with `a` and `b` drawn inside it |

The frame of the root is a square-cornered box headed `«part def» Name` for a part definition, or a
rounded box headed `«part» name : Type` for a part usage declared in a package whose body declares nested
parts and connectors (a usage is a root only when there is no part definition to show, or when the view
exposes it). An italic `interconnection` title separates the heading from the contents.

Ports are small white rounded squares on the border of the part, with the label `name : Type` outside it:

| Textual form | Drawn as |
| --- | --- |
| `in port p : T;` / `out port p : T;` / `inout port p : T;` | an arrow: into the part, out of it, or both ways |
| `port p : T;` | a square without an arrow |
| `ref port p : T;` | a dashed square |
| `in port p : ~T;` | the label `p : ~T` |

A part's ports are the ports it declares in its own body, in its type, and in the supertypes of that type. A
port that no drawn connector names is still drawn, on the left face of the part (the right face for an `out`
port). The declared ports of the root itself are drawn on its frame; connectors to a frame port are not
drawn. A connector endpoint that names only a part (`connect a to b`) attaches to the part, with no port.
An endpoint segment that is not a declared port (`connect a.x to b.y` where no port `x` is declared) is
drawn as an undirected square labeled with the bare name.

Connectors (`connection`, `binding`, `interface`, and `flow` usages; `message` and `allocation` usages are
not drawn in this view):

| Textual form | Drawn as |
| --- | --- |
| `connection c connect a.p to b.q;` | a plain line |
| `bind a.p = b.q;` | a plain line labeled `=` |
| `interface bus connect a.p to b.q;` | a plain line labeled `«interface» bus` |
| `flow f of Torque from a.out to b.in;` | a line with a filled arrowhead, labeled `«flow» f of Torque` |
| `connect [2] supply ::> a.p to drain ::> b.q;` | role names `supply`, `drain` above the line ends, `[2]` below |

The names of plain connections are not drawn (the connection label is optional in the notation).
A port that several connectors name is one port, labeled once; the layout engine currently draws a glyph
for each of its connectors (a known engine limitation). When a connector label would overlap the port labels at its
ends, the space between the boxes is widened.

### Sequence View Notation

The Sequence View draws the interaction of a part definition with the notation of SysML v2 (graphical
notation, clause 8.2.3.12). The repository's diagram gallery shows it in `05-oauth-sequence.sysml`.

Lifelines (a rounded box with the keyword in guillemet marks above the name line, and a dashed stem below):

| Textual form | Drawn as |
| --- | --- |
| `part browser : Browser;` | `«part»` over `browser : Browser` |
| `ref part resourceServer : ResourceServer;` | a dashed outline, `«ref part»` over `resourceServer : ResourceServer` |
| `item token : Token;` | `«item»` over `token : Token`; `occurrence` likewise |

Every `part`, `item`, and `occurrence` usage that the part definition declares is a lifeline, in declaration
order, whether or not it exchanges a message. A message endpoint whose first segment names no declared
usage still gets a lifeline, after the declared ones, drawn with its plain name and no keyword.

Messages (a horizontal arrow from the sender to the receiver with a hollow triangular head; an arrow from a
lifeline to itself is drawn as a small loop):

| Textual form | Drawn as |
| --- | --- |
| `message request from client.send to server.receive;` | an arrow labeled `request` |
| `message grant of Token from authServer.issue to browser.accept;` | an arrow labeled `grant of Token` |

The order of the messages from top to bottom follows the events they connect. A lifeline declares its events
with `event occurrence` usages in order (`event occurrence a; then event occurrence b;`), and a message
endpoint names a lifeline and one of its events (`client.send`). A message is drawn above another when one of
its events comes before one of the other's events on the same lifeline, so messages may be declared in any
order. The messages that the events do not order, and every message of a model that declares no events, keep
declaration order. Lifelines are spaced wide enough for the longest message label. Proxies, found and lost
messages, sequence ports, and combined fragments (`alt`, `opt`, `loop`) are not drawn; a combined fragment is
not part of the SysML v2 sequence notation.

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
    subtree. When `X` is a package, the General View draws it as an empty package node. If `X`
    resolves to a usage (e.g. `part myVehicle : Vehicle;`) rather than a definition, its resolved
    type (`Vehicle`) is also included, itself only (not the type's subtree either).
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
Transition, Action Flow, and Sequence Views render the contents of a selected root (an
Interconnection View draws several roots side by side when the scope exposes several independent
systems), so
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
part's own interior depends on whether that specific part's own branch is matched by a recursive
`expose` subject — decided **independently for each top-level part**, not once for the whole
diagram. A part matched by at least one recursive form (`expose X::**;` or `expose X::*::**;`),
or any part at all when there is no `expose` statement, has its own branch recurse fully — every
nested container's own interior is shown within that branch, at any depth. A part matched **only**
by non-recursive forms (`expose X;` and/or `expose X::*;`) has its own branch limited to that
part's own direct part children: a deeper nested part still renders as its own box within that
branch, but its own interior is not drawn. Because this decision is per-branch, a single view can
mix both outcomes: `expose SystemA::**; expose SystemB;` fully recurses into `SystemA`'s own
composed structure while stopping `SystemB`'s branch at its own direct children, in the very same
diagram — a sibling part matched by a different subject is never affected by another part's own
recursion kind. For example, `expose System; expose System::*;` shows `System`'s direct part
children as boxes, but does not expand into any of those parts' own nested structure, even if
their types have one — whereas `expose System::**;` shows every level.

When the no-single-root scoped fallback (above) collects top-level parts from more than one
containing definition, a connection declared inside one of those definitions always resolves
against that definition's own top-level parts only — even if a different exposed definition
happens to declare a same-named part.

Named `view Name { ... }` usages (not just `view def` declarations) are also now recognized as
their own renderable declarations: a workspace containing both `view def` declarations and named
`view` usages surfaces both kinds as views the `render` command discovers and renders.

### Interconnection View Connector Detail

An Interconnection View's connector endpoints show the port the connection's endpoint reference
names (for example a connection between `StepperMotorX.encoder` and `LBO3AxisGantry.J40` draws its two
ports labeled `encoder` and `J40` when they are not declared, and `encoder : EncoderPort` when they are; see
*Interconnection View Notation*). When several distinct connections wire the same two parts (for example separate
`power`, `encoder`, and `sensor` connections between one controller and one motor), each
connection now renders as its own independently-routed connector line, rather than visually
collapsing onto a single shared line. A connection whose endpoint reaches into a part nested
inside a container (for example `connect psu to board.cpu`) shows the port label for the true
nested target (`cpu`), but the connector line itself still terminates at the containing `board`
box's own boundary rather than continuing on to the inner part — routing a connector all the way
into a nested container remains a known limitation.

### Comments and Documentation

In SysML v2, `doc /* ... */` documentation and `comment /* ... */` comments are owned members of
the element they annotate, so `expose X::**;` (or `expose X::*;`) exposes them along with X's other
members, while a bare `expose X;` does not. In a General View, exposed documentation is drawn as a
word-wrapped `doc` compartment inside its element's box, and an exposed comment as a folded-corner
note attached to its element. Documentation and comments are drawn for elements that are boxes; an
attribute, plain reference, or port without nested features is a compartment row of its owner, and its
documentation and comments are not drawn. Interconnection and other view types never draw them.

Like any exposed element, documentation and comments are only drawn when they satisfy the view's
conditions. Use the standard view condition to hide documentation (comments remain):

```sysml
view Overview {
    expose MySystem::**;
    filter not @SysML::Documentation;
}
```

View conditions (`filter <expr>;`) select exposed elements by metaclass (`@SysML::PartUsage`,
`@SysML::PartDefinition`, `@SysML::Documentation`, `@SysML::Comment`, and so on, also accepted as
`@KerML::…`) or by applied metadata, combined with `not`, `and`, `or`, and `xor`. All of a view's
conditions apply, including those inherited from its view definition
(`view def NoDocs { filter not @SysML::Documentation; }` then `view Overview : NoDocs { … }`). A
positive condition such as `filter @SysML::PartDefinition;` also hides documentation and comments,
since they are not part definitions. An expose filter applies a condition to one expose only:
`expose MySystem::**[not @SysML::Documentation];`.

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

## Empty Views and Lone Package Exposes

Per SysML v2 (8.3.26.3), `expose P;` exposes the package element `P` itself, so the General View
draws it as a package node (tabbed folder symbol with its name) without its contents; `expose P::*;`
exposes only `P`'s members (no package node); and `expose P::**;` exposes `P` and all its members
recursively (the package node containing its members). When a package is exposed alone in a view
laid out as a General View, `render`
prints an informational hint, located at the view's declaration in the same
`file(line,col): severity:` form `lint` uses (see [Diagnostic Format](#diagnostic-format)):

```text
design-views.sysml(9,5): info: View 'SoftwareStructureView': 'expose BuildMark;' exposes the
  package element itself, so it is drawn as a package node without contents; use
  'expose BuildMark::**;' to also show the members.
```

Only a view whose diagram contains nothing renderable at all is reported as a `warning`. In both
cases the output file is still written, and the exit code is unchanged (each message is printed on
a single line). The `lint` command does not lay views out, so it does not report these conditions.

## Depth Limiting

Use `--walk-depth <n>` to limit the nesting depth rendered. Parts beyond the limit are replaced
with an ellipsis marker (`…`): a trailing `…` row in a definition whose nested definitions are
hidden, and a bare `…` in a collapsed package. Silent omission is never used — truncation is
always visible in the output. In a General View every enclosing package counts as one nesting
level, the same as a definition nested inside another definition: with `--walk-depth 1` each
top-level package is drawn collapsed, and with `--walk-depth 2` its own definitions are drawn while
its nested packages are collapsed. A collapsed package hides everything it contains, including
nested packages, and relationships to hidden definitions are not drawn.

```bash
sysml2tools render model.sysml --output out --walk-depth 3
```

## Output Formats

| Extension | Format | Notes |
| --- | --- | --- |
| `.svg` | SVG | Zero external dependencies |
| `.png` | PNG | CanvasNet (MIT); pixel-identical across platforms |

PNG output uses an embedded Noto Sans font to guarantee pixel-identical output across
Windows, Linux, and macOS.

# Querying

The `query` command loads a workspace, resolves the semantic model, and answers
model-comprehension and analysis questions via 12 verbs. Every verb accepts
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

# Combined "Dependencies" section prose: what it depends on, and what depends on it
sysml2tools query dependencies --element Model::Engine "src/**/*.sysml"

# Transitive blast radius of a change, optionally bounded
sysml2tools query impact --element Model::Engine --walk-depth 2 "src/**/*.sysml"

# Also follow connect/bind edges, so a part joined to the rest of the assembly only by
# connectors is no longer reported as impacting nothing
sysml2tools query impact --element Model::System::motorA --include-connections "src/**/*.sysml"

# A single-element "fact sheet": kind, supertypes, typing, annotations, applied metadata, children
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

# Embed the report under a custom heading (e.g., when appending to a larger document)
sysml2tools query describe --element Model::Vehicle --depth 2 --heading "Vehicle Report" "src/**/*.sysml"
```

## Connection-Aware Impact Analysis

By default, `query impact` follows only *reference* relationships — specialization, typing,
imports, and similar resolved references — in the reverse direction. A part that is joined to
the rest of an assembly purely by `connect` statements therefore reports no impacted elements,
because a connector is not a reference.

> **Upgrade note — the default `impact` result changed.** In releases before connection-aware
> impact analysis existed, `query impact` followed `connect` and `bind` relationships as if they
> were ordinary references, whenever a connector named a directly declared element (for example
> `connect b to a;` or `connect hub.J1 to motorA;`). Those connectors were reported as their raw
> endpoint — including nested port names such as
> `Model::System::hub::J1`, which cannot themselves be used as an `--element` subject. That was
> never intended, is inconsistent with the reference-only default described above, and has been
> corrected. As a result, a
> `query impact` command that you have not changed may now report **fewer** rows than it used to
> — often none — on models that rely on `connect` statements. To get those elements back,
> deliberately add `--include-connections`, which now reports them correctly rolled up to their
> owning part.
>
> **Upgrade note — connection-aware depth changed since `0.2.0-beta.1`.** In `0.2.0-beta.1`,
> `--include-connections` followed at most **one** connector per traversal path unless you
> supplied `--walk-depth`, so leaving `--walk-depth` off silently meant "one hop" for connectors
> while meaning "unlimited" for references. `--walk-depth` is now the single depth control and
> counts every relationship equally, so `--include-connections` with no `--walk-depth` now
> follows a connector chain all the way to its end. A command you have not changed may therefore
> report **more** rows than it did in `0.2.0-beta.1`. There is no longer an exact equivalent of
> the old behavior, because the one-hop bound applied to connectors only while references stayed
> unlimited; pass the proximity you actually want instead, such as `--walk-depth 1` for immediate
> neighbors of every kind.

Adding `--include-connections` makes `impact` follow `connect` and `bind` relationships as
well. Three rules apply:

- **Connectors are followed in both directions.** A connector's two ends carry no
  "source causes target" meaning, so the connected element is reported no matter which end you
  query from. Querying a motor reports the hub it is plugged into, and querying that hub
  reports every motor plugged into it.
- **Port endpoints roll up to the part that owns them.** Connectors join nested ports, but you
  normally ask about parts. An endpoint such as `System::hub::J1` is reported as
  `System::hub`, with the actual port named in the entry's notes (and in the
  `ViaQualifiedName` field of `--format json` output) so nothing is lost. Roll-up applies only
  to endpoints that are not themselves declared elements — typically ports inherited through a
  typed usage such as `part hub : Hub`. An endpoint that *is* a declared element, such as a
  directly connected sibling part (`connect alpha to beta;`) or a port declared inline on a
  usage, is reported as-is, and `ViaQualifiedName` is then absent.
- **One uniform depth.** `--walk-depth <n>` counts every relationship equally — a `connect`
  hop, a `bind` hop, a specialization, and a typing reference each cost exactly one. So
  `--walk-depth 3` means "everything within 3 relationships of this element", whatever kinds of
  relationship those turn out to be. Omitting `--walk-depth` means *unlimited* for connectors
  exactly as it always has for references. `--include-connections` decides only *which*
  relationships exist in the graph, never how far the walk goes.

Because real models connect many parts to a shared hub or bus, an unlimited connection-aware
walk on such an assembly can reach the whole assembly. That is worth knowing when you want
proximity rather than reachability: pass `--walk-depth <n>` to ask for the neighborhood you
actually care about. Every reported element carries the distance at which it was found, and
that distance is always the *shortest* number of relationships separating it from your subject.

Omitting the flag leaves reference-only results completely unchanged, and adding it never
removes an element: every element reported without the flag is still reported with it. It can,
however, change how an already-reported element is described. An element that a reference path
reaches at depth 2 may be reached over a connector at depth 1, and is then reported at the
lower depth with `Relation` `Connect` and the connector named in its notes, instead of at the
higher depth with its reference relation. `--format json` entries
additionally carry `Depth` (the traversal depth), `Relation` (`Connect`/`Binding` for a
connector, or the reference edge kind otherwise), and `ViaQualifiedName`, so scripts can
distinguish "referenced by" from "connected to" without parsing the human-readable detail text.

## Query Output Formats

| Format | Flag | Notes |
| --- | --- | --- |
| Markdown | default, or `--format markdown` | Heading, summary bullets, table (prose bullets for `dependencies`) |
| JSON | `--format json` | Source-generated (AOT-safe) serialization of the same result shape |

Markdown and JSON renderings of the same query always contain the same qualified names in
the same order, so either format can be relied on for automated comparisons.

## Verb Reference

| Verb | Requires `--element` | Answers |
| --- | --- | --- |
| `uses` | yes | What does this element depend on? |
| `used-by` | yes | What depends on this element? |
| `dependencies` | yes | What does this element depend on, and what depends on it (combined, as prose)? |
| `impact` | yes | What is transitively affected by a change (`--walk-depth`, `--include-connections`)? |
| `describe` | yes | What is this element (kind, supertypes, typing, annotations, applied metadata, children)? |
| `hierarchy` | yes | What is the supertype/subtype tree (`--direction up`\|`down`\|`both`)? |
| `requirements` | yes | What satisfy/verify/allocate relationships involve this element? |
| `interface` | yes | What ports/typed features does this definition expose? |
| `connections` | yes | What is this element connected to? |
| `states` | yes | What states and transitions does this element contain? |
| `list` | no | Enumerate elements, optionally filtered by `--kind`/`--name` |
| `find` | no | Search elements — requires `--kind` and/or `--name` |

## `query` Options

| Option | Description |
| --- | --- |
| `--element <name>`, `-e <name>` | Qualified name of the target element; required for every verb except `list`/`find` |
| `--format markdown\|json` | Output format (default: `markdown`); distinct from `render`'s `--format` (`svg`/`png`) |
| `--output <file>` | Write to this **file** (default: stdout); `render`'s `--output` is a *directory* instead |
| `--walk-depth <#>` | Max impact-walk depth (`impact` only); all relationship kinds count equally, unlimited default |
| `--include-connections` | `impact` only: also follow `connect`/`bind` edges, undirected (see above) |
| `--direction up\|down\|both` | Traversal direction (`hierarchy` verb only) |
| `--kind <kind>` | Element-kind filter (`list`/`find` verbs only) |
| `--name <substring>` | Name substring filter (`list`/`find` verbs only) |
| `--include-stdlib` | Include OMG standard library elements in results |
| `--depth <#>` | Markdown heading depth (1-6, default: 1); Markdown output only, no effect on `--format json` |
| `--heading <text>` | Replaces default `# query <verb>[: <element>]`; Markdown only, no effect on JSON |

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
    { "SourceQualifiedName": "Model::Engine", "TargetQualifiedName": "Model::Vehicle", "Kind": "Composition" },
    { "SourceQualifiedName": "Model::Truck", "TargetQualifiedName": "Model::Vehicle",
      "Kind": "Supertype", "RawReference": "Vehicle" },
    { "SourceQualifiedName": null, "TargetQualifiedName": "Model", "Kind": "Import", "RawReference": "Model::*" }
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
- An edge may carry an optional `RawReference`: the reference text as written in the
  model (`Vehicle`, `P::Vehicle`) that the edge was resolved from, so a consumer can tell which
  reference of a declaration an edge answers (for example, which of `:> X, A` resolved).
  Tokens are concatenated without whitespace (`P :: X` is `P::X`). For `import` and `expose`
  edges the text keeps the wildcard and recursive markers as written — `import P;` is `P`,
  `import P::*;` is `P::*`, `import P::**;` is `P::**`, and `expose P::*::**;` is `P::*::**` — so
  they stay distinguishable even though they resolve to the same target; a trailing `[filter]`
  expression is not part of the text (`expose P::**[@Safety];` is `P::**`).
  It is present for supertype, typing, redefinition, import, verify, expose, and metadata-type
  edges, and omitted for the two-ended edge kinds (satisfy, allocate, dependency, connect,
  binding, transition) and when no reference text is recorded. The field is additive: consumers
  that do not know it can ignore it.

**`--format jsonl`** emits one compact JSON object per line, each tagged with a `"Kind"`
discriminator so a line-oriented consumer (`grep`, `jq -c`, streaming parsers) can process
records without buffering the whole document:

```jsonl
{"Kind":"declaration","QualifiedName":"Model::Vehicle","Node":{"$type":"definition","...":"..."}}
{"Kind":"edge","SourceQualifiedName":"Model::Engine","TargetQualifiedName":"Model::Vehicle","EdgeKind":"Composition"}
{"Kind":"edge","SourceQualifiedName":"Model::Truck","TargetQualifiedName":"Model::Vehicle","EdgeKind":"Supertype","RawReference":"Vehicle"}
{"Kind":"diagnostic","FilePath":"model.sysml","Line":1,"Column":1,"Severity":"Error","Message":"..."}
```

Declarations are emitted first, then edges, then diagnostics. An edge line includes
`RawReference` under the same conditions as the JSON form. Both output shapes exclude
OMG standard-library declarations and edges by default (mirroring `query`'s
`--include-stdlib` convention exactly); diagnostics are always included, since
`WorkspaceLoader` diagnostics only ever come from the user's own supplied files (the stdlib
symbol table is a pre-resolved seed, never re-parsed).

### Modifier and notation data on nodes

Declaration nodes carry additional optional properties that capture what the SysML v2 notation
displays. Each appears **only when it is set**, so a node that does not use the syntax is exported
exactly as before, and a consumer that does not know a property can ignore it. The property
names are written exactly as shown (no case conversion), like the other node properties.

| Property | On | Meaning |
| --- | --- | --- |
| `IsAbstract`, `IsVariation`, `IsIndividual` | definitions, usages | leading `abstract`/`variation`/`individual` |
| `IsVariant` | usages | a `variant` usage |
| `IsReference` | usages | declared with `ref` (`ref part x`, `ref x : T`) |
| `Direction` | usages | the string `"In"`, `"Out"`, or `"InOut"` for `in`, `out`, `inout` |
| `IsOrdered`, `IsNonunique` | usages | the `ordered` / `nonunique` multiplicity modifiers |
| `ValueText` | usages | the value clause as written, operator included: `"= 5 [kg]"`, `":= 3"` |
| `ReferenceSubsettingName` | usages | the raw target of `::>` / `references` |
| `ConjugatedFeatureTyping` | usages | the raw type name of a conjugated typing (`port p : ~PortT` gives `"PortT"`) |
| `ActionText` | `entry`/`do`/`exit` actions, `accept`/`send` nodes | the declaration or payload, body excluded |
| `IsParallel` | state definitions, state usages | declared `parallel` |
| `Trigger`, `Effect` | transitions | the text after `accept` and after `do`, as written |
| `IsElse` | action successions | declared with `else` (the default branch of a `decide` node) |

Notes:

- Text values keep the original spacing; they are never parsed or evaluated.
- `terminate` nodes are exported as features with the `terminate` keyword, and an action usage now
  exports its typing and multiplicity (`action build : BuildStep [2]`) like any other usage.
- `ReferenceSubsettingName` and `ConjugatedFeatureTyping` are raw and unresolved: they produce no
  edge and no diagnostic, and a conjugated usage still has a `null` typing reference.
- Only usages that become model elements carry a `Direction`. A bare parameter such as
  `in x : T;` is not a model element, so it is not exported.
- `derived`, `constant`, and `end` modifiers are not captured.

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
