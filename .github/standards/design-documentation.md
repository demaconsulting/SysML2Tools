---
name: Design Documentation
description: Follow these standards when creating design documentation.
globs: ["docs/design/**/*.md"]
---

# Required Standards

- **`technical-documentation.md`** - General technical documentation standards
- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)

# Folder Structure

```text
docs/design/
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
Do not record version numbers anywhere in design documentation — version information is managed in SBOMs.

# introduction.md (MANDATORY)

Must include:

- **Purpose**: audience and compliance drivers
- **Scope**: items covered and explicitly excluded (no test projects)
- **Software Structure**: text tree showing all Systems/Subsystems/Units/OTS/Shared items
- **Folder Layout**: text tree showing source folder structure
- **Companion Artifact Structure**: parallel paths for requirements, design, verification, source, tests
- **References** _(if applicable)_: external standards or specifications - only in `introduction.md`

# System Design (MANDATORY)

Create `{system-name}.md` (`#` heading) and `{system-name}/` folder:

- **Architecture**: software items, relationships, and collaboration
- **External Interfaces**: name, direction, format, constraints
- **Dependencies**: OTS and Shared Packages used; cross-reference their design docs
- **Risk Control Measures**: segregation required for risk control (IEC 62304 §5.3.3)
- **Data Flow**: inputs to outputs
- **Design Constraints**: platform, performance, security, regulatory

# Subsystem Design (MANDATORY)

Place `{subsystem-name}.md` in the **parent** folder; create `{subsystem-name}/` for children:

- **Overview**: responsibility, boundaries, contained units
- **Interfaces**: what it exposes and consumes
- **Design**: how internal units collaborate

# Unit Design (MANDATORY)

Place `{unit-name}.md` in the **parent** folder:

- **Purpose**: single responsibility
- **Data Model**: fields, properties, types, invariants (IEC 62304 §5.4.2)
- **Key Methods**: name, purpose, algorithm, preconditions, postconditions, parameter types
- **Error Handling**: detection and handling; what is propagated vs. handled locally
- **Dependencies**: other units, subsystems, OTS items, and shared packages used
- **Callers**: units or subsystems that call or consume this unit

# OTS Integration Design (when OTS items exist)

Create `docs/design/ots.md` (`#` heading) covering the overall OTS integration strategy.

For each OTS item, create `docs/design/ots/{ots-name}.md` (`##` heading) with sections:

- **Purpose**: why chosen and what it provides to the local system
- **Features Used**: which specific features, APIs, or capabilities are consumed
- **Integration Pattern**: how it is consumed; initialization, configuration, disposal requirements

# Shared Package Integration Design (when Shared Packages exist)

Create `docs/design/shared.md` (`#` heading) covering the overall consumption strategy.

For each Shared Package, create `docs/design/shared/{package-name}.md` (`##` heading) with sections:

- **Advertised Features Consumed**: which features the local system relies on
- **Integration Pattern**: how the package is referenced, initialized, and consumed
- **Assumptions**: any assumptions the local system makes about the package's behavior

# Writing Guidelines

- Use Mermaid diagrams to supplement (not replace) text
- Use verbal cross-references ("see _Parser Design_") - not markdown hyperlinks (break in PDF)
- Provide sufficient detail for formal code review

# Quality Checks

- [ ] `introduction.md` includes Software Structure, Folder Layout, and Companion Artifact Structure
- [ ] Software structure correctly categorizes items per `software-items.md`
- [ ] Each file's heading depth matches its folder depth
- [ ] All folders use kebab-case mirroring source structure
- [ ] System design includes all mandatory sections (Architecture, External Interfaces, Dependencies,
  Risk Control Measures, Data Flow, Design Constraints)
- [ ] Subsystem design includes all mandatory sections (Overview, Interfaces, Design)
- [ ] Unit design includes all mandatory sections (Purpose, Data Model, Key Methods, Error Handling, Dependencies, Callers)
- [ ] Non-applicable mandatory sections contain "N/A - {justification}"
- [ ] `docs/design/ots.md` and `docs/design/ots/{ots-name}.md` exist when OTS items are present
- [ ] `docs/design/shared.md` and `docs/design/shared/{package-name}.md` exist when Shared Packages are present
- [ ] Documents are integrated into ReviewMark review-sets
