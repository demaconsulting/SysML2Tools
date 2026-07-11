// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

// cspell:ignore Parenthesization istype Istype hastype Hastype Reparses LBRACK RBRACK uncatchable

using DemaConsulting.SysML2Tools.Filtering;

namespace DemaConsulting.SysML2Tools.Tests.Filtering;

/// <summary>
///     Tests for <see cref="FilterExpressionParser"/>.
/// </summary>
public sealed class FilterExpressionParserTests
{
    /// <summary>A bare classification test parses into a <see cref="ClassificationTestExpression"/>.</summary>
    [Fact]
    public void Parse_ClassificationTest_ReturnsClassificationTestExpression()
    {
        var result = FilterExpressionParser.Parse("@Safety");

        Assert.Empty(result.Diagnostics);
        var expression = Assert.IsType<ClassificationTestExpression>(result.Expression);
        Assert.Equal("Safety", expression.TypeName);
    }

    /// <summary>A qualified classification test preserves the fully-qualified type name text.</summary>
    [Fact]
    public void Parse_QualifiedClassificationTest_PreservesQualifiedName()
    {
        var result = FilterExpressionParser.Parse("@Pkg::Safety");

        var expression = Assert.IsType<ClassificationTestExpression>(result.Expression);
        Assert.Equal("Pkg::Safety", expression.TypeName);
    }

    /// <summary>An <c>and</c> connective builds a <see cref="BooleanFilterExpression"/> with both operands.</summary>
    [Fact]
    public void Parse_AndConnective_ReturnsBooleanExpression()
    {
        var result = FilterExpressionParser.Parse("@Safety and @Critical");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.And, expression.Connective);
        Assert.Equal("and", expression.OperatorText);
        Assert.IsType<ClassificationTestExpression>(expression.Left);
        Assert.IsType<ClassificationTestExpression>(expression.Right);
    }

    /// <summary>An <c>or</c> connective builds a <see cref="BooleanFilterExpression"/>.</summary>
    [Fact]
    public void Parse_OrConnective_ReturnsBooleanExpression()
    {
        var result = FilterExpressionParser.Parse("@Safety or @Critical");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.Or, expression.Connective);
    }

    /// <summary>An <c>xor</c> connective builds a <see cref="BooleanFilterExpression"/>.</summary>
    [Fact]
    public void Parse_XorConnective_ReturnsBooleanExpression()
    {
        var result = FilterExpressionParser.Parse("@Safety xor @Critical");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.Xor, expression.Connective);
    }

    /// <summary>The <c>&amp;</c> symbol spelling maps to the And connective, preserving its source spelling.</summary>
    [Fact]
    public void Parse_AmpSymbol_ReturnsAndWithSymbolSpelling()
    {
        var result = FilterExpressionParser.Parse("@Safety & @Critical");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.And, expression.Connective);
        Assert.Equal("&", expression.OperatorText);
    }

    /// <summary>The <c>|</c> symbol spelling maps to the Or connective, preserving its source spelling.</summary>
    [Fact]
    public void Parse_PipeSymbol_ReturnsOrWithSymbolSpelling()
    {
        var result = FilterExpressionParser.Parse("@Safety | @Critical");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.Or, expression.Connective);
        Assert.Equal("|", expression.OperatorText);
    }

    /// <summary><c>not</c> builds a <see cref="NotFilterExpression"/>.</summary>
    [Fact]
    public void Parse_Not_ReturnsNotExpression()
    {
        var result = FilterExpressionParser.Parse("not @Safety");

        var expression = Assert.IsType<NotFilterExpression>(result.Expression);
        Assert.IsType<ClassificationTestExpression>(expression.Operand);
    }

    /// <summary>Parenthesization groups sub-expressions without altering their meaning.</summary>
    [Fact]
    public void Parse_Parenthesized_ReturnsInnerExpression()
    {
        var result = FilterExpressionParser.Parse("(@Safety and @Critical) or @Other");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.Or, expression.Connective);
        Assert.IsType<BooleanFilterExpression>(expression.Left);
    }

    /// <summary>A bare <c>(as Type).attribute</c> read builds an <see cref="AttributeReadExpression"/>.</summary>
    [Fact]
    public void Parse_AttributeRead_ReturnsAttributeReadExpression()
    {
        var result = FilterExpressionParser.Parse("(as Safety).isMandatory");

        var expression = Assert.IsType<AttributeReadExpression>(result.Expression);
        Assert.Equal("Safety", expression.TypeName);
        Assert.Equal("isMandatory", expression.AttributeName);
    }

    /// <summary>An attribute read compared with <c>==</c> against a boolean literal builds a comparison.</summary>
    [Fact]
    public void Parse_AttributeReadEqualsBoolean_ReturnsComparisonExpression()
    {
        var result = FilterExpressionParser.Parse("(as Safety).isMandatory == true");

        var expression = Assert.IsType<ComparisonFilterExpression>(result.Expression);
        Assert.Equal(ComparisonOperator.Equal, expression.Operator);
        Assert.Equal(FilterLiteralKind.Boolean, expression.Right.Kind);
        Assert.True(expression.Right.BooleanValue);
    }

    /// <summary>
    ///     The canonical OMG "Filtering" idiom <c>@Safety and (as Safety).isMandatory</c> parses
    ///     as the intuitively-expected <c>AND(classification-test, attribute-read)</c> tree, not
    ///     the literal CST shape the grammar produces (a DOT node wrapping the whole AND chain —
    ///     see <c>FilterExpressionParser.BuildAttributeReadOnto</c>'s remarks for why DOT binds
    ///     looser than the boolean connectives in this grammar and how the parser re-associates
    ///     the attribute read onto the chain's rightmost operand).
    /// </summary>
    [Fact]
    public void Parse_ClassificationTestAndAttributeRead_ReAssociatesDotOntoRightOperand()
    {
        var result = FilterExpressionParser.Parse("@Safety and (as Safety).isMandatory");

        var expression = Assert.IsType<BooleanFilterExpression>(result.Expression);
        Assert.Equal(BooleanConnective.And, expression.Connective);
        var left = Assert.IsType<ClassificationTestExpression>(expression.Left);
        Assert.Equal("Safety", left.TypeName);
        var right = Assert.IsType<AttributeReadExpression>(expression.Right);
        Assert.Equal("Safety", right.TypeName);
        Assert.Equal("isMandatory", right.AttributeName);
    }

    /// <summary>An attribute read compared with <c>!=</c> against a string literal builds a comparison.</summary>
    [Fact]
    public void Parse_AttributeReadNotEqualsString_ReturnsComparisonExpression()
    {
        var result = FilterExpressionParser.Parse("(as Safety).level != \"low\"");

        var expression = Assert.IsType<ComparisonFilterExpression>(result.Expression);
        Assert.Equal(ComparisonOperator.NotEqual, expression.Operator);
        Assert.Equal(FilterLiteralKind.String, expression.Right.Kind);
        Assert.Equal("low", expression.Right.StringValue);
    }

    /// <summary>An attribute read compared with a number literal builds a comparison.</summary>
    [Fact]
    public void Parse_AttributeReadEqualsNumber_ReturnsComparisonExpression()
    {
        var result = FilterExpressionParser.Parse("(as Safety).level == 4");

        var expression = Assert.IsType<ComparisonFilterExpression>(result.Expression);
        Assert.Equal(FilterLiteralKind.Number, expression.Right.Kind);
        Assert.Equal(4, expression.Right.NumberValue);
    }

    /// <summary><c>istype</c> is outside the Phase 1 subset and produces an "unsupported construct" diagnostic.</summary>
    [Fact]
    public void Parse_Istype_ReturnsUnsupportedConstructDiagnostic()
    {
        var result = FilterExpressionParser.Parse("x istype Safety");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
    }

    /// <summary><c>hastype</c> is outside the Phase 1 subset and produces an "unsupported construct" diagnostic.</summary>
    [Fact]
    public void Parse_Hastype_ReturnsUnsupportedConstructDiagnostic()
    {
        var result = FilterExpressionParser.Parse("x hastype Safety");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
    }

    /// <summary><c>all</c> is outside the Phase 1 subset and produces an "unsupported construct" diagnostic.</summary>
    [Fact]
    public void Parse_All_ReturnsUnsupportedConstructDiagnostic()
    {
        var result = FilterExpressionParser.Parse("all Safety");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
    }

    /// <summary>Arithmetic is outside the Phase 1 subset and produces an "unsupported construct" diagnostic.</summary>
    [Fact]
    public void Parse_Arithmetic_ReturnsUnsupportedConstructDiagnostic()
    {
        var result = FilterExpressionParser.Parse("1 + 2");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
    }

    /// <summary>A conditional (<c>if</c>) expression is outside the Phase 1 subset.</summary>
    [Fact]
    public void Parse_Conditional_ReturnsUnsupportedConstructDiagnostic()
    {
        var result = FilterExpressionParser.Parse("if @Safety ? true else false");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
    }

    /// <summary>General feature-chain navigation (a plain member access, no cast) is unsupported in Phase 1.</summary>
    [Fact]
    public void Parse_GeneralFeatureChainNavigation_ReturnsUnsupportedConstructDiagnostic()
    {
        var result = FilterExpressionParser.Parse("someFeature.someAttribute");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
    }

    /// <summary>Malformed syntax never throws and reports a syntax-error diagnostic.</summary>
    [Fact]
    public void Parse_MalformedSyntax_NeverThrows_ReturnsDiagnostic()
    {
        var result = FilterExpressionParser.Parse("@Safety and and");

        Assert.Null(result.Expression);
        Assert.NotEmpty(result.Diagnostics);
    }

    /// <summary>
    ///     Deeply nested parenthesization (thousands of levels) must not overflow the native call
    ///     stack via ANTLR's recursive-descent parse of <c>ownedExpression</c>/<c>baseExpression</c>
    ///     (a <see cref="StackOverflowException"/> cannot be caught in .NET and would crash the
    ///     whole process instead). Reproduces the exact input shape from the retroactive Filtering
    ///     code-review report: 5000 levels of <c>(</c> around <c>@Foo</c>, followed by 5000 levels
    ///     of <c>)</c>.
    /// </summary>
    [Fact]
    public void Parse_DeeplyNestedParentheses_ReturnsDiagnosticInsteadOfCrashing()
    {
        var deeplyNested = new string('(', 5000) + "@Foo" + new string(')', 5000);

        var result = FilterExpressionParser.Parse(deeplyNested);

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("too deeply nested"));
    }

    /// <summary>
    ///     Reasonable (non-adversarial) parenthesization nesting still parses successfully — the
    ///     depth guard must not reject ordinary Phase 1 filter expressions.
    /// </summary>
    [Fact]
    public void Parse_ModeratelyNestedParentheses_StillParsesSuccessfully()
    {
        var moderatelyNested = new string('(', 20) + "@Foo" + new string(')', 20);

        var result = FilterExpressionParser.Parse(moderatelyNested);

        Assert.Empty(result.Diagnostics);
        var expression = Assert.IsType<ClassificationTestExpression>(result.Expression);
        Assert.Equal("Foo", expression.TypeName);
    }

    /// <summary>
    ///     Deeply nested sequence-indexing brackets must not overflow the native call stack either
    ///     — a follow-up review found that the deep-nesting guard above only tracked <c>(</c>/<c>)</c>
    ///     and prefix unary operators, leaving the grammar's <c>ownedExpression LBRACK
    ///     sequenceExpressionList? RBRACK</c> indexing production (which recurses back into
    ///     <c>ownedExpression</c> for its bracketed contents exactly like parenthesization does)
    ///     free to still crash the process via the same uncatchable <see cref="StackOverflowException"/>
    ///     failure mode, at a nesting depth (500 levels, ~1501 characters) far below the guard's own
    ///     200-level ceiling. Reproduces the exact repro construction from the follow-up code-review
    ///     report: <c>"a[" * 500 + "0" + "]" * 500</c>.
    /// </summary>
    [Fact]
    public void Parse_DeeplyNestedBracketIndexing_ReturnsDiagnosticInsteadOfCrashing()
    {
        var deeplyNested = "a" + string.Concat(Enumerable.Repeat("[a", 500)) + "0" + string.Concat(Enumerable.Repeat("]", 500));

        var result = FilterExpressionParser.Parse(deeplyNested);

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("too deeply nested"));
    }

    /// <summary>
    ///     Shallow (non-adversarial) sequence-indexing bracket nesting must not be rejected by the
    ///     depth guard. Bracket indexing is outside the Phase 1 construct subset, so it is still
    ///     expected to produce an "unsupported construct" diagnostic — the point of this test is
    ///     that it is *that* diagnostic, and not the "too deeply nested" one, confirming the guard
    ///     itself introduces no false-positive rejection for ordinary-depth bracket expressions.
    /// </summary>
    [Fact]
    public void Parse_ShallowBracketIndexing_ReturnsUnsupportedConstructNotDeepNestingDiagnostic()
    {
        var shallowNested = "a" + string.Concat(Enumerable.Repeat("[a", 5)) + "0" + string.Concat(Enumerable.Repeat("]", 5));

        var result = FilterExpressionParser.Parse(shallowNested);

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("too deeply nested"));
    }

    /// <summary>
    ///     Deeply nested body-expression braces must not overflow the native call stack either —
    ///     a second follow-up review found that the deep-nesting guard still did not track
    ///     <c>{</c>/<c>}</c>, leaving the grammar's <c>bodyExpression : LBRACE functionBodyPart
    ///     RBRACE</c> production (reachable via <c>ownedExpression DOT_QUESTION bodyExpression</c>,
    ///     which recurses back into <c>ownedExpression</c> for its brace-enclosed contents exactly
    ///     like parenthesization and bracket indexing do) free to still crash the process via the
    ///     same uncatchable <see cref="StackOverflowException"/> failure mode, at a nesting depth
    ///     (500 levels) far below the guard's own 200-level ceiling. Reproduces the exact repro
    ///     construction from the second follow-up code-review report:
    ///     <c>"a.?{" * 500 + "0" + "}" * 500</c>.
    /// </summary>
    [Fact]
    public void Parse_DeeplyNestedBodyExpressionBraces_ReturnsDiagnosticInsteadOfCrashing()
    {
        var deeplyNested = string.Concat(Enumerable.Repeat("a.?{", 500)) + "0" + string.Concat(Enumerable.Repeat("}", 500));

        var result = FilterExpressionParser.Parse(deeplyNested);

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("too deeply nested"));
    }

    /// <summary>
    ///     Shallow (non-adversarial) body-expression brace nesting must not be rejected by the
    ///     depth guard. Body expressions are outside the Phase 1 construct subset, so this is
    ///     still expected to produce an "unsupported construct" diagnostic — the point of this
    ///     test is that it is *that* diagnostic, and not the "too deeply nested" one, confirming
    ///     the guard itself introduces no false-positive rejection for ordinary-depth
    ///     body-expression nesting.
    /// </summary>
    [Fact]
    public void Parse_ShallowBodyExpressionBraces_ReturnsUnsupportedConstructNotDeepNestingDiagnostic()
    {
        var shallowNested = string.Concat(Enumerable.Repeat("a.?{", 5)) + "0" + string.Concat(Enumerable.Repeat("}", 5));

        var result = FilterExpressionParser.Parse(shallowNested);

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unsupported filter construct"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("too deeply nested"));
    }

    /// <summary>
    ///     Filter text containing an astral-plane Unicode character (a valid UTF-16 surrogate
    ///     pair, e.g. an emoji) must never throw — ANTLR's own <c>Lexer.GetErrorDisplay</c> throws
    ///     an uncaught <see cref="ArgumentException"/> (via <c>Char.ConvertToUtf32</c>) when
    ///     formatting the lexer error for such input, which previously propagated straight out of
    ///     <see cref="FilterExpressionParser.Parse"/> since only <see cref="Antlr4.Runtime.RecognitionException"/>
    ///     was caught. Reproduces the exact repro input from the retroactive Filtering code-review
    ///     report.
    /// </summary>
    [Fact]
    public void Parse_AstralPlaneUnicodeCharacter_NeverThrows_ReturnsDiagnostic()
    {
        var result = FilterExpressionParser.Parse("@\U0001F600Type");

        Assert.Null(result.Expression);
        Assert.NotEmpty(result.Diagnostics);
    }

    /// <summary>
    ///     A second astral-plane Unicode reproduction shape from the report: the surrogate pair
    ///     appears as a trailing token rather than embedded in an identifier.
    /// </summary>
    [Fact]
    public void Parse_AstralPlaneUnicodeCharacterAsTrailingToken_NeverThrows_ReturnsDiagnostic()
    {
        var result = FilterExpressionParser.Parse("@Foo and \U0001F600");

        Assert.Null(result.Expression);
        Assert.NotEmpty(result.Diagnostics);
    }

    /// <summary>
    ///     Trailing garbage after a syntactically valid expression prefix must be reported as a
    ///     diagnostic rather than silently discarded — previously <c>parser.ownedExpression()</c>
    ///     returned the recognized prefix with zero diagnostics, discarding
    ///     <c>"extra garbage tokens"</c> with no indication anything was wrong. Reproduces the
    ///     exact repro input from the retroactive Filtering code-review report.
    /// </summary>
    [Fact]
    public void Parse_TrailingGarbageAfterValidExpression_ReturnsDiagnostic()
    {
        var result = FilterExpressionParser.Parse("@Foo and @Bar extra garbage tokens");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("trailing content"));
    }

    /// <summary>A stray trailing close-paren after a valid expression is also reported as trailing content.</summary>
    [Fact]
    public void Parse_TrailingCloseParen_ReturnsDiagnostic()
    {
        var result = FilterExpressionParser.Parse("@Foo )");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("trailing content"));
    }

    /// <summary>A stray trailing semicolon after a valid expression is also reported as trailing content.</summary>
    [Fact]
    public void Parse_TrailingSemicolon_ReturnsDiagnostic()
    {
        var result = FilterExpressionParser.Parse("@Foo;");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("trailing content"));
    }

    /// <summary>
    ///     A numeric literal whose magnitude overflows <see cref="double"/> during parsing (e.g.
    ///     <c>3.14e400</c>, which <c>double.TryParse</c> silently accepts as
    ///     <see cref="double.PositiveInfinity"/>) must be reported as an invalid literal rather
    ///     than silently producing a value whose pretty-printed form (<c>"Infinity"</c>) is not
    ///     valid SysML v2 numeric syntax and therefore cannot round-trip. Reproduces the exact
    ///     repro input from the retroactive Filtering code-review report.
    /// </summary>
    [Fact]
    public void Parse_NumericLiteralOverflow_ReturnsDiagnosticInsteadOfInfinity()
    {
        var result = FilterExpressionParser.Parse("(as X).y != 3.14e400");

        Assert.Null(result.Expression);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("out of range"));
    }

    /// <summary>An ordinary large-exponent real literal (well within <see cref="double"/>'s range) still parses.</summary>
    [Fact]
    public void Parse_LargeButFiniteRealLiteral_StillParsesSuccessfully()
    {
        var result = FilterExpressionParser.Parse("(as X).y != 3.14e10");

        Assert.Empty(result.Diagnostics);
        var expression = Assert.IsType<ComparisonFilterExpression>(result.Expression);
        Assert.Equal(FilterLiteralKind.Number, expression.Right.Kind);
        Assert.Equal(3.14e10, expression.Right.NumberValue);
    }

    /// <summary>
    ///     Round-trip: pretty-printing a parsed expression and re-parsing the printed text yields a
    ///     semantically-equivalent tree, for every Phase 1 construct.
    /// </summary>
    [Theory]
    [InlineData("@Safety")]
    [InlineData("@Pkg::Safety")]
    [InlineData("@Safety and @Critical")]
    [InlineData("@Safety or @Critical")]
    [InlineData("@Safety xor @Critical")]
    [InlineData("not @Safety")]
    [InlineData("(as Safety).isMandatory")]
    [InlineData("(as Safety).isMandatory == true")]
    [InlineData("(as Safety).level != \"low\"")]
    [InlineData("(as Safety).level == 4")]
    [InlineData("@Safety and @Critical or not @Other")]
    [InlineData("@Safety and (as Safety).isMandatory")]
    public void Parse_RoundTrip_PrettyPrintedTextReparsesToEquivalentTree(string expressionText)
    {
        var first = FilterExpressionParser.Parse(expressionText);
        Assert.NotNull(first.Expression);

        var printed = first.Expression!.ToString();
        var second = FilterExpressionParser.Parse(printed!);

        Assert.NotNull(second.Expression);
        Assert.Equal(first.Expression, second.Expression);
    }
}
