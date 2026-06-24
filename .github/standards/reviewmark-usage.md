---
name: ReviewMark Usage
description: Follow these standards when configuring file reviews with ReviewMark.
---

# ReviewMark Usage Standard

## Required Standards

Read these standards first before applying this standard:

- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)

## Purpose

ReviewMark manages file review status enforcement and formal review processes. It tracks which files need
review, organizes them into review-sets, and generates review plans and reports.

## Key Commands

- **Lint Configuration**: `dotnet reviewmark --lint`
- **Elaborate Review-Set**: `dotnet reviewmark --elaborate {review-set}`
- **Generate Plan**: `dotnet reviewmark --plan docs/code_review_plan/generated/plan.md --enforce`
  (exits non-zero if any files are uncovered)

## Repository Structure

- `.reviewmark.yaml` - Configuration for review-sets, file-patterns, and review evidence-source.

# Review Definition Structure

Configure reviews in `.reviewmark.yaml` at repository root:

```yaml
needs-review:
  - "**/*.cs"
  - "**/*.cpp"
  - "**/*.hpp"
  - "!**/bin/**"
  - "!**/obj/**"
  - "requirements.yaml"
  - "docs/reqstream/**/*.yaml"
  - "README.md"
  - "docs/user_guide/**/*.md"
  - "docs/design/**/*.md"
  - "docs/verification/**/*.md"

evidence-source:
  type: none

context:
  - docs/design/introduction.md

reviews:
  - id: Purpose
    title: Review that README and User Guide are Coherent and Complete
    paths:
      - "README.md"
      - "docs/user_guide/**/*.md"
  - id: Decomposition
    title: Review that {SystemName} Decomposition Addresses the Stated Purpose
    context:
      - "README.md"
      - "docs/user_guide/**/*.md"
    paths:
      - "requirements.yaml"
      - "docs/design/introduction.md"
```

For a complete annotated example with template directives, see `.reviewmark.yaml` in the
reference template (`{template-url}/.reviewmark.yaml` per `AGENTS.md`).

# Review-Set Design Principles

- **Hierarchical Scope**: Higher-level reviews exclude lower-level implementation details, relying instead on design
  documents to describe what components they use. System reviews exclude subsystem/unit details, subsystem reviews
  exclude unit source code, only unit reviews include actual implementation.
- **Single Focus**: Each review-set proves one specific compliance question (user promises, system architecture,
  design consistency, etc.)
- **Parent Context**: Unit and subsystem reviews include parent design and requirements as
  context so reviewers understand the intended role and scope; see the Context Files section.
- **Context Management**: Keep file counts manageable to prevent context overflow while maintaining complete coverage
  through the hierarchy

# Context Files

Context files are shown to reviewers for orientation but not fingerprinted. Add a top-level
`context:` key for global context (every reviewer) and a per-review-set `context:` between
`title:` and `paths:` for review-specific context. Always include `docs/design/introduction.md`
as global context.

| Review Type | Context to add |
| :---------- | :------------- |
| `Decomposition` | `README.md`, `docs/user_guide/**/*.md` |
| `{SystemName}-Architecture` | `README.md`, `docs/user_guide/**/*.md` |
| `{SystemName}-Design` | `docs/reqstream/{system-name}.yaml` |
| `{SystemName}-Verification` | `docs/reqstream/{system-name}.yaml` |
| `{SystemName}-AllRequirements` | Parent system design doc + `docs/reqstream/{system-name}.yaml` |
| `{SystemName}-{UnitName}` (direct unit) | Parent system design doc + parent system requirements |
| `{SystemName}-{SubsystemName}` (subsystem) | Parent system design doc + parent system requirements |
| `{SystemName}-{SubsystemName}-{UnitName}` (unit under subsystem) | System + subsystem design docs + requirements |

# Review-Set Organization

**Naming conventions**: Placeholders in documentation, requirements, design, and
verification file paths are kebab-case (e.g., `{system-name}`). Placeholders in
source and test file paths may use the casing conventional for the project's
source language or repository layout (e.g., `{SystemName}`). Review-set name
placeholders are always PascalCase (e.g., `{SystemName}`).

## `Purpose` Review (only one per repository)

- **Purpose**: Proves that the user-facing docs are coherent and complete — the north-star for the hierarchy
- **Title**: "Review that README and User Guide are Coherent and Complete"
- **ID**: `Purpose` (no system prefix — one per repository)
- **Scope**: README and user_guide only; no requirements or design files
- **File Path Patterns**:
  - README: `README.md`
  - User guide: `docs/user_guide/**/*.md`

## `Decomposition` Review (only one per repository)

- **Purpose**: Proves that the software items tree breakdown logically addresses
  the user-facing promise; the structural mirror of the decomposition decision
- **Title**: "Review that {SystemName} Decomposition Addresses the Stated Purpose"
- **ID**: `Decomposition` (no system prefix — one per repository)
- **Scope**: introduction.md (the decomposition narrative) and requirements.yaml
  (the structural tree); no system-level detail
- **File Path Patterns**:
  - Root requirements: `requirements.yaml`
  - Design introduction: `docs/design/introduction.md`
- **Context Files**: `README.md`, `docs/user_guide/**/*.md`

## `{SystemName}-Architecture` Review (one per system)

- **Purpose**: Proves that the system is designed and tested to satisfy its requirements
- **Title**: "Review that {SystemName} Architecture Satisfies Requirements"
- **Scope**: Excludes subsystem and unit files, relying on system-level design to describe
  what subsystems and units it uses
- **File Path Patterns**:
  - System requirements: `docs/reqstream/{system-name}.yaml`
  - Design introduction: `docs/design/introduction.md`
  - System design: `docs/design/{system-name}.md`
  - Verification introduction: `docs/verification/introduction.md`
  - System verification design: `docs/verification/{system-name}.md`
  - System integration tests: `test/{SystemName}.Tests/{SystemName}Tests.{ext}`
- **Context Files**: `README.md`, `docs/user_guide/**/*.md`

## `{SystemName}-Design` Review (one per system)

- **Purpose**: Proves the system design is consistent and complete
- **Title**: "Review that {SystemName} Design is Consistent and Complete"
- **Scope**: Only brings in top-level requirements and relies on brevity of design documentation
- **Context Files**: `docs/reqstream/{system-name}.yaml`
- **File Path Patterns**:
  - Platform requirements: `docs/reqstream/{system-name}/platform-requirements.yaml`
  - Design introduction: `docs/design/introduction.md`
  - System design: `docs/design/{system-name}.md`
  - System design files: `docs/design/{system-name}/**/*.md`
  - OTS overview: `docs/design/ots.md` _(only if OTS items exist)_
  - Shared Package overview: `docs/design/shared.md` _(only if Shared Package items exist)_

## `{SystemName}-Verification` Review (one per system)

- **Purpose**: Proves the system verification design is consistent and covers all requirements
- **Title**: "Review that {SystemName} Verification is Consistent and Complete"
- **Scope**: Only brings in top-level requirements and all verification docs for the system
- **Context Files**: `docs/reqstream/{system-name}.yaml`
- **File Path Patterns**:
  - Verification introduction: `docs/verification/introduction.md`
  - System verification: `docs/verification/{system-name}.md`
  - System verification files: `docs/verification/{system-name}/**/*.md`
  - OTS overview: `docs/verification/ots.md` _(only if OTS items exist)_
  - Shared Package overview: `docs/verification/shared.md` _(only if Shared Package items exist)_

## `{SystemName}-AllRequirements` Review (one per system)

- **Purpose**: Proves the requirements are consistent and complete
- **Title**: "Review that All {SystemName} Requirements are Complete"
- **Scope**: Only brings in requirements files to keep review manageable
- **File Path Patterns**:
  - Subsystem/unit requirements: `docs/reqstream/{system-name}/**/*.yaml`
- **Context Files**: `docs/design/{system-name}.md`, `docs/reqstream/{system-name}.yaml`

## `{SystemName}-{SubsystemName}[-{SubsystemName}...]` Review (one per subsystem at any depth)

- **Purpose**: Proves that the subsystem is designed and tested to satisfy its requirements
- **Title**: "Review that {SystemName} {SubsystemName} Satisfies Subsystem Requirements"
- **Scope**: Excludes units under the subsystem, relying on subsystem design to describe
  what units it uses
- **File Path Patterns**:
  - Requirements: `docs/reqstream/{system-name}[/{subsystem-name}...]/{subsystem-name}.yaml`
  - Design: `docs/design/{system-name}[/{subsystem-name}...]/{subsystem-name}.md`
  - Verification design: `docs/verification/{system-name}[/{subsystem-name}...]/{subsystem-name}.md`
  - Tests: `test/{SystemName}.Tests[/{SubsystemName}...]/{SubsystemName}Tests.{ext}`
- **Context Files**: `docs/design/{system-name}.md`, `docs/reqstream/{system-name}.yaml`

## `{SystemName}-{SubsystemName}[-{SubsystemName}...]-{UnitName}` Review (one per unit)

- **Purpose**: Proves the unit is designed, implemented, and tested to satisfy its requirements
- **Title**: "Review that {SystemName} {SubsystemName} {UnitName} Implementation is Correct"
- **Scope**: Complete unit review including all artifacts
- **File Path Patterns**:
  - Requirements: `docs/reqstream/{system-name}[/{subsystem-name}...]/{unit-name}.yaml`
  - Design: `docs/design/{system-name}[/{subsystem-name}...]/{unit-name}.md`
  - Verification design: `docs/verification/{system-name}[/{subsystem-name}...]/{unit-name}.md`
  - Source (C# example): `src/{SystemName}[/{SubsystemName}...]/{UnitName}.cs`
  - Tests (C# example): `test/{SystemName}.Tests[/{SubsystemName}...]/{UnitName}Tests.cs`
  - Source (snake_case C++ example): `src/{system_name}[/{subsystem_name}...]/{unit_name}.cpp`
  - Tests (snake_case C++ example): `test/{system_name}_tests[/{subsystem_name}...]/{unit_name}_tests.cpp`
- **Context Files**: Parent system design + requirements; add subsystem design +
  requirements for each subsystem level above the unit.

## `OTS-{OtsName}` Review (one per OTS item)

- **Purpose**: Proves that the OTS item provides the required functionality and is correctly integrated
- **Title**: "Review that {OtsName} Provides Required Functionality"
- **Scope**: No local source code; review covers integration design, requirements, and verification evidence
- **File Path Patterns**:
  - OTS requirements: `docs/reqstream/ots/{ots-name}.yaml`
  - OTS integration design: `docs/design/ots/{ots-name}.md`
  - OTS verification: `docs/verification/ots/{ots-name}.md`
  - Tests (if applicable): `test/OtsSoftwareTests/...` (C#) or `test/ots_software_tests/...`
    (Python/other) — fixed repo-level name, no system prefix

## `Shared-{PackageName}` Review (one per Shared Package)

- **Purpose**: Proves that the Shared Package provides the required advertised features and is correctly integrated
- **Title**: "Review that {PackageName} Provides Required Features"
- **Scope**: No local source code; review covers integration design, requirements, and verification evidence
- **File Path Patterns**:
  - Shared Package requirements: `docs/reqstream/shared/{package-name}.yaml`
  - Shared Package integration design: `docs/design/shared/{package-name}.md`
  - Shared Package verification: `docs/verification/shared/{package-name}.md`

**Note**: File path patterns use `{ext}` as a placeholder for language-specific
extensions (`.cs`, `.cpp`/`.hpp`, `.py`, etc.). Adapt to your repository's languages.

# Quality Checks

- [ ] `.reviewmark.yaml` exists at repository root with proper structure
- [ ] Review-set organization follows the standard hierarchy patterns
- [ ] Each review-set focuses on a single compliance question (single focus principle)
- [ ] File patterns use correct glob syntax and match intended files
- [ ] Review-set file counts remain manageable (context management principle)
- [ ] Context configured per the Context Files section
