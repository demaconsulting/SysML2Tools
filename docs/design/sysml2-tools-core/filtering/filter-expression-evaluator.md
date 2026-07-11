<!-- cspell:ignore istype hastype parenthesization uncatchable -->

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

`Parse` guards against four hardening scenarios beyond ordinary syntax errors, all reported as
`SysmlDiagnostic` rather than by throwing or crashing (see Error Handling below for why each is
necessary):

1. It eagerly tokenizes the input (`CommonTokenStream.Fill()`, which only drives the iterative
   lexer and never recurses) and rejects input whose parenthesization/bracket-indexing/
   body-expression-brace/prefix-unary-operator nesting depth would exceed `MaxNestingDepth` (200)
   *before* invoking the recursive-descent parser, which has no depth guard of its own.
2. It catches `Exception` generically (in addition to `RecognitionException`) around the lex/parse
   call, converting any other unexpected ANTLR-internal failure into a diagnostic.
3. It checks the token stream is positioned at EOF after `ownedExpression()` returns, reporting an
   "unexpected trailing content" diagnostic when the parser only consumed a prefix of the input.
4. `TryBuildLiteral` rejects integer/real literals that parse to a non-finite `double` (overflow),
   reporting a "numeric literal out of range" diagnostic instead of producing an
   `Infinity`/`NaN`-valued literal that cannot round-trip through `ToString()`.

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

`Parse`'s "never throws" contract is non-negotiable: a planned future GUI will call `Parse`/
`Evaluate` live on every keystroke of a text-editing filter box, so any uncaught exception —
recognized syntax error or not — would be user-visible and disruptive, and an uncatchable process
crash would be far worse. A retroactive robustness review found four ways ANTLR's own
lexer/recursive-descent-parser internals could violate that contract despite `Parse`'s existing
`RecognitionException` handling, all now hardened against:

- **Uncatchable stack overflow on deep nesting**: ANTLR's recursive-descent
  `ownedExpression()`/`baseExpression()`/`bodyExpression()` parse recurses once per nesting
  level — each `(`, each `[` (the `ownedExpression LBRACK sequenceExpressionList? RBRACK`
  sequence-indexing production), each `{` (the `bodyExpression : LBRACE functionBodyPart RBRACE`
  production, reachable via `ownedExpression DOT_QUESTION bodyExpression` and directly from
  `baseExpression`), or each prefix unary operator such as `not` — with no depth guard. Every one
  of these three balanced-delimiter productions recurses back into `ownedExpression` for its
  enclosed contents exactly like parenthesization does; per `SysMLv2Parser.g4`'s
  `ownedExpression`/`baseExpression`/`bodyExpression`/`argumentList` productions, `(`/`)`, `[`/`]`,
  and `{`/`}` are the *complete* set of delimiter pairs that can drive this recursion — see
  `ExceedsMaxNestingDepth`'s remarks in source for the full derivation. Beyond roughly 4000-5000
  levels for parens (far fewer — around 500 — for bracket indexing and body-expression braces,
  since their recursion signature involves an extra `sequenceExpressionList()`/`functionBodyPart()`
  frame per level) this overflows the native call stack with a `StackOverflowException`, which —
  unlike every other .NET exception — cannot be caught and terminates the entire process
  immediately, not just the `Parse` call. `Parse` now pre-scans the already-lexed (non-recursive)
  token stream and rejects input whose simulated recursion depth — tracking `(`/`[`/`{` and prefix
  unary operators identically as "pending frame" pushes, popped by a matching `)`/`]`/`}` or by
  reaching the next atom — would exceed 200 levels, well before the recursive parser runs. Two
  follow-up reviews each found the guard missing one of the three delimiter pairs in turn
  (`[`/`]` first, then `{`/`}`); all three are now handled identically and the source comment
  explicitly records that these are the complete set, to catch a future grammar change that adds a
  fourth.
- **Uncaught `ArgumentException` on astral-plane Unicode input**: `Antlr4.Runtime.Lexer.GetErrorDisplay`
  calls `Char.ConvertToUtf32` while formatting a lexer error message, which throws `ArgumentException`
  (not `RecognitionException`) for input containing an unpaired UTF-16 surrogate — including a
  *paired* surrogate that the lexer itself cannot otherwise tokenize. `Parse`'s catch clause was
  broadened to `catch (Exception ex)` around the lex/parse call (a deliberate, documented generic
  catch, consistent with this repository's established pattern for boundary code that must never
  propagate an unexpected failure) so this and any other ANTLR-internal failure mode become a
  diagnostic instead of an unhandled exception.
- **Silent trailing-garbage acceptance**: `parser.ownedExpression()` only requires a syntactically
  valid expression *prefix*; it returns successfully without consuming any trailing tokens, and
  previously nothing checked for that. `Parse` now verifies the token stream is positioned at EOF
  after the parse and reports an "unexpected trailing content" diagnostic otherwise.
- **Non-lossless round-trip for numeric literal overflow**: `double.TryParse` silently accepts
  literal text whose magnitude overflows `double` (e.g. `3.14e400`), returning
  `double.PositiveInfinity` rather than failing. `LiteralFilterExpression.ToString()` would then
  print the non-SysML-syntax text `"Infinity"`, which fails to re-parse. `TryBuildLiteral` now
  checks `double.IsFinite` after parsing an integer/real literal and reports a "numeric literal out
  of range" diagnostic instead of building a non-finite-valued literal.

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
- (Phase 2a) `ExposeScopeResolver.ResolveExposedScope` parses each `ExposeMember`'s
  `BracketFilterExpressionText` with `FilterExpressionParser.Parse`, evaluates successful ASTs
  with `FilterExpressionEvaluator.Evaluate` against that entry's own containment-subtree
  candidate set, adds matches to the resolved `ExposedScope.ExplicitMembers`, and falls back to
  whole-subtree inclusion (`PrefixSubjects`) plus a recorded `BracketFilterFailure` on parse or
  evaluation failure. No change was required in this unit to support this second caller.
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
  `FilterExpressionParser.TryBuildLiteral`, `FilterExpressionEvaluator.Evaluate`,
  `FilterExpressionParser.ExceedsMaxNestingDepth` (deep-nesting guard), `FilterExpressionParser.Parse`'s
  broadened `catch (Exception)` (astral Unicode / other ANTLR-internal failures), and `Parse`'s
  post-parse EOF check (trailing-content diagnostic)
- `SysML2Tools-Core-Filtering-FilterExpressionEvaluator-RoundTripPrettyPrinting` —
  `FilterExpression.ToString()` overrides, `FilterExpressionParser.Parse`, and
  `FilterExpressionParser.TryBuildLiteral`'s `double.IsFinite` check (numeric-literal-overflow guard)
