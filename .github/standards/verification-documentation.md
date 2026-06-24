---
name: Verification Documentation
description: Follow these standards when creating software verification design documentation.
globs: ["docs/verification/**/*.md"]
---

# Required Standards

- **`technical-documentation.md`** - General technical documentation standards
- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)

# Folder Structure

```text
docs/verification/
├── introduction.md              # heading depth #
├── {system-name}.md             # heading depth #
├── {system-name}/
│   ├── {subsystem-name}.md      # heading depth ##
│   ├── {subsystem-name}/
│   │   └── {unit-name}.md       # heading depth ###
│   └── {unit-name}.md           # heading depth ##
├── ots.md                       # heading depth # (if OTS items exist)
├── ots/
│   └── {ots-name}.md            # heading depth ##
├── shared.md                    # heading depth # (if Shared Packages exist)
└── shared/
    └── {package-name}.md        # heading depth ##
```

All sections in every file are mandatory; write "N/A - {justification}" rather than removing any.
Determine subsystem vs. unit classification from `docs/design/introduction.md` — folder depth does not determine classification.

# introduction.md (MANDATORY)

Must include:

- **Purpose**: audience and compliance drivers
- **Scope**: items covered and explicitly excluded (no test projects)
- **Companion Artifact Structure**: parallel paths for requirements, design, verification, source, tests
- **References** _(if applicable)_: external standards or specifications - only in `introduction.md`

# System Verification Design (MANDATORY)

Create `{system-name}.md` (`#` heading) and `{system-name}/` folder:

- **Verification Approach**: test types (unit, integration, end-to-end), framework, project structure
- **Test Environment**: OS, runtime, external services, files, or configuration required
- **Acceptance Criteria**: what constitutes a passing system test (IEC 62304 §5.7.2)
- **Test Scenarios**: named scenarios for each system requirement

# Subsystem Verification Design (MANDATORY)

Place `{subsystem-name}.md` in the **parent** folder; create `{subsystem-name}/` for children:

- **Verification Approach**: integration test approach and mocking at subsystem boundary
- **Test Environment**: any environment setup beyond the standard test runner
- **Acceptance Criteria**: what constitutes a passing subsystem test (IEC 62304 §5.5.2)
- **Test Scenarios**: named scenarios including boundary conditions, error paths, and normal operation

# Unit Verification Design (MANDATORY)

Place `{unit-name}.md` in the **parent** folder:

- **Verification Approach**: what is mocked/stubbed and why; injected vs. real dependencies
- **Test Environment**: any environment setup beyond the standard test runner
- **Acceptance Criteria**: what constitutes passing unit tests (IEC 62304 §5.5.2)
- **Test Scenarios**: named scenarios including boundary values, error paths, and normal operation

# OTS Verification Evidence (when OTS items exist)

Create `docs/verification/ots.md` (`#` heading) covering the overall OTS verification strategy.

For each OTS item, create `docs/verification/ots/{ots-name}.md` (`##` heading) covering:
verification approach (self-validation, integration tests, vendor evidence).

# Shared Package Verification Evidence (when Shared Packages exist)

Create `docs/verification/shared.md` (`#` heading) covering the overall Shared Package verification strategy.

For each Shared Package, create `docs/verification/shared/{package-name}.md` (`##` heading) covering:
verification approach.

# Writing Guidelines

- Name scenarios clearly ("Valid input returns parsed result", not "Test 1")
- Use verbal cross-references - not markdown hyperlinks (break in PDF)
- Use Mermaid diagrams to supplement (not replace) text

# Quality Checks

- [ ] `introduction.md` includes Companion Artifact Structure
- [ ] Each file's heading depth matches its folder depth
- [ ] All folders use kebab-case mirroring source structure
- [ ] Each system/subsystem/unit file includes all mandatory sections (Verification Approach,
  Test Environment, Acceptance Criteria, Test Scenarios)
- [ ] Non-applicable mandatory sections contain "N/A - {justification}"
- [ ] Requirements-to-test coverage is tracked via the ReqStream trace matrix, not in these documents
- [ ] `docs/verification/ots.md` and `docs/verification/ots/{ots-name}.md` exist when OTS items are present
- [ ] `docs/verification/shared.md` and `docs/verification/shared/{package-name}.md` exist when Shared Packages are present
- [ ] Documents are integrated into ReviewMark review-sets
