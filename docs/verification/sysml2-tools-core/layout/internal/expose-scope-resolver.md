#### ExposeScopeResolver Verification

##### Verification Approach

`ExposeScopeResolver` is verified through direct unit tests in `ExposeScopeResolverTests` that
call `ResolveExposedScope`, `IsInSubjectScope`, `IsRootRelevantToScope`, and
`IsMoreSpecificCandidate` directly with synthetic `SysmlWorkspace`/`SysmlViewNode` inputs and
assert on the returned `ExposedScope` (Phase 2a: `Subjects`/`ExplicitMembers`/`Failures`,
replacing the earlier flat qualified-name-list shape) or boolean result. No mocking is required;
every method is a pure function over its parameters.

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `ExposeScopeResolverTests` pass with zero failures across all three target frameworks.
- A `null` `ViewNode` resolves to a `null` scope.
- A `ViewNode` with no resolved `Expose`-kind edges resolves to a `null` scope.
- A resolved `Expose` edge targeting a definition resolves to an `ExposedScope` whose
  `Subjects` contains exactly that definition's qualified name.
- A resolved `Expose` edge targeting a feature usage additionally includes that usage's own
  resolved `Typing` edge target qualified name in `Subjects`.
- Two resolved `Expose` edges on the same view — one targeting a plain definition, one targeting a
  feature usage — union both targets plus the usage's resolved type into `Subjects`.
- (Phase 2a) An `expose <path>::**[<expr>];` entry whose bracket-filter expression parses and
  evaluates successfully narrows to only the matched descendant `SysmlDefinitionNode`s under that
  entry's containment subtree, added to `ExposedScope.ExplicitMembers` — the target itself is
  *not* added to `Subjects` (no whole-subtree fallback for a successfully-evaluated entry).
- (Phase 2a) Two `expose` entries on the same view, only one bracket-filtered, narrow
  independently: the unfiltered entry keeps its whole containment subtree in `Subjects`
  while the bracket-filtered entry narrows to only its own matched members in `ExplicitMembers`.
- (Phase 2a) A bracket-filter expression that fails to parse or evaluate degrades gracefully to
  whole-subtree inclusion for that entry (added to `Subjects`, same as the unfiltered case)
  and records a `BracketFilterFailure` (expression text plus a reason) in `ExposedScope.Failures`.
- A non-recursive `MembershipExact` `expose X;` entry resolves to a scope containing only `X`
  itself — not its containment subtree — and, for a usage target, adds the resolved type using
  the same exact-match recursion kind (itself only, not the type's subtree either).
- A recursive `MembershipRecursive`/`NamespaceRecursive` `expose X::**;`/`expose X::*::**;` entry
  continues to resolve to a scope containing `X` and its entire containment subtree, unchanged
  from before this fix.
- A non-recursive `NamespaceDirectChildren` `expose X::*;` entry resolves to a scope containing
  only `X`'s direct (one-level) children — not `X` itself and not deeper descendants.
- A bracket-filter expression that fails to parse or evaluate always falls back to whichever
  Recursive variant matches the entry's form, regardless of the entry's own recursion kind —
  never degrading to the narrower Exact/DirectChildren behavior on a fallback.
- (Phase 2d) A successfully-evaluated bracket-filter expression's candidate set includes named
  usage-level (`SysmlFeatureNode`) declarations, not just `SysmlDefinitionNode`s, so a
  metaclass-kind filter like `@SysML::PartUsage` can match a usage-level candidate.
- `IsInSubjectScope` returns `true` for an exact qualified-name match against a `Subjects`
  entry.
- `IsInSubjectScope` returns `true` for a qualified name nested under a `Subjects` entry (a
  `"{subject}::"` prefix match).
- `IsInSubjectScope` returns `false` for a qualified name that shares a string prefix with a
  `Subjects` entry but without the `"::"` separator (e.g. `Root::AB` vs. subject `Root::A`).
- `IsInSubjectScope` returns `false` for an unrelated qualified name.
- (Phase 2a) `IsInSubjectScope` returns `true` for an exact qualified-name match against an
  `ExplicitMembers` entry, and `false` for one of that entry's own descendants — an
  `ExplicitMembers` match is exact-only, not a subtree match.
- `IsRootRelevantToScope` returns `true` when the candidate equals a `Subjects` entry.
- `IsRootRelevantToScope` returns `true` when the candidate is nested within a `Subjects`
  entry.
- `IsRootRelevantToScope` returns `true` when a `Subjects` entry is nested within the
  candidate.
- `IsRootRelevantToScope` returns `false` for an unrelated candidate.
- (Phase 2a) `IsRootRelevantToScope` returns `true` when the candidate exactly equals an
  `ExplicitMembers` entry.
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
  Exposed definition resolves to a scope whose `Subjects` is exactly that qualified name
- `ResolveExposedScope_ExposedUsage_AlsoIncludesResolvedTypeTarget`:
  Exposed usage resolves to a scope including both the usage and its resolved type in
  `Subjects`
- `ResolveExposedScope_TwoExposeEdges_DefinitionAndUsageTarget_UnionsBothPlusResolvedType`:
  Two `Expose` edges (a definition and a usage) union both targets plus the usage's resolved type
  into `Subjects`
- `ResolveExposedScope_BracketFilterEvaluatesSuccessfully_NarrowsToMatchedDefinitionsOnly`:
  A successfully-evaluated bracket filter narrows to only the matched descendant definitions in
  `ExplicitMembers`, with an empty `Subjects` and no `Failures` for that entry
- `ResolveExposedScope_MixedFilteredAndUnfilteredEntries_NarrowsIndependently`:
  An unfiltered `expose` entry and a bracket-filtered `expose` entry on the same view narrow
  independently — whole subtree in `Subjects` for the former, matched members in
  `ExplicitMembers` for the latter
- `ResolveExposedScope_BracketFilterFailsToParse_FallsBackToWholeSubtreeAndRecordsFailure`:
  A bracket filter that fails to parse falls back to whole-subtree inclusion in `Subjects`
  and records a `BracketFilterFailure` (expression text and reason) in `Failures`
- `ResolveExposedScope_MembershipExact_ScopeIsExactOnly`:
  A non-recursive `MembershipExact` entry scopes to only the exposed subject itself, not its
  containment subtree
- `ResolveExposedScope_MembershipExactUsage_TypeFallbackIsAlsoExactOnly`:
  A non-recursive `MembershipExact` entry targeting a usage adds the resolved type using the same
  exact-match recursion kind — the type's own descendants are not included either
- `ResolveExposedScope_MembershipRecursive_ScopeIsWholeSubtree`:
  A recursive `MembershipRecursive` entry scopes to the exposed subject's entire containment
  subtree — unchanged whole-subtree behavior
- `ResolveExposedScope_NamespaceDirectChildren_ScopeIsDirectChildrenOnly`:
  A non-recursive `NamespaceDirectChildren` entry scopes to only the exposed subject's direct
  children — not the subject itself and not deeper descendants
- `ResolveExposedScope_NamespaceRecursive_ScopeIsWholeSubtree`:
  A recursive `NamespaceRecursive` entry scopes to the exposed subject and its entire containment
  subtree — unchanged whole-subtree behavior
- `ResolveExposedScope_BracketFilterFailure_AlwaysFallsBackToRecursive`:
  A bracket-filter parse failure on an entry classified as `NamespaceDirectChildren` still falls
  back to whole-subtree (Recursive) inclusion, never the narrower direct-children behavior
- `ResolveExposedScope_BracketFilterMetaclassKind_MatchesUsageLevelCandidate`:
  A successfully-evaluated bracket-filter metaclass-kind expression matches a named usage-level
  candidate (`SysmlFeatureNode`), not just definitions
- `IsInSubjectScope_ExactMatch_ReturnsTrue`:
  Exact qualified-name match against a `Subjects` entry is in scope
- `IsInSubjectScope_SubtreeMatch_ReturnsTrue`:
  Nested qualified name under a `Subjects` entry is in scope
- `IsInSubjectScope_PrefixWithoutSeparator_ReturnsFalse`:
  String-prefix-only match (no `::`) is not in scope
- `IsInSubjectScope_UnrelatedName_ReturnsFalse`:
  Unrelated qualified name is not in scope
- `IsInSubjectScope_ExplicitMemberExactMatch_ReturnsTrue`:
  Exact qualified-name match against an `ExplicitMembers` entry is in scope
- `IsInSubjectScope_ExplicitMemberDescendant_ReturnsFalse`:
  A descendant of an `ExplicitMembers` entry is not automatically in scope (exact match only)
- `IsRootRelevantToScope_CandidateEqualsSubject_ReturnsTrue`:
  Candidate equal to a `Subjects` entry is relevant
- `IsRootRelevantToScope_CandidateNestedInSubject_ReturnsTrue`:
  Candidate nested within a `Subjects` entry is relevant
- `IsRootRelevantToScope_SubjectNestedInCandidate_ReturnsTrue`:
  A `Subjects` entry nested within the candidate is relevant
- `IsRootRelevantToScope_UnrelatedCandidate_ReturnsFalse`:
  Unrelated candidate is not relevant
- `IsRootRelevantToScope_ExplicitMember_ReturnsTrue`:
  Candidate exactly equal to an `ExplicitMembers` entry is relevant
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
