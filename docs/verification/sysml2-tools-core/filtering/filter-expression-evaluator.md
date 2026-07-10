<!-- cspell:ignore istype hastype reparses -->

### FilterExpressionEvaluator Verification

#### Verification Approach

`FilterExpressionEvaluator` is verified through direct unit tests in `FilterExpressionParserTests`
and `FilterExpressionEvaluatorTests`. Parser tests exercise the AST builder, unsupported-construct
reporting, malformed-syntax handling, and canonical pretty-print round-tripping. Evaluator tests
load a small semantic workspace with metadata annotations and verify candidate matching over the
parsed AST. Integration evidence from `GeneralViewLayoutStrategyTests` and `RenderIntegrationTests`
confirms the parser/evaluator behavior composes correctly into layout and rendering.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Parser tests are fully
in-memory. Evaluator tests create temporary `.sysml` files, load them through `WorkspaceLoader`,
and delete them after each run. No external services or configuration are required beyond the .NET
SDK and the repository's committed SysML fixtures.

#### Acceptance Criteria

- All parser and evaluator tests pass with zero failures across all three target frameworks.
- Classification tests match candidates carrying the referenced metadata annotation.
- Boolean connectives and parentheses preserve the intended logical grouping.
- `(as Type).attribute` reads work both as bare boolean predicates and as scalar comparisons.
- Absent metadata attributes evaluate conservatively as false.
- Unsupported constructs and malformed syntax report diagnostics and never throw.
- Pretty-printing a supported AST re-parses to an equivalent tree.

#### Requirement-to-Test Mapping

- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-ClassificationTests`
  - `Parse_ClassificationTest_ReturnsClassificationTestExpression`
  - `Parse_QualifiedClassificationTest_PreservesQualifiedName`
  - `Evaluate_ClassificationTest_MatchesOnlyAnnotatedCandidates`
  - `Evaluate_QualifiedClassificationTest_Matches`
  - `Evaluate_ClassificationTestNoMatch_ReturnsEmpty`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-BooleanConnectives`
  - `Parse_AndConnective_ReturnsBooleanExpression`
  - `Parse_OrConnective_ReturnsBooleanExpression`
  - `Parse_XorConnective_ReturnsBooleanExpression`
  - `Parse_AmpSymbol_ReturnsAndWithSymbolSpelling`
  - `Parse_PipeSymbol_ReturnsOrWithSymbolSpelling`
  - `Parse_Not_ReturnsNotExpression`
  - `Parse_Parenthesized_ReturnsInnerExpression`
  - `Evaluate_Not_InvertsMatchSet`
  - `Evaluate_And_MatchesIntersection`
  - `Evaluate_Or_MatchesUnion`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-AttributeReads`
  - `Parse_AttributeRead_ReturnsAttributeReadExpression`
  - `Parse_AttributeReadEqualsBoolean_ReturnsComparisonExpression`
  - `Parse_ClassificationTestAndAttributeRead_ReAssociatesDotOntoRightOperand`
  - `Parse_AttributeReadNotEqualsString_ReturnsComparisonExpression`
  - `Parse_AttributeReadEqualsNumber_ReturnsComparisonExpression`
  - `Evaluate_BareAttributeRead_TrueOnlyWhenBooleanValueTrue`
  - `Evaluate_ComparisonEqual_MatchesEqualValue`
  - `Evaluate_ComparisonNotEqual_MatchesDifferingValue`
  - `Evaluate_AttributeReadAbsent_NeverMatchesComparison`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-UnsupportedConstructDiagnostics`
  - `Parse_Istype_ReturnsUnsupportedConstructDiagnostic`
  - `Parse_Hastype_ReturnsUnsupportedConstructDiagnostic`
  - `Parse_All_ReturnsUnsupportedConstructDiagnostic`
  - `Parse_Arithmetic_ReturnsUnsupportedConstructDiagnostic`
  - `Parse_Conditional_ReturnsUnsupportedConstructDiagnostic`
  - `Parse_GeneralFeatureChainNavigation_ReturnsUnsupportedConstructDiagnostic`
  - `Parse_MalformedSyntax_NeverThrows_ReturnsDiagnostic`
  - `Evaluate_UnknownCandidate_SkipsGracefully`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-RoundTripPrettyPrinting`
  - `Parse_RoundTrip_PrettyPrintedTextReparsesToEquivalentTree`

#### Test Scenarios

- `Parse_ClassificationTest_ReturnsClassificationTestExpression` — bare `@Type` filter parses to
  a classification-test node
- `Parse_ClassificationTestAndAttributeRead_ReAssociatesDotOntoRightOperand` — DOT is
  re-associated onto the boolean chain's rightmost operand
- `Parse_MalformedSyntax_NeverThrows_ReturnsDiagnostic` — syntax errors produce diagnostics
  instead of exceptions
- `Evaluate_ClassificationTest_MatchesOnlyAnnotatedCandidates` — only candidates carrying the
  requested metadata annotation match
- `Evaluate_BareAttributeRead_TrueOnlyWhenBooleanValueTrue` — bare attribute reads are true only
  for Boolean `true` values
- `Evaluate_ComparisonNotEqual_MatchesDifferingValue` — `!=` comparisons match differing captured
  values
- `Evaluate_UnknownCandidate_SkipsGracefully` — missing candidate declarations are ignored without
  failure
- `Parse_RoundTrip_PrettyPrintedTextReparsesToEquivalentTree` — pretty-printer output remains
  accepted by the parser
