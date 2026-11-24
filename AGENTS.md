# Project Overview

- **project-name**: SysML2Tools
- **organization**: DEMA Consulting
- **project-tagline**: SysML v2 diagram rendering for .NET
- **description**: SysML2Tools is a free, open-source .NET CLI tool and library that parses
  SysML v2 textual model files and renders them as professional nested block diagrams
  suitable for architecture documentation, CI/CD pipelines, and AI-assisted modeling
  workflows.
- **languages**: C#
- **technologies**: .NET, ANTLR4, CanvasNet

# Project Structure

```text
├── docs/
│   ├── build_notes/
│   ├── code_quality/
│   ├── code_review_plan/
│   ├── code_review_report/
│   ├── design/
│   ├── requirements_doc/
│   ├── requirements_report/
│   ├── reqstream/
│   ├── sysml2/
│   ├── user_guide/
│   └── verification/
├── src/
│   ├── DemaConsulting.SysML2Tools/
│   ├── DemaConsulting.SysML2Tools.Svg/
│   ├── DemaConsulting.SysML2Tools.Png/
│   └── DemaConsulting.SysML2Tools.Tool/
└── test/
    ├── DemaConsulting.SysML2Tools.Tests/       (TODO: core library tests — Phase 1+)
    ├── DemaConsulting.SysML2Tools.Svg.Tests/   (TODO: SVG renderer tests — Phase 4+)
    ├── DemaConsulting.SysML2Tools.Png.Tests/   (TODO: PNG renderer tests — Phase 4+)
    └── DemaConsulting.SysML2Tools.Tool.Tests/
```

# Language and Spelling (ALL Agents)

Always use **US English** spelling in all output (code, comments, documentation,
commit messages, and reports).

# Reference Template

This repository follows a reference template for structure and file conventions.

- **template-url**: `https://github.com/demaconsulting/Agents/raw/refs/heads/template`
- **Repository map**: `{template-url}/repository-map.md`
- **Template files**: `{template-url}/{file-path}` for files described in the map

# Codebase Navigation (ALL Agents)

When working with source code, design, or requirements artifacts, query the SysML2
architecture model under `docs/sysml2/` first (see the `sysml2tools-query` skill) to
understand software structure, purpose, and relationships. Fall back to
`docs/design/introduction.md` for the human-facing narrative, folder layout, and
companion artifact locations, and use it as the primary map when the model doesn't
yet cover something.

# Key Configuration Files

- **`.config/dotnet-tools.json`** - Local tool manifest for Continuous Compliance tools
- **`.editorconfig`** - Code formatting rules
- **`.clang-format`** - C/C++ formatting (if applicable)
- **`.textlint.yaml`** - Spell-check configuration and technical term dictionary (TextLint)
- **`.lintmark.yaml`** - Markdown formatting rules (LintMark)
- **`.yamllint.yaml`** - YAML formatting configuration
- **`.reviewmark.yaml`** - File review definitions and tracking
- **`docs/sysml2/`** - SysML2 architecture model; authoritative source for software structure
- **`nuget.config`** - NuGet package sources (if .NET)
- **`package.json`** - Node.js dependencies for Mermaid rendering in document builds
- **`requirements.yaml`** - Root requirements file with includes
- **`pip-requirements.txt`** - Python dependencies for yamllint and yamlfix
- **`fix.ps1`** - Applies the auto-fixers (dotnet format, LintMark, YAML); a fixer run always exits 0.
  Requires a scope; see `pwsh ./fix.ps1 -Help`.
- **`build.ps1`** - Builds the solution and runs the tests. Requires a scope (`-All` is the full build);
  see `pwsh ./build.ps1 -Help`.
- **`lint.ps1`** - Runs the lint checks (`-All` is the CI merge gate). Requires a scope; see
  `pwsh ./lint.ps1 -Help`.
- **`scripts/ProjectScripts.psm1`** - PowerShell module with the scoping logic the three scripts share.
  Delivered by the Agents release zip and shared verbatim across repositories: never edit it here
  (see *Project Scripts*).

# Standards Application (ALL Agents Must Follow)

Before performing any work, agents must read and apply the relevant standards
from `.github/standards/`. Use this matrix to determine which to load:

- **Any code**: `coding-principles.md`
- **C# code**: `coding-principles.md`, `csharp-language.md`
- **C++ code**: `coding-principles.md`, `cpp-language.md`
- **Any tests**: `testing-principles.md`
- **C# tests**: `testing-principles.md`, `csharp-testing.md`
- **C++ tests**: `testing-principles.md`, `cpp-testing.md`
- **Requirements**: `requirements-principles.md`, `software-items.md`, `reqstream-usage.md`
- **Design docs**: `software-items.md`, `design-documentation.md`, `technical-documentation.md`
- **Verification docs**: `software-items.md`, `verification-documentation.md`, `technical-documentation.md`
- **Review configuration**: `software-items.md`, `reviewmark-usage.md`
- **Software structure**: `sysml2-modeling.md`
- **Any documentation**: `technical-documentation.md`
- **Diagrams in documentation**: `diagram-documentation.md` (and the `canvasnet` skill for tooling)
- **Reading PDF or Office documents**: the `docdown` skill
- **Project scripts** (changing `build.ps1`, `lint.ps1`, `fix.ps1`, or `scripts/`): `project-scripts.md`

Load only the standards relevant to your specific task scope.

# Agent Delegation Guidelines

The default agent should handle simple, straightforward tasks directly.
Delegate to specialized agents only for specific scenarios:

- **Pre-PR lint cleanup** (fix all lint issues before pull request) → Call the lint-fix agent
- **Scoped fixes with no new user-visible behavior** (PR review comments, doc
  corrections, known bug fixes with defined root cause) → Call the developer agent
- **Light quality checking** (basic validation) → Call the quality agent
- **Any change introducing new user-visible behavior** (features, enhancements,
  new commands or options) → Call the implementation agent
- **Formal bug resolution** (complex debugging, unknown root cause) → Call the implementation agent
- **Formal reviews** (compliance verification, detailed analysis) → Call the formal-review agent
- **Structural audit**: (repository layout vs. template) → Call the template-sync agent
- **Implementation planning only** (review a plan before committing to implementation) → Call the planning agent

# Agent Reporting (Specialized Agents Must Follow)

Specialized agents MUST generate a completion report:

1. Save to `.agent-logs/{agent-name}-{subject}-{unique-id}.md`
   where `{subject}` is a kebab-case task summary (max 5 words) and
   `{unique-id}` is a short unique suffix (e.g., 8-char hex or timestamp)
2. Start with `**Result**: (SUCCEEDED|FAILED|INCOMPLETE)` as the first metadata field
3. Include the agent-specific report sections defined in each agent's prompt
4. Return the summary to the caller

Result semantics for orchestrator decision-making:

- **SUCCEEDED**: Work completed and all quality gates applicable to that agent's scope met
- **FAILED**: Work could not be completed or quality gates not met
- **INCOMPLETE**: Work cannot proceed without information only the user can
  provide (implementation, planning, and template-sync agents)

# Formatting (After Making Changes)

After making changes, run the auto-fix pass. This applies all available fixers
silently and **always exits 0** - agents do not need to respond to its output.

```pwsh
pwsh ./fix.ps1 -Changed   # during development: only the fixers for the changed file types
pwsh ./fix.ps1 -All       # everything; run before the pull request
```

This automatically handles: `dotnet format`, Markdown formatting (LintMark), and YAML
formatting. Full lint compliance is a **pre-PR responsibility**, not an agent
responsibility - invoke the lint-fix agent once before submitting a pull request.

## CI Quality Tools

CI runs `lint.ps1 -All` which checks: lintmark, textlint, yamllint, dotnet format,
reqstream, versionmark, reviewmark, and sysml2tools.

# Efficient Validation (ALL Agents)

Full `build.ps1 -All` and `lint.ps1 -All` runs can take many minutes on large repositories. Spend
them once, not after every edit. The scripts require a scope: with no arguments they print usage
and exit 1.

- **While iterating**, use `-Changed` (or `lint.ps1`/`fix.ps1 -Check`, or `build.ps1 -Project`) so only the
  affected work runs; add `-DryRun` to see the selection without running anything
- **Before handing back**, run the full gates your agent requires once (`build.ps1 -All`,
  `lint.ps1 -All`); re-run them only if you change files after they pass
- **Documentation-only changes** need no build
- Run `pwsh ./<script> -Help` for the switches of a repository; do not guess
- Never report a targeted check as the full gate - state which full gates ran in your report

# Scoped Validation (ALL Agents Must Follow)

`build.ps1 -Changed` (or `-Project`, which takes full or short names such as `Core`, `Language`, `Stdlib`, `Tool`,
`Tests`, and `Tool.Tests`; `-Help` lists them) selects the test projects a change affects through the project
reference graph;
a build-wide file (solution, project, props/targets, `global.json`, `.editorconfig`, `nuget.config`, tool manifest,
`build.ps1`, `scripts/ProjectScripts.psm1`) runs the full build and tests, and documentation-only changes build nothing.
Both test projects read repository files at run time (`test/SysMLModels/`, `docs/gallery/`, `docs/sysml2/`), mapped
to them by `$RuntimeReads` in `build.ps1`; keep it in step if a test starts reading another file. Every build,
scoped or full, first regenerates `stdlib.json.gz` with StdlibGen.

# Scope Discipline (ALL Agents Must Follow)

- **No generated file access**: Files inside any `generated/` folder are build
  outputs - do not read, lint, or modify them
- **Minimum necessary changes**: Only modify files directly required by the task
- **No speculative refactoring**: Do not refactor code adjacent to the change
  unless the task explicitly requests it
- **No drive-by fixes**: If you discover pre-existing issues in files you are
  reading but not modifying, document them in the report but do not fix them
- **Declare scope upfront**: Before making changes, determine which files will be
  modified. Any file outside this scope requires explicit justification.

# Protected Configuration Files

These files contain carefully designed configuration with documented intent
in header comments. Agents MUST NOT modify them unless the task explicitly
requires it and the modification preserves the documented design intent:

- `.reviewmark.yaml`, `.textlint.yaml`, `.editorconfig`
- `.lintmark.yaml`, `.yamllint.yaml`
- `requirements.yaml`

# Project Scripts

`build.ps1`, `lint.ps1`, and `fix.ps1` belong to the project: extend them as it grows. Add project-specific
steps, update tool versions, and add optional parameters that focus a run (for example one library in a
multi-library repository, or headless tests without GUI automation). Two rules keep them trustworthy:

- Run with no arguments, each script prints an error and its usage and exits 1 (a scope must be chosen).
  `-All` must still do the script's full job (CI and the agents rely on that); slow or
  environment-dependent test tiers may be opt-in switches of `build.ps1`.
- Never remove or weaken a check to make lint or tests pass.

The shared scoping logic lives in `scripts/ProjectScripts.psm1`, delivered by the Agents release zip (extracted into the
repository root; source of truth: `src/scripts/ProjectScripts.psm1` in the Agents repository) and byte-identical to
that copy: never edit it in a repository. Repository-specific
behavior belongs in the scripts' `[PROJECT-SPECIFIC]` blocks. See `project-scripts.md`.

# Continuous Compliance Overview

This repository follows the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance)
approach. Tools: **ReqStream** (requirements traceability), **ReviewMark** (file review enforcement),
**BuildMark** (tool versions), **VersionMark** (version tracking).
