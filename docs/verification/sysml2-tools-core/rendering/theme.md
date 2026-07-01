### Theme Verification

#### Verification Approach

`Theme` is verified through unit tests in `ThemeTests` that construct or read the built-in themes,
call `ConnectorApproachZone`, and assert on the returned geometry. The record is immutable and the
helper is pure, so no mocking is required.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

#### Acceptance Criteria

- All `ThemeTests` pass with zero failures across all target frameworks.
- `ConnectorApproachZone` returns the sum of the stub, bend radius, and supplied clearance.
- The Light and Dark themes carry the same connector geometry; the Print theme is tighter with a
  zero bend radius.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ConnectorApproachZone_SumsStubBendAndClearance` | The approach zone equals stub + bend radius + clearance |
| `Themes_HaveExpectedConnectorGeometry` | Light/Dark share connector geometry; Print is tighter with zero bend |
