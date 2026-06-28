#### GridQuantizer Verification

##### Verification Approach

`GridQuantizer` is verified through unit tests in `GridQuantizerTests` that construct explicit box
lists and assert on the snapped, unified rectangles. The engine is pure and deterministic, so
determinism is verified by quantising identical input twice and comparing the results. No mocking is
required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `GridQuantizerTests` pass with zero failures across all three target frameworks.
- Positions snap to the nearest grid multiple.
- Boxes in a column unify to the wider width; boxes in different columns do not.
- Identical input produces identical rectangles.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Quantize_SnapsToNearestMultiple` | Position rounds to nearest grid line |
| `Quantize_SimilarWidths_UnifiedToWider` | Column widths unify to the wider |
| `Quantize_DifferentColumns_NotUnified` | Distinct columns keep own widths |
| `Quantize_SameInput_IsDeterministic` | Identical input yields identical rectangles |
