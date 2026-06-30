# SysML2 Tools

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

SysML2Tools is a free, open-source .NET CLI tool and library that parses SysML v2 textual
model files and renders them as professional nested block diagrams suitable for architecture
documentation, CI/CD pipelines, and AI-assisted modeling workflows.

## Features

- **SysML v2 Parsing**: Parses SysML v2 textual notation (`.sysml` files) via an
  ANTLR4-generated parser against the official OMG grammar
- **OMG Standard Library**: Automatically loads and pre-resolves the OMG stdlib from
  embedded resources — no external installation required
- **Multi-File Workspace**: Accepts glob patterns to load multiple `.sysml` files as a
  single workspace; stdlib is always implicitly included
- **Semantic Model**: Full symbol table, reference resolution, import chain walking, and
  supertype chain resolution
- **Structured Diagnostics**: File, line, and column information on all errors and warnings
- **`lint` Command**: Load a workspace and report all diagnostics; exit non-zero if errors
  are present — suitable for CI/CD and AI-assisted model-fix loops
- **`render` Command**: Load a workspace, resolve a view, and render to SVG or PNG
- **GeneralView Layout**: Package-grouped definition block diagrams placed by a layered (ELK-style)
  engine with orthogonal specialization and membership edges, depth-coded fill colors, compartments,
  and configurable depth limiting
- **SVG Output**: Zero external dependencies
- **PNG Output**: Pixel-identical across Windows, Linux, and macOS via SkiaSharp and an
  embedded Noto Sans font
- **NuGet Library**: Publishable packages with a stable public API — use the parser,
  semantic model, or layout engine independently of the CLI tool
- **Self-Validation**: Built-in validation tests with TRX/JUnit output for regulated
  environments
- **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- **Multi-Runtime Support**: Targets .NET 8, 9, and 10

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.SysML2Tools
```

## Usage

### Linting

Check a SysML v2 workspace for errors and warnings:

```bash
# Lint a single file
sysml2tools lint model.sysml

# Lint all .sysml files in a directory
sysml2tools lint "src/**/*.sysml"
```

Exit code is non-zero if any errors are present, making it suitable for CI/CD pipelines.

### Rendering

Render a SysML v2 workspace to SVG or PNG:

```bash
# Render to SVG (auto-selects the single view in the workspace)
sysml2tools render model.sysml --output diagram.svg

# Render to PNG
sysml2tools render model.sysml --output diagram.png

# Render a named view from a multi-view workspace
sysml2tools render "src/**/*.sysml" --view SystemContext --output context.svg

# Auto-render the top-level part def when no view is defined
sysml2tools render model.sysml --auto --output diagram.svg

# Limit nesting depth (truncated parts show "+N more…")
sysml2tools render model.sysml --output diagram.svg --depth 3
```

### Global Flags

```bash
# Display version
sysml2tools --version

# Display help
sysml2tools --help

# Verb-specific help
sysml2tools --help lint
sysml2tools --help render

# Run self-validation
sysml2tools --validate

# Save validation results
sysml2tools --validate --results results.trx
sysml2tools --validate --results results.xml

# Silent mode with logging
sysml2tools --silent --log output.log
```

## Command-Line Reference

```text
sysml2tools [-v|--version] [-?|-h|--help [<verb>]] [--silent]
            [--validate] [--result|--results <file>] [--depth <#>] [--log <file>]
            [<verb> [verb-options] [<globs>]]
```

### Global Options

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help [<verb>]` | Display help; optionally for a specific verb |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation tests |
| `--results <file>`, `--result <file>` | Write validation results to `.trx` (TRX) or `.xml` (JUnit) |
| `--depth <#>` | Set heading depth for validation output (default: 1) |
| `--log <file>` | Write all output to a log file |

### `lint` Options

| Option | Description |
| --- | --- |
| `<globs>` | One or more glob patterns for `.sysml` input files |

### `render` Options

| Option | Description |
| --- | --- |
| `<globs>` | One or more glob patterns for `.sysml` input files |
| `--output <file>` | Output file path; extension determines format (`.svg` or `.png`) |
| `--view <name>` | Name of the view to render (required when workspace has multiple views) |
| `--auto` | Auto-render the BDD of the top-level `part def` when no view is defined |
| `--depth <#>` | Limit rendered nesting depth; truncated parts show `+N more…` |

## View Selection

| Condition | Behavior |
| --- | --- |
| Exactly one view in workspace | Render it |
| Zero views, `--auto` specified | Auto-render BDD of top-level `part def` silently |
| Zero views, no `--auto` | Warn and auto-render |
| Multiple views, none specified | Error: lists available view names and exits non-zero |
| Multiple views, `--view <name>` | Render the named view |

## NuGet Packages

| Package | Description |
| --- | --- |
| `DemaConsulting.SysML2Tools` | Core library: parser, semantic model, layout, `IRenderer` interface |
| `DemaConsulting.SysML2Tools.Svg` | SVG renderer — zero external dependencies |
| `DemaConsulting.SysML2Tools.Png` | PNG renderer — SkiaSharp (MIT), embedded Noto Sans font |

Library consumers can take a dependency on `DemaConsulting.SysML2Tools` alone to access
parsing, semantic model, and layout without pulling in any native graphics binaries.

## Contributing

See [CONTRIBUTING.md](https://github.com/demaconsulting/SysML2Tools/blob/main/CONTRIBUTING.md) for
guidelines on reporting bugs, suggesting features, and submitting pull requests.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/SysML2Tools?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/SysML2Tools?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/SysML2Tools?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/SysML2Tools?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/SysML2Tools/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_SysML2Tools&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_SysML2Tools&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.SysML2Tools?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/SysML2Tools/network/members
[link-stars]: https://github.com/demaconsulting/SysML2Tools/stargazers
[link-contributors]: https://github.com/demaconsulting/SysML2Tools/graphs/contributors
[link-license]: https://github.com/demaconsulting/SysML2Tools/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/SysML2Tools/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_SysML2Tools
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_SysML2Tools
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.SysML2Tools
