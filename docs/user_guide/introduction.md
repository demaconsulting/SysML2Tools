# Introduction

SysML2Tools is a free, open-source .NET CLI tool and library that parses SysML v2 textual
model files and renders them as professional nested block diagrams. It is designed for .NET
teams in regulated industries who author SysML v2 models as part of a Model-Based Systems
Engineering (MBSE) practice and need to generate diagram images programmatically — without
a paid GUI tool or a non-.NET runtime dependency.

A secondary audience is AI agents iterating on SysML v2 models: the `lint` command provides
structured diagnostic output (file, line, column, severity) that enables a model-fix loop
without requiring a rendered diagram.

## Purpose

This guide covers the installation, configuration, and use of SysML2Tools. It describes
the `lint` and `render` commands, the global CLI flags, view selection behavior, output
formats, and depth limiting.

## Scope

This user guide covers:

- Installation via `dotnet tool install`
- Linting SysML v2 workspaces with the `lint` command
- Rendering diagrams with the `render` command
- Global CLI options
- View selection and depth limiting
- Self-validation for tool qualification evidence

# Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.SysML2Tools
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
sysml2tools render "common/**/*.sysml" "system/**/*.sysml" --output diagram.svg
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
The output format is determined by the file extension of `--output`.

```bash
# Render to SVG
sysml2tools render model.sysml --output diagram.svg

# Render to PNG
sysml2tools render model.sysml --output diagram.png

# Render a named view from a multi-view workspace
sysml2tools render "src/**/*.sysml" --view SystemContext --output context.svg

# Auto-render the top-level part def when no view is defined
sysml2tools render model.sysml --auto --output diagram.svg
```

## View Selection

| Condition | Behavior |
| --- | --- |
| Exactly one view in workspace | Render it |
| Zero views, `--auto` specified | Auto-render BDD of top-level `part def` silently |
| Zero views, no `--auto` | Warn: "define a view or use --auto", then auto-render |
| Multiple views, none specified | Error: lists available view names, exits non-zero |
| Multiple views, `--view <name>` | Render the named view |

## Depth Limiting

Use `--depth <n>` to limit the nesting depth rendered. Parts beyond the limit are replaced
with an ellipsis footer (`+N more…`). Silent omission is never used — truncation is always
visible in the output.

```bash
sysml2tools render model.sysml --output diagram.svg --depth 3
```

## Output Formats

| Extension | Format | Notes |
| --- | --- | --- |
| `.svg` | SVG | Zero external dependencies |
| `.png` | PNG | SkiaSharp (MIT); pixel-identical across platforms |

PNG output uses an embedded Noto Sans font to guarantee pixel-identical output across
Windows, Linux, and macOS.

# Global Options

The following global options are accepted before the verb:

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help [<verb>]` | Display help; optionally for a specific verb |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation tests |
| `--results <file>`, `--result <file>` | Write validation results to `.trx` or `.xml` |
| `--depth <#>` | Set heading depth for validation output (default: 1) |
| `--log <file>` | Write all output to a log file |

Verb-specific help is available via either form:

```bash
sysml2tools --help lint
sysml2tools lint --help
```

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

SysML2Tools is structured as four NuGet packages. Library consumers can take individual
packages without pulling in the full CLI tool or native graphics binaries:

| Package | Contents |
| --- | --- |
| `DemaConsulting.SysML2Tools` | Core library: parser, semantic model, layout, `IRenderer` interface |
| `DemaConsulting.SysML2Tools.Svg` | SVG renderer — zero external dependencies |
| `DemaConsulting.SysML2Tools.Png` | PNG renderer — requires SkiaSharp native assets at publish time |
| `DemaConsulting.SysML2Tools.Tool` | dotnet tool — references all three packages |

Consumers who need only the parsed semantic model or `LayoutTree` take a dependency on
`DemaConsulting.SysML2Tools` only. Consumers who need SVG or PNG output opt in to the
respective renderer package explicitly.

# Continuous Compliance

This project follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology.
Compliance evidence (requirements, trace matrix, quality reports) is generated automatically
on every CI run.

## References

N/A
