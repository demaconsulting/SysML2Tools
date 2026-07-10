// <copyright file="FilterExpression.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

// cspell:ignore parenthesization istype hastype

namespace DemaConsulting.SysML2Tools.Filtering;

/// <summary>
/// Base type for a view <c>filter [&lt;expr&gt;];</c> expression's abstract syntax tree, covering
/// exactly the Phase 1 construct subset: classification-test atoms (<c>@Type</c>, <c>@Pkg::Type</c>),
/// boolean connectives (<c>and</c>, <c>or</c>, <c>not</c>, <c>xor</c>, <c>|</c>, <c>&amp;</c>),
/// parenthesization, and <c>(as Type).attribute</c> reads (bare, or compared with <c>==</c>/<c>!=</c>
/// against a scalar literal). Everything else (<c>istype</c>/<c>hastype</c>/<c>all</c>/arithmetic/
/// conditional/general feature-chain navigation) is unsupported in Phase 1 and never produces an
/// instance of this type — <see cref="FilterExpressionParser"/> reports it as a diagnostic instead.
/// </summary>
/// <remarks>
/// Every subtype implements <see cref="ToString"/> as a canonical pretty-printer that produces valid
/// SysML v2 filter-expression syntax; re-parsing the printed text with
/// <see cref="FilterExpressionParser.Parse"/> yields a semantically-equivalent tree (the round-trip
/// requirement — see <c>docs/design/sysml2-tools-core/filtering.md</c>).
/// </remarks>
public abstract record FilterExpression
{
    /// <summary>
    /// Wraps <paramref name="expression"/>'s pretty-printed text in parentheses when it is a
    /// compound expression (boolean connective or comparison) whose precedence could otherwise be
    /// misread once embedded as an operand of another expression; atoms (classification test,
    /// attribute read, literal) and already-unary <see cref="NotFilterExpression"/> never need it.
    /// </summary>
    private protected static string Parenthesize(FilterExpression expression) =>
        expression is BooleanFilterExpression or ComparisonFilterExpression
            ? $"({expression})"
            : expression.ToString() ?? string.Empty;
}

/// <summary>
/// A classification-test atom (<c>@Type</c> / <c>@Pkg::Type</c>): true when the candidate element
/// carries a resolved metadata annotation of the referenced type.
/// </summary>
/// <param name="TypeName">The raw (possibly qualified) metadata type reference text.</param>
public sealed record ClassificationTestExpression(string TypeName) : FilterExpression
{
    /// <inheritdoc/>
    public override string ToString() => $"@{TypeName}";
}

/// <summary>
/// The boolean connective a <see cref="BooleanFilterExpression"/> node applies.
/// </summary>
public enum BooleanConnective
{
    /// <summary>Logical conjunction (<c>and</c> / <c>&amp;</c>).</summary>
    And,

    /// <summary>Logical disjunction (<c>or</c> / <c>|</c>).</summary>
    Or,

    /// <summary>Logical exclusive-or (<c>xor</c>).</summary>
    Xor,
}

/// <summary>
/// A binary boolean connective expression (<c>and</c>/<c>or</c>/<c>xor</c>/<c>|</c>/<c>&amp;</c>).
/// </summary>
/// <param name="Connective">Which boolean connective this node applies.</param>
/// <param name="OperatorText">
/// The exact source spelling of the operator (<c>"and"</c>, <c>"&amp;"</c>, <c>"or"</c>,
/// <c>"|"</c>, or <c>"xor"</c>) — preserved so the pretty-printer reproduces the author's chosen
/// spelling rather than normalizing <c>&amp;</c> to <c>and</c> or <c>|</c> to <c>or</c>.
/// </param>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand.</param>
public sealed record BooleanFilterExpression(
    BooleanConnective Connective,
    string OperatorText,
    FilterExpression Left,
    FilterExpression Right) : FilterExpression
{
    /// <inheritdoc/>
    public override string ToString() => $"{Parenthesize(Left)} {OperatorText} {Parenthesize(Right)}";
}

/// <summary>
/// A unary boolean negation (<c>not X</c>).
/// </summary>
/// <param name="Operand">The negated expression.</param>
public sealed record NotFilterExpression(FilterExpression Operand) : FilterExpression
{
    /// <inheritdoc/>
    public override string ToString() => $"not {Parenthesize(Operand)}";
}

/// <summary>
/// An <c>(as Type).attribute</c> read: evaluates the named literal attribute value captured on the
/// candidate element's <c>Type</c> metadata annotation (see
/// <c>DemaConsulting.SysML2Tools.Semantic.Model.SysmlMetadataNode</c>), or is absent when the
/// candidate carries no such annotation or attribute.
/// </summary>
/// <param name="TypeName">The raw (possibly qualified) metadata type reference text.</param>
/// <param name="AttributeName">The attribute's simple name.</param>
public sealed record AttributeReadExpression(string TypeName, string AttributeName) : FilterExpression
{
    /// <inheritdoc/>
    public override string ToString() => $"(as {TypeName}).{AttributeName}";
}

/// <summary>
/// The kind of scalar literal a <see cref="LiteralFilterExpression"/> carries.
/// </summary>
public enum FilterLiteralKind
{
    /// <summary>A boolean literal (<c>true</c>/<c>false</c>).</summary>
    Boolean,

    /// <summary>A numeric literal (integer or real).</summary>
    Number,

    /// <summary>A double-quoted string literal.</summary>
    String,
}

/// <summary>
/// A scalar literal value (boolean, number, or string) used as the right-hand side of a
/// <see cref="ComparisonFilterExpression"/>.
/// </summary>
/// <param name="Kind">Which kind of literal this node holds.</param>
/// <param name="BooleanValue">The boolean value when <paramref name="Kind"/> is <see cref="FilterLiteralKind.Boolean"/>.</param>
/// <param name="NumberValue">The numeric value when <paramref name="Kind"/> is <see cref="FilterLiteralKind.Number"/>.</param>
/// <param name="StringValue">The (unquoted) string value when <paramref name="Kind"/> is <see cref="FilterLiteralKind.String"/>.</param>
public sealed record LiteralFilterExpression(
    FilterLiteralKind Kind,
    bool? BooleanValue = null,
    double? NumberValue = null,
    string? StringValue = null) : FilterExpression
{
    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        FilterLiteralKind.Boolean => (BooleanValue ?? false) ? "true" : "false",
        FilterLiteralKind.Number => (NumberValue ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
        FilterLiteralKind.String => $"\"{StringValue}\"",
        _ => string.Empty,
    };
}

/// <summary>
/// The comparison operator a <see cref="ComparisonFilterExpression"/> applies.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Equality (<c>==</c>).</summary>
    Equal,

    /// <summary>Inequality (<c>!=</c>).</summary>
    NotEqual,
}

/// <summary>
/// A comparison of an <see cref="AttributeReadExpression"/> against a scalar
/// <see cref="LiteralFilterExpression"/> (e.g. <c>(as Safety).isMandatory == true</c>).
/// </summary>
/// <param name="Left">The attribute read being compared.</param>
/// <param name="Operator">Which comparison operator applies.</param>
/// <param name="Right">The literal being compared against.</param>
public sealed record ComparisonFilterExpression(
    AttributeReadExpression Left,
    ComparisonOperator Operator,
    LiteralFilterExpression Right) : FilterExpression
{
    /// <inheritdoc/>
    public override string ToString() =>
        $"{Left} {(Operator == ComparisonOperator.Equal ? "==" : "!=")} {Right}";
}
