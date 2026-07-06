# Introduction

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

## View Body Statements

A `view def`/`view` declaration's body may contain `render <target>;`, `expose <name>;`, and
`filter [<expr>];` statements. For the General View strategy (the diagram produced when no
more specialized view kind applies), `expose` now scopes the rendered diagram instead of always
rendering the entire workspace:

- `expose <name>;` (valid only inside a named `view` usage's body, not a `view def`
  definition's body) — scopes the diagram to the union of every exposed name's containment
  subtree: `<name>` plus every declaration whose qualified name is `<name>` or is contained
  within it (a containment-subtree match, not just the exact element). If `<name>` does not
  resolve to any declaration in the workspace (for example, a typo), the tool falls back to
  rendering the full workspace for that view — but now also reports a diagnostic identifying the
  unresolved name, so the mistake is visible instead of silently rendering everything with no
  signal.
- `render <target>;` — per the SysML v2 grammar, this names a rendering style/format (e.g.
  `asTreeDiagram`, `asElementTable`) rather than content. It is captured but currently has **no
  effect** on the rendered scope — see `ROADMAP.md` for the planned future capability to honor
  it as a rendering-style selector.
- `filter [<expr>];` — the bracketed filter expression is parsed and captured, but **not yet
  evaluated**: the resolved (`expose`) scope is rendered unfiltered, and a diagnostic reports
  that the filter expression was parsed but not yet evaluated. Full filter-expression
  evaluation is planned future work — see `ROADMAP.md`.
- A view with **no** `expose` statement (including the `--auto`-synthesized view) renders the
  full workspace, exactly as before this scoping behavior was introduced.

Only the General View layout strategy honors `expose` scoping in this release; the other view
kinds (Interconnection, State Transition, Action Flow, Sequence, Grid, Browser) continue to
render their full applicable scope regardless of a view's declared `expose` statements — see
`ROADMAP.md` for the planned follow-up extending scoping to those view kinds.

Named `view Name { ... }` usages (not just `view def` declarations) are also now recognized as
their own renderable declarations: a workspace containing both `view def` declarations and named
`view` usages surfaces both kinds as views the `render` command discovers and renders.

## Expose vs. Render: Worked Examples

The three view body statements look similar but do very different jobs. This is a common point
of confusion, so it is worth stating plainly:

> **`render <target>;` looks like it should select what's shown, but it does not — use
> `expose` for that.**

| Statement | What it actually does |
| --- | --- |
| `expose <name>;` | The **only** mechanism scoping which model content appears in the diagram (see above). |
| `render <renderingKind>;` | Selects a rendering *style/format* (e.g. `asTreeDiagram`). Metadata only, no scoping. |
| `filter [<expr>];` | Captured as raw text only; not yet evaluated (see ROADMAP.md's filter-evaluation entry). |

### Example A: exposing a definition to scope down to a subsystem

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
}
```

`EngineOnlyView` renders **only** the `Engine` definition's containment subtree (`Engine` and
its `cylinder` part) — `Vehicle`, `myVehicle`, and `wheel` are excluded entirely. Removing the
`expose Engine;` statement (leaving only `render asTreeDiagram;`, or an empty view body) renders
the **full workspace** instead: `render` never narrows the scope, only `expose` does.

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

Here `expose myVehicle;` names a **usage** (`myVehicle : Vehicle`), not a `def`. The tool
resolves `myVehicle`'s own `Typing` edge to find the definition it is typed by (`Vehicle`), and
scopes the diagram to the union of `myVehicle`'s and `Vehicle`'s containment subtrees. The
rendered diagram therefore includes `Vehicle` (with its `engine` and `wheel` parts) and, because
`engine` is typed by `Engine`, the `Engine` definition (with its `cylinder` part) as well.

Contrast this with `expose Vehicle;` (exposing the **definition** directly, as in Example A):
that scopes straight to `Vehicle`'s own containment subtree without needing to resolve any
`Typing` edge, since a definition's subtree is already the thing being scoped to. Exposing a
usage takes one extra hop — through the usage's type reference — to arrive at the same kind of
definition subtree that exposing a `def` reaches directly.

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

# Command-specific help (identical to `lint --help`/`render --help`)
sysml2tools help lint
sysml2tools help render

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
