<!-- cspell:ignore istype hastype -->

### FilterExpressionEvaluator

#### Purpose

`FilterExpressionEvaluator` implements the Phase 1 standalone view-filter capability end to end:
it defines the `FilterExpression` AST hierarchy, adapts the generated SysML parser's
`ownedExpression()` CST into that AST, and evaluates the result against a caller-supplied set of
semantic-model candidates. Keeping the AST, parser, and evaluator together in one documented unit
reflects the implementation's tight coupling: none of the three artifacts is independently useful.

#### Data Model

The unit exposes three public record families plus two result records:

| Type | Purpose |
| --- | --- |
| `FilterExpression` | Abstract base for every supported Phase 1 predicate |
| `ClassificationTestExpression` | `@Type` / `@Pkg::Type` metadata-presence predicate |
| `BooleanFilterExpression` / `NotFilterExpression` | Binary and unary boolean composition |
| `AttributeReadExpression` | `(as Type).attribute` metadata-attribute read |
| `LiteralFilterExpression` | Scalar Boolean/Number/String literal |
| `ComparisonFilterExpression` | `==` / `!=` comparison of an attribute read against a literal |
| `FilterParseResult` | Parser output: AST or diagnostics |
| `FilterEvaluationResult` | Evaluator output: matched candidate names plus diagnostics |

Every `FilterExpression` subtype overrides `ToString()` as a canonical pretty-printer. The helper
`FilterExpression.Parenthesize` inserts parentheses only where a compound child could otherwise be
misread when embedded into another expression.

#### Key Methods

##### `FilterExpressionParser.Parse(string expressionText)`

Creates an ANTLR lexer/parser over the raw filter fragment and invokes `SysMLv2Parser.ownedExpression()`.
A custom `CollectingErrorListener` captures syntax errors as `SysmlDiagnostic` entries targeting a
virtual file path (`[filter-expression]`) so parsing never writes to the console or throws on
malformed input. If ANTLR reports no syntax errors, `Parse` delegates to `TryBuild` to adapt the
CST into the Phase 1 AST.

##### `FilterExpressionParser.TryBuild(OwnedExpressionContext, diagnostics)`

Performs a shape-driven CST walk restricted to the supported subset:

- prefix classification tests become `ClassificationTestExpression`
- `and`/`or`/`xor`/`&`/`|` become `BooleanFilterExpression`
- `not` becomes `NotFilterExpression`
- `==`/`!=` become `ComparisonFilterExpression` when the left side is an attribute read and the
  right side is a scalar literal
- parenthesized base expressions recurse into their inner expression

Every other CST shape appends an "Unsupported filter construct" diagnostic and returns no AST.

##### `FilterExpressionParser.BuildAttributeReadOnto(left, attributeName, diagnostics)`

Builds an `AttributeReadExpression` from the grammar's DOT form. In this repository's
`ownedExpression` grammar, DOT binds looser than the boolean connectives, so the canonical SysML
idiom `@Safety and (as Safety).isMandatory` parses as `DOT(AND(@Safety, (as Safety)), isMandatory)`
rather than `AND(@Safety, DOT((as Safety), isMandatory))`. `BuildAttributeReadOnto` repairs that
shape by re-associating the attribute read onto the boolean chain's rightmost operand (or the
operand of a unary `not`), producing the intuitive AST the evaluator expects.

##### `FilterExpressionEvaluator.Evaluate(workspace, candidateQualifiedNames, expression)`

Iterates the supplied candidate names in order, resolves each to a `SysmlNode` in
`workspace.Declarations`, evaluates the AST against that node, and returns the subset whose result
is `true`. Missing candidates are skipped silently, preserving the evaluator's no-throw contract.

##### `FilterExpressionEvaluator.ReadAttribute(node, attributeRead)` / `FindMetadata(node, typeName)`

Locate the first directly-owned `SysmlMetadataNode` child whose resolved `MetadataType` edge (or,
when unresolved, raw `TypeReference`) matches the requested type name. `ReadAttribute` then returns
that annotation's first matching `MetadataAttributeValue` by simple attribute name.

##### `FilterExpressionEvaluator.EvaluateComparison(node, comparison)`

Reads the attribute value and compares it against the literal using `ValuesEqual`. Absent metadata
or absent attributes evaluate conservatively as false regardless of operator; there is no implicit
three-valued logic or default-value synthesis in Phase 1.

#### Error Handling

`FilterExpressionParser` never throws for unsupported constructs or malformed syntax. Syntax errors
arrive via `CollectingErrorListener`; unsupported constructs are diagnosed explicitly during the CST
adaptation pass. `FilterExpressionEvaluator` uses ordinary false/empty results for unknown
candidates, missing metadata, and missing attributes. Diagnostics in `FilterEvaluationResult` are
currently always empty because evaluation of an already-supported AST cannot fail, but the result
shape leaves room for future evaluation-time diagnostics without a breaking API change.

#### Dependencies

- `SysMLv2Lexer` / `SysMLv2Parser` (Language Parser subsystem) — reusable grammar implementation
- `SysmlDiagnostic` / `DiagnosticSeverity` (Language Parser subsystem) — parse diagnostics
- `SysmlWorkspace`, `SysmlNode`, `SysmlMetadataNode`, `MetadataAttributeValue`, and
  `SysmlEdgeKind.MetadataType` (Language Semantic subsystem) — evaluation inputs and metadata
  resolution evidence
- .NET base class library numeric parsing/comparison helpers — scalar literal handling

#### Callers

- `GeneralViewLayoutStrategy` parses `SysmlViewNode.FilterExpressionText` with
  `FilterExpressionParser.Parse`, evaluates successful ASTs with `FilterExpressionEvaluator.Evaluate`,
  narrows its candidate definition set to the matched subset, and falls back to an unfiltered
  render with a warning when parsing produces diagnostics.
- `FilterExpressionParserTests` and `FilterExpressionEvaluatorTests` exercise the parser,
  pretty-printer, and evaluator directly.

#### Requirements Traceability

- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-ClassificationTests` —
  `FilterExpressionParser.Parse`, `FilterExpressionEvaluator.FindMetadata`,
  `FilterExpressionEvaluator.Evaluate`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-BooleanConnectives` —
  `FilterExpressionParser.TryBuild`, `BooleanFilterExpression.ToString()`,
  `FilterExpressionEvaluator.Evaluate`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-AttributeReads` —
  `FilterExpressionParser.BuildAttributeReadOnto`,
  `FilterExpressionParser.BuildComparison`, `FilterExpressionEvaluator.ReadAttribute`,
  `FilterExpressionEvaluator.EvaluateComparison`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-UnsupportedConstructDiagnostics` —
  `CollectingErrorListener`, `FilterExpressionParser.Unsupported`,
  `FilterExpressionParser.TryBuildLiteral`, `FilterExpressionEvaluator.Evaluate`
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-RoundTripPrettyPrinting` —
  `FilterExpression.ToString()` overrides and `FilterExpressionParser.Parse`
