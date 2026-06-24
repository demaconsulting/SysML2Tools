# SysML2Tools Architecture

## Purpose

SysML2Tools is a free, open-source .NET CLI tool and library that parses SysML v2
textual model files and renders them as professional nested block diagrams suitable
for architecture documentation, CI/CD pipelines, and AI-assisted modeling workflows.

Target stakeholders are .NET teams in regulated industries who author SysML v2 models
as part of a Model-Based Systems Engineering (MBSE) practice and need to generate
diagram images programmatically — without a paid GUI tool or a non-.NET runtime
dependency.

A secondary stakeholder is AI agents iterating on SysML v2 models: the `lint` command
provides structured diagnostic output that enables a model-fix loop without requiring
a rendered diagram.

## Scope

**Included:**

- Parsing SysML v2 textual syntax (`.sysml` files) via an ANTLR4-generated parser
- Loading and pre-resolving the OMG standard library (stdlib) from embedded resources
- Multi-file workspace model (glob patterns; stdlib is always implicitly included)
- Semantic model: symbol table, reference resolution, supertype chain walking
- Structured diagnostic model with file, line, and column information
- `lint` command: load workspace and report all diagnostics (no rendering)
- `render` command: load workspace, resolve a view, render to SVG or PNG
- `--validate` flag: self-test using embedded test models, results via `DemaConsulting.TestResults`
- Standard DEMA CLI argument contract (`-v`, `-?/-h`, `--silent`, `--validate`,
  `--result/--results`, `--depth`, `--log`)
- `GeneralView` layout (nested rectangular blocks with compartments) in v1
- SVG output (zero external dependencies)
- PNG output (SkiaSharp, MIT license; pixel-identical across platforms with embedded font)
- Publishable NuGet library with a stable public API
- `dotnet tool` packaging

**Excluded from v1:**

- GUI, interactive editor, or language server
- SysML v1 syntax or semantics
- Non-SysML input formats
- SARIF output (diagnostic infrastructure supports it; surface in v2)
- Loadable theme files (Theme record ships as compile-time constants in v1)
- Non-`GeneralView` diagram rendering (other view kinds produce an "unsupported" diagnostic)
- Full OMG graphical notation conformance (targeting practical correctness, not spec
  exhaustiveness)

## Package Structure

```text
SysML2Tools
├── DemaConsulting.SysML2Tools - core library: parse, semantic model, layout, IRenderer
├── DemaConsulting.SysML2Tools.Svg - SvgRenderer : IRenderer (zero external deps)
├── DemaConsulting.SysML2Tools.Png - PngRenderer : IRenderer (SkiaSharp, MIT)
└── DemaConsulting.SysML2Tools.Tool - dotnet tool (thin CLI wrapper)
```

## Inter-Package Interfaces

### Core → Svg / Png

The core library defines the `IRenderer` interface and the `LayoutTree` primitive
vocabulary. Renderer packages implement `IRenderer` and consume `LayoutTree` only —
they have no dependency on parsing or semantic model internals.

```csharp
// DemaConsulting.SysML2Tools (public API surface)

// Low-level: one LayoutTree → one output stream. Pure, stateless, no filesystem access.
// Callers pass a FileStream, MemoryStream, or Console.OpenStandardOutput() as needed.
public interface IRenderer
{
    string MediaType { get; }           // e.g. "image/svg+xml"
    string DefaultExtension { get; }    // e.g. ".svg"
    void Render(LayoutTree layout, RenderOptions options, Stream output);
}

// High-level orchestration: walks views, calls ILayoutStrategy + IRenderer per view.
// Lives in the core library — not in renderer packages.
public sealed class DiagramRenderer
{
    public IReadOnlyList<RenderOutput> RenderWorkspace(
        SysmlWorkspace workspace,
        IRenderer renderer,
        RenderOptions options);
}

public sealed record RenderOutput(
    string SuggestedFileName,  // e.g. "structure.svg" (derived from view name)
    string MediaType,           // e.g. "image/svg+xml"
    Stream Data);               // caller decides where to write

public sealed record RenderOptions(/* scale, dpi, theme, depth limit, etc. */);

public sealed record LayoutTree(/* positioned primitives: boxes, lines, arrows, text */);
public sealed record SysmlWorkspace(/* resolved semantic model */);
public sealed record SysmlLoadResult(SysmlWorkspace? Workspace, IReadOnlyList<SysmlDiagnostic> Diagnostics);
public sealed record SysmlDiagnostic(string Location, DiagnosticSeverity Severity, string Message);
//                                    ^ "file.sysml:12:5"
public enum DiagnosticSeverity { Error, Warning, Information }
```

### Core → Tool

The tool references all three packages. It owns only argument parsing, console
output formatting, file I/O, and exit codes. All substantive logic is delegated to
the core library and renderer packages.

### Renderer package isolation

Library consumers who need only the parsed semantic model or `LayoutTree` take a
dependency on `DemaConsulting.SysML2Tools` only — no SkiaSharp native binaries,
no rendering overhead. Consumers who need SVG or PNG opt into the respective
renderer package explicitly.

## Technology Stack

| Concern | Choice | License |
| --- | --- | --- |
| Language / runtime | C# / .NET 8+ | MIT |
| Parser generator | ANTLR4 (`Antlr4.Runtime.Standard`) | BSD-3-Clause |
| SysML v2 grammar | `antlr/grammars-v4` (official OMG KEBNF) | MIT |
| PNG rendering | SkiaSharp | MIT |
| Embedded font | Noto Sans | SIL OFL 1.1 |
| Test results output | `DemaConsulting.TestResults` | — |
| Unit testing | xUnit v3 | Apache 2.0 |

No ImageSharp dependency. SkiaSharp is chosen over ImageSharp to avoid the Six Labors
Split License, which would impose licensing obligations on library consumers embedding
`DemaConsulting.SysML2Tools.Png` in commercial products.

## Software Structure

### Parse Pipeline

```text
.sysml files (user) + embedded stdlib resources
        ↓
Lexer + Parser (ANTLR4-generated, per file, independent)
        ↓  CST
AST Walker (produces typed AST nodes)
        ↓  AST per file
Symbol Table Population (all files merged)
        ↓
Reference Resolver (resolves names to declarations, walks import chains)
        ↓
Supertype Walker (resolves specializes / typing chains)
        ↓
SysmlWorkspace (fully resolved semantic model)
```

Errors at any stage produce `SysmlDiagnostic` entries. If errors exist, `Workspace`
is null in `SysmlLoadResult`.

### Render Pipeline

```text
SysmlWorkspace + view name (or auto-selection)
        ↓
DiagramTypeRouter
  - resolves view's viewpoint type to stdlib qualified name
  - walks supertype chain to find nearest stdlib ancestor
  - dispatches to ILayoutStrategy
        ↓
ILayoutStrategy (one implementation per view kind)
  - v1: GeneralViewLayoutStrategy
  - others: UnsupportedViewLayoutStrategy (returns diagnostic)
        ↓
LayoutTree (positioned primitives)
        ↓
IRenderer (SvgRenderer or PngRenderer)
        ↓
Output stream (SVG or PNG)
```

### CLI Arguments

All DEMA CLI tools share a standard global argument contract:

```text
sysml2tools [-v|--version] [-?|-h|--help [<verb>]] [--silent]
            [--validate] [--result|--results <file>] [--depth <#>] [--log <file>]
            [<verb> [verb-options] [<globs>]]
```

`--validate` runs self-test against embedded test models and writes pass/fail
results to `--results <file>` as TRX/JUnit via `DemaConsulting.TestResults`. This
flag is part of a shared CI contract executed uniformly across all DEMA tools —
the same `--validate --results <file> --depth <#>` invocation works for every tool.

Verb-specific help is available via either `sysml2tools --help <verb>` or
`sysml2tools <verb> --help`.

### CLI Verbs

| Verb | Behavior |
| --- | --- |
| `lint` | Load workspace, report all diagnostics, exit non-zero if errors |
| `render` | Load workspace, resolve view, render to SVG or PNG |
| `export` | *(future)* Export model data to structured formats |

### View Selection Logic

| Condition | Behavior |
| --- | --- |
| Exactly one view in workspace | Render it |
| Zero views, `--auto` specified | Auto-render BDD of top-level `part def`, no warning |
| Zero views, no `--auto` | Warn with "define a view or use --auto", then auto-render |
| Multiple views, none specified | Error: list available view names and exit |
| Multiple views, `--view name` | Render the named view |

### Depth Limiting

`--depth <n>` limits the nesting depth rendered. Parts beyond the limit are replaced
with an ellipsis footer: `+N more…`. Silent omission is not permitted — truncation
must always be visible.

### Theme

A `Theme` record holds all visual parameters: depth-coded fill colors, stroke
widths, font sizes, label padding, and font descriptor. Three compile-time constants
are provided: `Themes.Light`, `Themes.Dark`, `Themes.Print`.

The `FontDescriptor` within a theme specifies a font family name and optionally an
embedded resource path. Default themes reference the embedded Noto Sans font,
ensuring pixel-identical PNG output across all platforms. User-specified themes
(v2) may override with system fonts or external `.ttf` files; pixel-identity is
then the user's responsibility.

## Test Strategy

Tests are layered by complexity to create a gradient the implementation agent can
climb incrementally. Each level only becomes reachable once the previous levels pass.

### Leveled Semantic Model Tests (unit, agent-written)

| Level | Content |
| --- | --- |
| 1 | Empty package parses cleanly |
| 2 | Single `part def` with `doc` comment |
| 3 | Inline nested parts |
| 4 | Typed parts (`part p : SomeType`) |
| 5 | Cross-file references |
| 6 | Stdlib type references (`GeneralView`, `SequenceView`) |
| 7 | Supertype chains (`specializes`) |
| 8 | View definitions with `expose` |
| 9 | Circular import detection (expected diagnostic) |
| 10 | `software-structure.sysml` (real-world; zero diagnostics expected) |

### OMG Reference Models (CC BY 4.0)

The OMG [`SysML-v2-Release`](https://github.com/Systems-Modeling/SysML-v2-Release)
repository provides normative example models (vehicle systems, mass rollup,
behaviors, state machines, requirements patterns). These are included in the test
suite with attribution and serve as ground-truth inputs the implementation agent
cannot game.

### spec42 Cross-Validation

For every `.sysml` test file, `spec42` is run in parallel. If spec42 parses a file
cleanly, SysML2Tools must parse it cleanly too. View identification results are
compared between the two tools.

### Render Regression Tests

Reference SVG and PNG outputs for the standard test models are committed. CI fails
if rendered output changes unexpectedly. PNG comparisons use pixel-exact diff (valid
because of the embedded font guarantee).

## Implementation Phases

### Phase 0 — Repository Scaffold (1 session)

Stand up repo, solution, project structure, dotnet tool packaging, CI pipeline,
ReviewMark / ReqStream configuration. No logic.

**Gate:** `dotnet tool install` works; tool prints version and exits cleanly.

### Phase 1 — Parser + Stdlib (2–3 sessions)

Wire ANTLR4 MSBuild code generation against the official grammar. Embed OMG stdlib
files as assembly resources. Implement parse pipeline to CST only. Implement `lint`
command stub (syntax errors only).

**Gate:** All OMG example models parse without syntax errors. Stdlib loads silently.

### Phase 2 — Semantic Model (3–5 sessions)

Build symbol table, reference resolver, import chain, and supertype walker using the
leveled test progression as the iteration driver.

**Gate:** All OMG example `.sysml` files resolve cleanly. Circular import produces
the correct diagnostic. `software-structure.sysml` resolves with zero diagnostics.

### Phase 3 — LayoutTree Design (1 session — human review required)

Design and freeze the `LayoutTree` primitive vocabulary. Review all 8 SysML v2 view
kinds and confirm the primitives cover them. This is a design session producing an
interface, not an implementation.

**Gate:** Human sign-off on `LayoutTree` primitives before proceeding.

### Phase 4 — GeneralView Layout + Renderers (3–4 sessions)

Implement `GeneralViewLayoutStrategy`, `SvgRenderer`, `PngRenderer`. Wire
`DiagramTypeRouter` with unsupported-view-kind diagnostics for the other seven view
kinds. Implement `render` command fully.

**Gate:** `software-structure.sysml` renders to a visually correct SVG and
pixel-identical PNG across Windows, Linux, and macOS.

### Phase 5 — Polish + Self-test (1–2 sessions)

Implement `--validate` self-test. Add `--depth` with ellipsis indicator. Add view
enumeration on multiple-view error. Finalize theme record. Wire embedded Noto Sans
font. Attribution notices in NuGet metadata.

**Gate:** `--validate` passes. Lint CI clean. All OMG example models render without
crash (unsupported view kinds produce clean diagnostics, not exceptions).

## Architectural Decisions

**Multi-package from day one.** Separating the core library from renderer packages
allows library consumers to take a dependency on parsing and layout without pulling
in native graphics binaries. The `IRenderer` interface is the extension point.

**Multi-file workspace.** The OMG stdlib is pre-loaded from embedded resources before
any user files are parsed. Single-file input is the degenerate case of a multi-file
workspace; there is no single-file mode.

**SkiaSharp over ImageSharp.** SkiaSharp is MIT-licensed. ImageSharp v2+ uses the
Six Labors Split License which imposes obligations on commercial library consumers.
SkiaSharp's native asset requirement is transparent for tool consumers (handled by
NuGet at publish time) and is a known, documented constraint for library consumers.

**Embedded Noto Sans font.** Ensures pixel-identical PNG output across all platforms.
Noto Sans is licensed SIL OFL 1.1 which explicitly permits embedding in software.
Default themes use the embedded font; user-overridden themes may use system or
external fonts at the cost of the pixel-identity guarantee.

**`IRenderer` is low-level and pure — no filesystem access.** It receives a
`LayoutTree` and writes to a caller-supplied `Stream`. Passing a `FileStream`
writes directly to disk with no intermediate buffer; passing a `MemoryStream`
keeps output in memory for testing or in-process use; passing
`Console.OpenStandardOutput()` supports stdout piping. Multi-view orchestration
(`DiagramRenderer`) lives in the core library and calls `IRenderer` once per view —
renderer packages have no concept of workspaces or view iteration.

**DiagramTypeRouter uses resolved qualified names.** The router dispatches on the
stdlib-resolved qualified name of a view's viewpoint type (e.g.,
`SystemsModelingLibrary::Views::GeneralView`), not the raw token. User aliases and
local imports therefore do not break dispatch. The router walks the supertype chain
to handle custom viewpoints that specialize stdlib viewpoints.

**Diagnostic model mirrors ReviewMark.** `SysmlDiagnostic` mirrors ReviewMark's
`LintIssue` in structure and philosophy: file/line/col location, severity enum,
human-readable message. The `lint` command makes this output useful for AI-assisted
model authoring loops.

**Theme record is a compile-time constant in v1.** Loadable theme files are deferred
to v2. The Theme record data structure is defined in v1 so that v2 loadable themes
are additive and non-breaking.

**`--auto` flag.** When a workspace has no view definitions, `--auto` renders the BDD
of the top-level `part def` silently. Without `--auto`, the same auto-render occurs
but a warning is emitted advising the user to define an explicit view. This keeps v1
immediately useful for unstructured models while encouraging good authoring practice.

**Self-test via `--validate` flag.** Uses the same `Program.Run` pattern as
ReviewMark: invokes the full CLI pipeline against embedded test models with a test
context, then asserts expected outputs. Results are written as TRX/JUnit via
`DemaConsulting.TestResults`, consistent with the shared DEMA CI contract. This
tests the integrated tool, not just unit-level components.

**SARIF deferred.** The `SysmlDiagnostic` list is structurally compatible with SARIF.
SARIF output can be added as a formatting option on the existing infrastructure
without any breaking changes.

## Open Concerns

1. 🔴 **HIGH** LayoutTree vocabulary: the primitive set must cover all 8 SysML v2
   view kinds from day one. Designing it only for `GeneralView` risks structural
   breaking changes when sequence, state-machine, or interconnection views are added.
   **Requires human review in Phase 3 before implementation proceeds.**

2. 🟡 **MEDIUM** IRenderer public API stability: once `DemaConsulting.SysML2Tools`
   is published, changes to `IRenderer` or `LayoutTree` are semver-major breaking
   changes. The Phase 3 design review is the primary mitigation.

3. 🟡 **MEDIUM** SkiaSharp native assets for library consumers: consumers referencing
   `DemaConsulting.SysML2Tools.Png` must ensure the appropriate
   `SkiaSharp.NativeAssets.*` package is included in their publish output. This must
   be documented clearly in the package README.

4. 🟡 **MEDIUM** Noto Sans SIL OFL attribution: OFL requires the copyright notice and
   license text to be included in distributions. Must appear in NuGet package notices
   and the tool's `--licenses` output (or equivalent).

5. 🟢 **LOW** spec42 competitive risk: if Elan8 improves spec42's `GeneralView`
   renderer to produce professional nested block diagrams, the primary rendering
   differentiator is reduced. The .NET-native integration story and the AI-loop
   diagnostic design remain defensible regardless.

6. 🟢 **LOW** Theme file format for v2: the format for loadable theme files
   (YAML, JSON, or TOML) is undecided. This is internal to v2 and non-breaking
   relative to v1's public API.
