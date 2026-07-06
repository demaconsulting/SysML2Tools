#### LayoutWarnings Verification

##### Verification Approach

`LayoutWarnings` is verified through unit tests in `LayoutWarningsTests` that call `ForCrossings`
with a view name and a crossing count, and `ForUnevaluatedFilter` with a view name and a filter
expression text, asserting on the returned lists in each case. The unit is a pure function, so no
mocking is required.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `LayoutWarningsTests` pass with zero failures across all target frameworks.
- A zero crossing count yields no warning.
- A count of one yields a single singular-form warning naming the view.
- A count greater than one yields a single plural-form warning reporting the count.
- A null filter expression text yields no warning.
- A non-null filter expression text yields a single warning naming the view and stating the
  filter expression is parsed but not yet evaluated.

##### Test Scenarios

| Test | Assertion |
| --- | --- |
| `ForCrossings_Zero_ReturnsEmpty` | Zero crossings yields an empty list |
| `ForCrossings_One_ReturnsSingularWarning` | One crossing yields a singular warning naming the view |
| `ForCrossings_Many_ReturnsPluralWarning` | Multiple crossings yield a plural warning with the count |
| `ForUnevaluatedFilter_NullText_ReturnsEmpty` | A null filter expression text yields an empty list |
| `ForUnevaluatedFilter_NonNullText_ReturnsNotYetEvaluatedWarning` | Non-null filter yields a warning naming the view |
