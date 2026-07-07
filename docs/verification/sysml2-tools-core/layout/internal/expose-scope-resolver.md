#### ExposeScopeResolver Verification

##### Verification Approach

`ExposeScopeResolver` is verified through direct unit tests in `ExposeScopeResolverTests` that
call `ResolveExposedScope`, `IsInSubjectScope`, `IsRootRelevantToScope`, and
`IsMoreSpecificCandidate` directly with synthetic `SysmlWorkspace`/`SysmlViewNode` inputs and
assert on the returned scope or boolean result. No mocking is required; every method is a pure
function over its parameters.

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
- Two resolved `Expose` edges on the same view — one targeting a plain definition, one targeting a
  feature usage — union both targets plus the usage's resolved type into the returned scope.
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
- With no current best candidate, `IsMoreSpecificCandidate` returns `true` for any candidate.
- `IsMoreSpecificCandidate` returns `true` for a candidate with a longer qualified name than the
  current best, regardless of score.
- `IsMoreSpecificCandidate` returns `false` for a candidate with a shorter qualified name than the
  current best, regardless of score.
- `IsMoreSpecificCandidate` falls back to the caller-supplied score comparison when the candidate
  and current best are equally deeply nested (whether their qualified names are equal in length or
  merely equal in containment depth).

##### Test Scenarios

- `ResolveExposedScope_NullViewNode_ReturnsNull`:
  Null `ViewNode` resolves to `null` scope
- `ResolveExposedScope_NoResolvedExposeEdges_ReturnsNull`:
  No resolved `Expose` edges resolves to `null` scope
- `ResolveExposedScope_ExposedDefinition_ReturnsThatQualifiedName`:
  Exposed definition resolves to a scope of exactly that qualified name
- `ResolveExposedScope_ExposedUsage_AlsoIncludesResolvedTypeTarget`:
  Exposed usage resolves to a scope including both the usage and its resolved type
- `ResolveExposedScope_TwoExposeEdges_DefinitionAndUsageTarget_UnionsBothPlusResolvedType`:
  Two `Expose` edges (a definition and a usage) union both targets plus the usage's resolved type
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
- `IsMoreSpecificCandidate_NoCurrentBest_ReturnsTrue`:
  No current best candidate: any candidate becomes the new best
- `IsMoreSpecificCandidate_LongerQualifiedName_ReturnsTrueRegardlessOfScore`:
  Longer (more deeply nested) qualified name wins even with a worse score
- `IsMoreSpecificCandidate_ShorterQualifiedName_ReturnsFalseRegardlessOfScore`:
  Shorter qualified name loses even with a better score
- `IsMoreSpecificCandidate_EqualLength_FallsBackToScore_True`:
  Equal-length qualified names fall back to the score comparison — true case
- `IsMoreSpecificCandidate_EqualLength_FallsBackToScore_False`:
  Equal-length qualified names fall back to the score comparison — false case
- `IsMoreSpecificCandidate_SameDepthSiblingsDifferentLength_ShorterWithBetterScoreWins`:
  Same-depth siblings with different qualified-name lengths fall back to score — shorter
  candidate with a better score wins
- `IsMoreSpecificCandidate_SameDepthSiblingsDifferentLength_ShorterWithWorseScoreLoses`:
  Same-depth siblings with different qualified-name lengths fall back to score — shorter
  candidate with a worse score loses
