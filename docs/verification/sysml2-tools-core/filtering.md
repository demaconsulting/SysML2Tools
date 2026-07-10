<!-- cspell:ignore reparses -->

## DemaConsulting.SysML2Tools — Filtering Subsystem Verification

### Verification Approach

The Filtering subsystem is verified by focused parser/evaluator unit tests in
`DemaConsulting.SysML2Tools.Tests.Filtering` plus end-to-end rendering tests in
`RenderIntegrationTests` and layout integration tests in `GeneralViewLayoutStrategyTests`. The
unit tests exercise the parser and evaluator directly against inline source text and synthetic
candidate sets; the integration tests confirm that a view's captured `FilterExpressionText` really
narrows the rendered scope or, on failure, falls back to the unfiltered scope with a warning.

### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Parser tests operate entirely
in-memory. Evaluator and integration tests load temporary or repository-owned `.sysml` models
through `WorkspaceLoader` with the seeded standard library. No external services or network access
are required.

### Acceptance Criteria

- All filtering parser, evaluator, layout, and rendering tests pass with zero failures across all
  three target frameworks.
- A standalone `filter [<expr>];` expression can narrow a candidate set by metadata annotation
  presence alone.
- Boolean composition and metadata-attribute reads can combine to narrow the rendered scope to a
  strict subset of annotated candidates.
- A supported filter that matches no candidates renders an empty General View rather than falling
  back to the unfiltered scope.
- Malformed or unsupported filter expressions surface explicit diagnostics and cause layout to
  render the unfiltered resolved scope with a warning instead of throwing.
- Canonical pretty-printing of supported filter expressions round-trips through the parser.

### Test Scenarios

- `Parse_ClassificationTest_ReturnsClassificationTestExpression` — bare metadata classification
  test parses successfully
- `Parse_AttributeReadEqualsBoolean_ReturnsComparisonExpression` — attribute-read comparison
  parses successfully
- `Parse_ClassificationTestAndAttributeRead_ReAssociatesDotOntoRightOperand` — the DOT/boolean
  grammar quirk is repaired into the intended AST
- `Parse_MalformedSyntax_NeverThrows_ReturnsDiagnostic` — malformed syntax reports diagnostics
  without throwing
- `Evaluate_ClassificationTest_MatchesOnlyAnnotatedCandidates` — metadata classification narrows
  to annotated candidates
- `Evaluate_And_MatchesIntersection` — boolean conjunction yields the intersection of matches
- `Evaluate_ComparisonEqual_MatchesEqualValue` — string-valued metadata attribute comparison
  matches correctly
- `Evaluate_AttributeReadAbsent_NeverMatchesComparison` — missing metadata attributes evaluate
  conservatively as false
- `Parse_RoundTrip_PrettyPrintedTextReparsesToEquivalentTree` — canonical pretty-printing
  re-parses to an equivalent tree
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionMatchesNothing_RendersEmpty` —
  a supported filter can narrow the General View to zero boxes
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionPresent_EmitsNotYetEvaluatedWarning` —
  unsupported filter text falls back to the unfiltered scope with a warning
- `DiagramRenderer_RenderWorkspace_SafetyPartsView_FiltersToAnnotatedParts` — end-to-end
  rendering includes only `@Safety`-annotated definitions
- `DiagramRenderer_RenderWorkspace_MandatorySafetyPartsView_FiltersToMandatoryPart` —
  end-to-end rendering combines classification and attribute-read predicates
