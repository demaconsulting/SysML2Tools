# SysMLModels

This folder contains SysML v2 model files used as test inputs for
`DemaConsulting.SysML2Tools`. Each subfolder represents a distinct origin
(upstream source) so that provenance, licensing, and update instructions
remain clear.

## Structure

```text
SysMLModels/
  README.md          — this file
  OMG/               — models from Systems-Modeling/SysML-v2-Release
    README.md        — source, license, and transformation notes
    examples/        — OMG example models
    training/        — OMG training models
    validation/      — OMG validation models
```

## Adding a New Origin

1. Create a new subfolder named after the source (e.g., `MyOrg/`).
2. Add a `README.md` documenting the source URL, commit/tag, license,
   and any filename transformations applied.
3. Add a test in `DemaConsulting.SysML2Tools.Tests` that references the new
   folder.
