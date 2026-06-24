---
name: ReqStream Usage
description: Follow these standards when managing requirements with ReqStream.
globs: ["requirements.yaml", "docs/reqstream/**/*.yaml"]
---

# Required Standards

Read these standards first before applying this standard:

- **`requirements-principles.md`** - Requirements principles and unidirectionality
- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)

# Requirements Organization

Organize requirements under `docs/reqstream/` mirroring the source code structure
because ReqStream discovers files via the includes chain in `requirements.yaml`
and organizes report output by this hierarchy:

```text
requirements.yaml                    # Root file (includes only)
docs/reqstream/
├── {system-name}.yaml               # System-level requirements
├── {system-name}/                   # System folder (one per system)
│   ├── platform-requirements.yaml  # Platform support requirements
│   ├── {subsystem-name}.yaml        # Subsystem requirements
│   ├── {subsystem-name}/            # Subsystem folder (kebab-case); may nest recursively
│   │   ├── {subsystem-name}.yaml    # Child subsystem requirements
│   │   ├── {subsystem-name}/        # Child subsystem folder
│   │   └── {unit-name}.yaml         # Unit requirements
│   └── {unit-name}.yaml             # System-level unit requirements
├── ots/                             # OTS items appear as a distinct section in reports
│   └── {ots-name}.yaml              # Requirements for OTS components
└── shared/                          # Shared Packages appear as a distinct section in reports
    └── {package-name}.yaml          # Requirements for Shared Package dependencies
```

Local items have matching relative paths across `docs/reqstream/`, `docs/design/`, and `docs/verification/`:

- Requirements: `{system-name}[/{subsystem-name}...]/{item-name}.yaml`
- Design: `{system-name}[/{subsystem-name}...]/{item-name}.md`
- Verification: `{system-name}[/{subsystem-name}...]/{item-name}.md`

# Requirements File Format

Each file adds requirements at exactly one level of the hierarchy. The file spells out
its full ancestry as nested `{ItemName} Requirements` sections down to that level, then
places requirements there. ReqStream merges identical section title paths across included
files automatically. Always determine item classification from `docs/design/introduction.md` -
folder depth does not determine whether an item is a subsystem or unit.

Valid section nestings (names in `{braces}` are placeholders):

```text
{SystemName} Requirements              # system-level requirements
├── {SubsystemName} Requirements       # root subsystem requirements
│   ├── {SubsystemName} Requirements   # nested subsystem (may recurse)
│   │   └── {UnitName} Requirements    # unit under a nested subsystem
│   └── {UnitName} Requirements        # unit under a root subsystem
└── {UnitName} Requirements            # unit directly under the system
OTS Software Requirements          # OTS root section (fixed title)
└── {OtsName} Requirements         # requirements for one OTS item
Shared Package Requirements        # shared package root section (fixed title)
└── {PackageName} Requirements     # requirements for one shared package
```

Each file implements one path through this tree:

```yaml
sections:
  - title: '{SystemName} Requirements'
    sections:
      - title: '{SubsystemName} Requirements'
        requirements:
          - id: System-Subsystem-Feature    # Used as-is in all reports - make it readable
            title: The subsystem shall perform the required function.
            justification: |              # ReqStream extracts this into the justifications report (--justifications)
              Business rationale and any regulatory references.
            tags:                         # Optional: categorize for filtering with --filter
              - security
            children:                     # Optional: ReqStream validates this decomposition chain
              - System-Subsystem-Unit-Feat  # Downward links only (see requirements-principles.md)
            tests:                        # ReqStream matches these by method name in test results
              - TestMethodName
              - windows@PlatformSpecificTest  # Only test runs on Windows count as evidence
```

# Tags (OPTIONAL)

Tags are free-form - no mandatory vocabulary. Common tags: `security`, `safety`, `performance`,
`compliance`, `reliability`, `critical`. Use `--filter` to selectively export or enforce subsets
(OR logic across comma-separated tags):

```bash
dotnet reqstream --requirements requirements.yaml \
  --filter security,critical \
  --report docs/requirements_doc/generated/security_requirements.md
```

# Semantic IDs (MANDATORY)

Use the `System-Component-Feature` pattern because ReqStream uses IDs as-is in
all generated reports and the trace matrix - opaque IDs make those outputs
unreadable without a separate lookup:

- **System-level**: `TemplateTool-Core-DisplayHelp`
- **Subsystem-level**: `TemplateTool-Parser-ParseYaml`
- **Unit-level**: `TemplateTool-Validator-CheckFormat`
- **Bad**: `REQ-042` (meaningless in report output)

# Source Filter Requirements (CRITICAL)

Platform-specific requirements MUST use source filters because without them
ReqStream accepts test results from any platform as evidence - a Windows-only
requirement would incorrectly pass on Linux:

```yaml
tests:
  - "windows@TestMethodName"    # Only Windows test runs count as evidence
  - "ubuntu@TestMethodName"     # Only Linux test runs count as evidence
  - "net8.0@TestMethodName"     # Only .NET 8 runs count as evidence
  - "TestMethodName"            # Any platform acceptable
```

**WARNING**: Removing source filters invalidates platform-specific compliance
evidence.

# ReqStream Commands

```bash
# Validate YAML syntax and requirement IDs before generating any reports
dotnet reqstream --requirements requirements.yaml --lint

# Generate requirements document for compliance record
dotnet reqstream --requirements requirements.yaml \
  --report docs/requirements_doc/generated/requirements.md

# Generate justifications document for compliance record
dotnet reqstream --requirements requirements.yaml \
  --justifications docs/requirements_doc/generated/justifications.md

# Generate trace matrix proving each requirement is covered by passing tests
dotnet reqstream --requirements requirements.yaml \
  --tests "artifacts/**/*.trx" \
  --matrix docs/requirements_report/generated/trace_matrix.md
```

# Quality Checks

Before submitting requirements, verify:

- [ ] All requirements have semantic IDs (`System-Component-Feature` pattern)
- [ ] Every requirement has a justification explaining business/regulatory need
- [ ] Every requirement links to at least one test
- [ ] Platform-specific requirements use source filters (`platform@TestName`)
- [ ] All files and folders use kebab-case names matching source code structure
- [ ] All files are organized under `docs/reqstream/` following the folder structure above
