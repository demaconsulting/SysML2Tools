# SysMLModels

This folder contains SysML v2 model files used as test inputs for
`DemaConsulting.SysML2Tools`. Each subfolder represents a distinct origin
(upstream source) so that provenance, licensing, and update instructions
remain clear.

## Structure

```text
SysMLModels/
  README.md          — this file
  Custom/            — hand-authored models for parser/semantic coverage
  OMG/               — models from Systems-Modeling/SysML-v2-Release
    README.md        — source, license, and transformation notes
    examples/        — OMG example models
    training/        — OMG training models
    validation/      — OMG validation models
  TestCases/         — minimal regression models for layout/rendering quality
    README.md        — purpose, acceptance criteria table, and contribution guide
```

## Folder purposes

| Folder | Purpose |
| --- | --- |
| `Custom/` | Hand-authored models covering parser and semantic model breadth |
| `OMG/` | Upstream OMG release models; provenance and license documented in subfolder README |
| `TestCases/` | Minimal adversarial regression models for layout and rendering quality; grows monotonically |

## Adding a New Origin

1. Create a new subfolder named after the source (e.g., `MyOrg/`).
2. Add a `README.md` documenting the source URL, commit/tag, license,
   and any filename transformations applied.
3. Add a test in `DemaConsulting.SysML2Tools.Tests` that references the new
   folder.

## Adding a Test Case

See `TestCases/README.md` for the contribution workflow.
