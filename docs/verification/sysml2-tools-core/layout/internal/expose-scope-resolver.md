#### ExposeScopeResolver Verification

##### Verification Approach

`ExposeScopeResolver` is verified through direct unit tests in `ExposeScopeResolverTests` that
call `ResolveExposedScope`, `IsInSubjectScope`, and `IsRootRelevantToScope` directly with
synthetic `SysmlWorkspace`/`SysmlViewNode` inputs and assert on the returned scope or boolean
result. No mocking is required; every method is a pure function over its parameters.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ExposeScopeResolverTests` pass with zero failures across all three target frameworks.
- A `null` `ViewNode` resolves to a `null` scope.
- A `ViewNode` with no resolved `Expose`-kind edges resolves to a `null` scope.
- A resolved `Expose` edge targeting a definition resolves to a scope containing exactly that
  definition's qualified name.
- A resolved `Expose` edge targeting a feature usage additionally includes that usage's own
  resolved `Typing` edge target qualified name in the scope.
- `IsInSubjectScope` returns `true` for an exact qualified-name match.
- `IsInSubjectScope` returns `true` for a qualified name nested under a subject (a `"{subject}::"`
  prefix match).
- `IsInSubjectScope` returns `false` for a qualified name that shares a string prefix with a
  subject but without the `"::"` separator (e.g. `Root::AB` vs. subject `Root::A`).
- `IsInSubjectScope` returns `false` for an unrelated qualified name.
- `IsRootRelevantToScope` returns `true` when the candidate equals a subject.
- `IsRootRelevantToScope` returns `true` when the candidate is nested within a subject.
- `IsRootRelevantToScope` returns `true` when a subject is nested within the candidate.
- `IsRootRelevantToScope` returns `false` for an unrelated candidate.

##### Test Scenarios

- `ResolveExposedScope_NullViewNode_ReturnsNull`:
  Null `ViewNode` resolves to `null` scope
- `ResolveExposedScope_NoResolvedExposeEdges_ReturnsNull`:
  No resolved `Expose` edges resolves to `null` scope
- `ResolveExposedScope_ExposedDefinition_ReturnsThatQualifiedName`:
  Exposed definition resolves to a scope of exactly that qualified name
- `ResolveExposedScope_ExposedUsage_AlsoIncludesResolvedTypeTarget`:
  Exposed usage resolves to a scope including both the usage and its resolved type
- `IsInSubjectScope_ExactMatch_ReturnsTrue`:
  Exact qualified-name match is in scope
- `IsInSubjectScope_SubtreeMatch_ReturnsTrue`:
  Nested qualified name is in scope
- `IsInSubjectScope_PrefixWithoutSeparator_ReturnsFalse`:
  String-prefix-only match (no `::`) is not in scope
- `IsInSubjectScope_UnrelatedName_ReturnsFalse`:
  Unrelated qualified name is not in scope
- `IsRootRelevantToScope_CandidateEqualsSubject_ReturnsTrue`:
  Candidate equal to a subject is relevant
- `IsRootRelevantToScope_CandidateNestedInSubject_ReturnsTrue`:
  Candidate nested within a subject is relevant
- `IsRootRelevantToScope_SubjectNestedInCandidate_ReturnsTrue`:
  Subject nested within the candidate is relevant
- `IsRootRelevantToScope_UnrelatedCandidate_ReturnsFalse`:
  Unrelated candidate is not relevant
