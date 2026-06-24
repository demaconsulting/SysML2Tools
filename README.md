# SysML2 Tools

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

DEMA Consulting template project for DotNet Tools, demonstrating best practices for building command-line tools with .NET.

## Features

This template demonstrates:

- **Standardized Command-Line Interface**: Context class handling common arguments
  (`--version`, `--help`, `--silent`, `--validate`, `--results`, `--depth`, `--log`)
- **Self-Validation**: Built-in validation tests with TRX/JUnit output
- **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- **Multi-Runtime Support**: Targets .NET 8, 9, and 10
- **Comprehensive CI/CD**: GitHub Actions workflows with quality checks, builds, and
  integration tests
- **Linting Enforcement**: markdownlint, cspell, and yamllint enforced on every CI run
- **Continuous Compliance**: Compliance evidence generated automatically on every CI run, following
  the [Continuous Compliance][link-continuous-compliance] methodology
- **SonarCloud Integration**: Quality gate and security analysis on every build
- **Documentation Generation**: Automated build notes, user guide, code quality reports,
  requirements, justifications, and trace matrix
- **Requirements Traceability**: Requirements linked to passing tests with auto-generated trace matrix

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.SysML2Tools
```

## Usage

```bash
# Default behavior (no arguments) — displays banner and copyright
sysml2tools

# Display version
sysml2tools --version

# Display help
sysml2tools --help

# Run self-validation
sysml2tools --validate

# Save validation results (--result is an accepted alias for --results)
sysml2tools --validate --results results.trx
sysml2tools --validate --result results.xml

# Set heading depth for embedded validation output
sysml2tools --validate --depth 2

# Silent mode with logging
sysml2tools --silent --log output.log
```

## Command-Line Options

| Option                                | Description                                                  |
| ------------------------------------- | ------------------------------------------------------------ |
| `-v`, `--version`                     | Display version information                                  |
| `-?`, `-h`, `--help`                  | Display help message                                         |
| `--silent`                            | Suppress console output                                      |
| `--validate`                          | Run self-validation                                          |
| `--results <file>`, `--result <file>` | Write results to `.trx` (TRX) or `.xml` (JUnit XML) file.    |
| `--depth <#>`                         | Set heading depth for markdown output (default: 1)           |
| `--log <file>`                        | Write output to log file                                     |

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

This behavior enables CI/CD pipelines to detect and surface misconfiguration failures
automatically.

## Self Validation

Running self-validation produces a report containing the following information:

```text
# DEMA Consulting SysML2 Tools

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | <version>                                          |
| Machine Name        | <machine-name>                                     |
| OS Version          | <os-version>                                       |
| DotNet Runtime      | <dotnet-runtime-version>                           |
| Time Stamp          | <timestamp> UTC                                    |

✓ TemplateTool_VersionDisplay - Passed
✓ TemplateTool_HelpDisplay - Passed

Total Tests: 2
Passed: 2
Failed: 0
```

Each test in the report proves:

- **`TemplateTool_VersionDisplay`** - `--version` outputs a valid version string.
- **`TemplateTool_HelpDisplay`** - `--help` outputs usage and options information.

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding validation output into a larger markdown document:

```bash
# Embed validation at heading level 2
sysml2tools --validate --depth 2
```

See the [User Guide][link-guide] for more details on the self-validation tests.

On validation failure the tool will exit with a non-zero exit code.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Requirements Justifications**: Detailed requirement rationale
- **Trace Matrix**: Requirements to test traceability

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
[link-guide]: https://github.com/demaconsulting/SysML2Tools/blob/main/docs/user_guide/introduction.md
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
