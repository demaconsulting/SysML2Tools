# Contributing to SysML2 Tools

Thank you for your interest in contributing to SysML2 Tools! We welcome contributions from the community and
appreciate your help in making this project better.

## Code of Conduct

This project adheres to a [Code of Conduct][code-of-conduct]. By participating, you are expected to uphold this code.
Please report unacceptable behavior through GitHub.

## How to Contribute

### Reporting Bugs

If you find a bug, please create an issue on GitHub with the following information:

- **Description**: Clear description of the bug
- **Steps to Reproduce**: Detailed steps to reproduce the issue
- **Expected Behavior**: What you expected to happen
- **Actual Behavior**: What actually happened
- **Environment**: Operating system, .NET version, SysML2 Tools version
- **Logs**: Any relevant error messages or logs

### Suggesting Features

We welcome feature suggestions! Please create an issue on GitHub with:

- **Feature Description**: Clear description of the proposed feature
- **Use Case**: Why this feature would be useful
- **Proposed Solution**: Your ideas on how to implement it (optional)
- **Alternatives**: Any alternative solutions you've considered (optional)

### Submitting Pull Requests

We follow a standard GitHub workflow for contributions:

1. **Fork** the repository
2. **Clone** your fork locally
3. **Create a branch** for your changes (`git checkout -b feature/my-feature`)
4. **Make your changes** following our coding standards
5. **Test your changes** thoroughly
6. **Commit your changes** with clear commit messages
7. **Push** to your fork
8. **Create a Pull Request** to the main repository

## Development Setup

### Prerequisites

- [.NET SDK][dotnet-download] 11.0, plus the .NET 8.0, 9.0, and 10.0 runtimes (the 11.0 SDK builds
  every target; the library tests run on .NET 8, 9, 10, and 11, and the tool and its tests run on
  .NET 10). Installing the 8.0, 9.0, 10.0, and 11.0 SDKs satisfies this.
- Git
- A code editor (Visual Studio, VS Code, or Rider recommended)

### Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/demaconsulting/SysML2Tools.git
   cd SysML2Tools
   ```

2. Restore dependencies:

   ```bash
   dotnet tool restore
   dotnet restore
   ```

3. Build the project:

   ```bash
   dotnet build --configuration Release
   ```

4. Run unit tests:

   ```bash
   dotnet test --configuration Release
   ```

## Coding Standards

### General Guidelines

- Follow the [C# Coding Conventions][csharp-conventions]
- Use clear, descriptive names for variables, methods, and classes
- Write XML documentation comments for all public, internal, and private members
- Keep methods focused and single-purpose
- Write tests for new functionality

### Code Style

This project enforces code style through `.editorconfig`. Key requirements:

- **Indentation**: 4 spaces for C#, 2 spaces for YAML/JSON/XML
- **Line Endings**: LF (Unix-style)
- **Encoding**: UTF-8
- **Namespaces**: Use file-scoped namespace declarations
- **Braces**: Required for all control statements
- **Naming Conventions**:
  - Interfaces: `IInterfaceName`
  - Classes/Structs/Enums: `PascalCase`
  - Methods/Properties: `PascalCase`
  - Parameters/Local Variables: `camelCase`

### XML Documentation

All members require XML documentation with proper indentation:

```csharp
/// <summary>
///     Brief description of what this does.
/// </summary>
/// <param name="parameter">Description of the parameter.</param>
/// <returns>Description of the return value.</returns>
public int ExampleMethod(string parameter)
{
    // Implementation
}
```

Note the spaces after `///` for proper indentation in summary blocks.

## Testing Guidelines

### Test Framework

We use MSTest v4 for unit and integration tests.

### Test Naming Convention

Use the pattern: `ClassName_MethodUnderTest_Scenario_ExpectedBehavior`

Examples:

- `Program_Main_NoArguments_ReturnsSuccess`
- `Context_Create_WithInvalidFlag_ThrowsArgumentException`
- `Validation_Run_AllTests_ReturnsSuccess`

### Writing Tests

- Write tests that are clear and focused
- Use modern MSTest v4 assertions:
  - `Assert.HasCount(expectedCount, collection)`
  - `Assert.IsEmpty(collection)`
  - `Assert.DoesNotContain(item, collection)`
- Always clean up resources (use `try/finally` for console redirection)
- Link tests to requirements in `requirements.yaml` when applicable

### Running Tests

#### Unit Tests

```bash
# Run all unit tests
dotnet test --configuration Release

# Run specific unit test
dotnet test --filter "FullyQualifiedName~YourTestName"

# Run with coverage
dotnet test --collect "XPlat Code Coverage"
```

#### Self-Validation Tests

```bash
# Run self-validation tests
dotnet run --project src/DemaConsulting.SysML2Tools \
  --configuration Release --framework net10.0 --no-build -- --validate
```

## Documentation

### Markdown Guidelines

All markdown files must follow these rules (enforced by LintMark):

- Maximum line length: 120 characters
- Use ATX-style headers (`# Header`)
- Lists must be surrounded by blank lines
- Use reference-style links: `[text][ref]` with `[ref]: url` at document end
- **Exceptions**:
  - `README.md` uses absolute URLs (it's included in the NuGet package)
  - AI agent markdown files (`.github/agents/*.agent.md`) use inline links
    `[text](url)` so URLs are visible in agent context

### Spell Checking

All files are spell-checked using TextLint. **Never** add a word to the `.textlint.yaml` word list in order to silence a
spell-checking failure. Doing so defeats the purpose of spell-checking and reduces the quality of the repository.

- If TextLint flags a word that is **misspelled**, fix the spelling in the source file.
- If TextLint flags a word that is a **genuine technical term** (tool name, project identifier, etc.) and is spelled
  correctly, raise a **proposal** (e.g. comment in a pull request) explaining why the word should be added. The
  proposal must be reviewed and approved before the word is added to the list.

### Help Cards

The built-in help (`sysml2tools help`) is served from Markdown help cards in
`src/DemaConsulting.SysML2Tools.Tool/Help/`, which the DemaConsulting.HelpCards package embeds in the tool
assembly. `Cli/HelpCommand.cs` is the only code that uses the package.

- **One file per topic.** The path below `Help/`, without `.md`, is the card id. `_source.md` names the source
  (`name:`) and sets `schema: 2`. The cards of the commands are under `commands/`, and those of the query verbs
  under `commands/query/`.
- **Front matter.** Every card has a `title`, a `summary` (at most 80 characters for a topic, 100 otherwise), and
  exactly one purpose tag: `tutorial`, `reference`, or `troubleshooting`. Add `aliases` for topic names that users
  commonly guess, such as `examples`, so `help <name>` finds the card.
- **New topics.** Add the id of every new top-level card to `HelpCommand.RootCardIds`, the one list of topics; a
  missing or extra topic fails the tests.
- **New commands and verbs.** `help <command>` and `help query <verb>` are routed to the cards under `commands/`, and
  the tests require a card for every command of `SysmlCommand` and every query verb. A command card's title is
  `<command> command`, and a verb card's title starts with `query <verb>`.
- **Examples.** Give every task a runnable `command` example, one command per example: no pipes, redirection, or
  `&&`. Wrap `content` in single quotes and use double quotes inside the command, which bash, PowerShell, and cmd
  read alike. State the result when it matters with `exit:` (default 0) and `expect:`, a short fragment that must
  appear somewhere in the console output; `expect` cannot check a file that the command writes. Keep each
  `description` to about 50 characters.
- **Getting started.** Say how to install the tool for CI: the .NET version it needs (from `TargetFramework`), a
  local tool manifest, and the global install as the alternative. The CI example must be a complete GitHub Actions
  steps snippet (`language: yaml`) with `actions/setup-dotnet` for that .NET version, `dotnet tool restore`, and a
  `dotnet sysml2tools` step that lints the model; prose alone is not enough. Mark the install and CI examples as
  `snippet` examples so that the example runner does not run them.
- **Input files.** Show complete files only for formats that users author, such as the sample model of the
  `sysml-models` card, and describe formats that the tool produces (JSON exports, query reports) in prose. The
  examples of every card read that sample as `vehicle.sysml`: a copy of it is kept in the test project under
  `TestData/HelpExamples/`, which the example-runner test copies into `invocation.WorkingDirectory` before running
  the tool, and that test fails when the copy differs from the card. A card's own `source` example (such as
  `broken.sysml` in `troubleshooting`) is used instead of a test-data file of the same name.
- **Width.** A printed card may not be wider than 120 characters. Links print as full commands (`[[#name]]` as
  about 50 to 60 characters) and `{{version}}` of a development build as about 46, so keep body lines near 75
  characters with at most one link each, and put a link on a short line.
- **Text.** Cards are linted and spell-checked like any Markdown file: no body line starting with `#`, no wide
  tables, and every fenced block names its language.

Run `pwsh ./build.ps1 -Changed` after changing a card. The `HelpCommand` unit tests validate every card, with
warnings treated as errors, and check its printed width; an integration test runs every command example against
the built tool and compares the result with the card.

## Quality Checks

Before submitting a pull request, ensure all quality checks pass:

### 1. Build and Test

```bash
# Build the project
dotnet build --configuration Release

# Run unit tests
dotnet test --configuration Release
```

All tests must pass with zero warnings.

### 2. Linting

```pwsh
# While iterating: only the fixers and checks for the files you changed
pwsh ./fix.ps1 -Changed
pwsh ./lint.ps1 -Changed

# Before submitting a pull request: apply all fixers, then all linters must pass
pwsh ./fix.ps1 -All
pwsh ./lint.ps1 -All
```

Run `pwsh ./lint.ps1 -Help` (or `fix.ps1`, `build.ps1`) for all switches; each script requires a scope.

### 3. Code Coverage

Maintain or improve code coverage. Use the `--collect "XPlat Code Coverage"` option when running tests.

## Commit Messages

Write clear, concise commit messages:

- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit first line to 72 characters
- Reference issues and pull requests when applicable

Examples:

- `Add support for custom report headers`
- `Fix crash when results file path is invalid`
- `Update documentation for --walk-depth option`
- `Refactor argument parsing for better testability`

## Pull Request Process

1. **Update Documentation**: Update relevant documentation for your changes
2. **Add Tests**: Include tests that cover your changes
3. **Run Quality Checks**: Ensure all linters, tests, and builds pass
4. **Submit PR**: Create a pull request with a clear description
5. **Code Review**: Address feedback from maintainers
6. **Merge**: Once approved, a maintainer will merge your PR

### Pull Request Template

When creating a pull request, include:

- **Description**: What changes does this PR introduce?
- **Motivation**: Why are these changes needed?
- **Related Issues**: Link to any related issues
- **Testing**: How have you tested these changes?
- **Checklist**:
  - [ ] Tests added/updated
  - [ ] Documentation updated
  - [ ] All tests pass
  - [ ] Code follows style guidelines
  - [ ] No new warnings introduced

## Requirements Management

SysML2 Tools uses [DemaConsulting.ReqStream][reqstream] for requirements traceability:

- All requirements are defined in `requirements.yaml`
- Each requirement should be linked to test cases
- Run `dotnet reqstream` to generate requirements documentation
- Use the `--enforce` flag to ensure all requirements have test coverage

## Release Process

Releases are managed by project maintainers. The process includes:

1. Version bump in project files
2. Tag the release in Git
3. Build and test across all supported platforms
4. Publish NuGet package
5. Create GitHub release with artifacts and release notes

## Getting Help

- **Questions**: Use [GitHub Discussions][discussions]
- **Bugs**: Report via [GitHub Issues][issues]
- **Security**: See [SECURITY.md][security] for vulnerability reporting

## License

By contributing to SysML2 Tools, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing to SysML2 Tools!

[code-of-conduct]: https://github.com/demaconsulting/SysML2Tools/blob/main/CODE_OF_CONDUCT.md
[dotnet-download]: https://dotnet.microsoft.com/download
[csharp-conventions]: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
[reqstream]: https://github.com/demaconsulting/ReqStream
[discussions]: https://github.com/demaconsulting/SysML2Tools/discussions
[issues]: https://github.com/demaconsulting/SysML2Tools/issues
[security]: https://github.com/demaconsulting/SysML2Tools/blob/main/SECURITY.md
