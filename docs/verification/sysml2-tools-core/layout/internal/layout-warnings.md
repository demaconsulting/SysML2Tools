#### LayoutWarnings Verification

##### Verification Approach

`LayoutWarnings` is verified through unit tests in `LayoutWarningsTests` that call `ForCrossings`
with a view name and a crossing count and assert on the returned list. The unit is a pure function,
so no mocking is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `LayoutWarningsTests` pass with zero failures across all target frameworks.
- A zero crossing count yields no warning.
- A count of one yields a single singular-form warning naming the view.
- A count greater than one yields a single plural-form warning reporting the count.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ForCrossings_Zero_ReturnsEmpty` | Zero crossings yields an empty list |
| `ForCrossings_One_ReturnsSingularWarning` | One crossing yields a singular warning naming the view |
| `ForCrossings_Many_ReturnsPluralWarning` | Multiple crossings yield a plural warning with the count |
