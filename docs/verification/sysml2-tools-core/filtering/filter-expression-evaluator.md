<!-- cspell:ignore istype hastype reparses parenthesization -->

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
- Pathologically deep nesting (thousands of levels of parenthesization, or hundreds of levels of
  sequence-indexing brackets or body-expression braces) reports a diagnostic instead of
  overflowing the native call stack and crashing the process.
- Filter text containing non-BMP (astral-plane) Unicode characters never throws.
- A syntactically valid expression prefix followed by trailing content reports a diagnostic
  instead of silently discarding the trailing tokens.
- A numeric literal that overflows `double` during parsing (producing a non-finite value) reports
  a diagnostic instead of silently round-tripping to unparsable `"Infinity"`/`"NaN"` text.
- A classification test (`@Type`/`@Pkg::Type`) matches a candidate whose own AST node kind
  (`DefinitionKeyword`/`FeatureKeyword`) maps to the requested built-in SysML metaclass name, in
  both bare (`@PartUsage`) and `SysML::`-qualified (`@SysML::PartUsage`) spelling, on both a usage
  and a definition, without affecting existing applied-annotation classification-test matching.
- A metaclass-kind classification test for an unrelated metaclass does not match.
- A metaclass-kind classification test also matches via the stdlib's `specializes` chain (e.g.
  `@ConstraintUsage` matches a `RequirementUsage`-kind candidate).

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
  - `Parse_DeeplyNestedParentheses_ReturnsDiagnosticInsteadOfCrashing`
  - `Parse_ModeratelyNestedParentheses_StillParsesSuccessfully`
  - `Parse_DeeplyNestedBracketIndexing_ReturnsDiagnosticInsteadOfCrashing`
  - `Parse_ShallowBracketIndexing_ReturnsUnsupportedConstructNotDeepNestingDiagnostic`
  - `Parse_DeeplyNestedBodyExpressionBraces_ReturnsDiagnosticInsteadOfCrashing`
  - `Parse_ShallowBodyExpressionBraces_ReturnsUnsupportedConstructNotDeepNestingDiagnostic`
  - `Parse_AstralPlaneUnicodeCharacter_NeverThrows_ReturnsDiagnostic`
  - `Parse_AstralPlaneUnicodeCharacterAsTrailingToken_NeverThrows_ReturnsDiagnostic`
  - `Parse_TrailingGarbageAfterValidExpression_ReturnsDiagnostic`
  - `Parse_TrailingCloseParen_ReturnsDiagnostic`
  - `Parse_TrailingSemicolon_ReturnsDiagnostic`
  - `Evaluate_UnknownCandidate_SkipsGracefully`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-RoundTripPrettyPrinting`
  - `Parse_RoundTrip_PrettyPrintedTextReparsesToEquivalentTree`
  - `Parse_NumericLiteralOverflow_ReturnsDiagnosticInsteadOfInfinity`
  - `Parse_LargeButFiniteRealLiteral_StillParsesSuccessfully`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-MetaclassKindClassificationTests`
  - `Evaluate_BareMetaclassKind_MatchesUsage`
  - `Evaluate_QualifiedMetaclassKind_MatchesUsage`
  - `Evaluate_MetaclassKind_MatchesDefinition`
  - `Evaluate_MetaclassKind_NonMatchingMetaclass_DoesNotMatch`
  - `Evaluate_ClassificationTest_AppliedAnnotationMatchingUnaffectedByMetaclassKindAddition`
  - `Evaluate_MetaclassKind_SpecializationConformance_MatchesAncestorMetaclass`

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
- `Parse_DeeplyNestedParentheses_ReturnsDiagnosticInsteadOfCrashing` — 5000 levels of nested
  parentheses report a diagnostic instead of overflowing the native call stack
- `Parse_DeeplyNestedBracketIndexing_ReturnsDiagnosticInsteadOfCrashing` — 500 levels of nested
  sequence-indexing brackets (`a[a[a[...0...]]]`) report a diagnostic instead of overflowing the
  native call stack, closing the gap a follow-up review found in the initial paren-only guard
- `Parse_DeeplyNestedBodyExpressionBraces_ReturnsDiagnosticInsteadOfCrashing` — 500 levels of
  nested body-expression braces (`a.?{a.?{...0...}}`) report a diagnostic instead of overflowing
  the native call stack, closing a second follow-up gap in the paren/bracket-only guard
- `Parse_AstralPlaneUnicodeCharacter_NeverThrows_ReturnsDiagnostic` — an astral-plane Unicode
  character (surrogate pair) is reported as a diagnostic instead of throwing `ArgumentException`
- `Parse_TrailingGarbageAfterValidExpression_ReturnsDiagnostic` — a valid expression prefix
  followed by extra tokens is reported as a diagnostic instead of silently truncating
- `Parse_NumericLiteralOverflow_ReturnsDiagnosticInsteadOfInfinity` — a numeric literal that
  overflows `double` (`3.14e400`) is reported as a diagnostic instead of silently becoming
  `Infinity`
- `Evaluate_BareMetaclassKind_MatchesUsage` — bare `@PartUsage` matches a `part`-keyword usage via
  its own AST node kind, with no applied annotation present
- `Evaluate_QualifiedMetaclassKind_MatchesUsage` — `@SysML::PartUsage` matches identically to the
  bare spelling
- `Evaluate_MetaclassKind_MatchesDefinition` — metaclass-kind matching also applies to
  definition-level candidates, not just usages
- `Evaluate_MetaclassKind_NonMatchingMetaclass_DoesNotMatch` — a metaclass filter unrelated to the
  candidate's kind does not match
- `Evaluate_ClassificationTest_AppliedAnnotationMatchingUnaffectedByMetaclassKindAddition` —
  existing applied-annotation classification-test matching on a usage is unaffected by the new
  metaclass-kind OR-path
- `Evaluate_MetaclassKind_SpecializationConformance_MatchesAncestorMetaclass` — `@ConstraintUsage`
  matches a `requirement` usage via the stdlib's `RequirementUsage specializes ConstraintUsage`
  chain
