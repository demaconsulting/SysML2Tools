// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

// cspell:ignore Parenthesization istype Istype hastype Hastype Reparses

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
