## DemaConsulting.SysML2Tools — Semantic Subsystem Verification

### Verification Approach

Semantic subsystem verification uses unit tests in `DemaConsulting.SysML2Tools.Tests`.
Tests exercise the public `WorkspaceLoader` API and validate that the symbol table is
populated correctly, that reference resolution produces expected diagnostics, and that
the embedded stdlib loads without errors. The xUnit v3 framework discovers and runs all
test methods; results are captured in TRX files consumed by ReqStream.

### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary files are created in `Path.GetTempPath()` and cleaned up after each test.

### Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- The stdlib loads without Error-level diagnostics (KerML parse errors are downgraded to Warnings).
- All 94 stdlib files are counted in the workspace file list.
- A single-package SysML file registers its package name in the workspace declarations.
- Nested packages register both parent and child qualified names.
- Part definitions register their qualified names.
- Unresolved supertype references produce Warning diagnostics.
- Circular imports produce Warning diagnostics without infinite loops.

### Test Scenarios

See *Semantic Verification* for the full list of test scenarios. Primary acceptance evidence
is provided by `WorkspaceLoader_LoadAsync_StdlibDeclarations_Registered`, which loads all
94 stdlib files and asserts `HasErrors` is false and `Declarations` is non-empty.
