// <copyright file="FilterExpressionEvaluator.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Filtering;

/// <summary>
/// The outcome of <see cref="FilterExpressionEvaluator.Evaluate(SysmlWorkspace, IReadOnlyList{string}, FilterExpression)"/>.
/// </summary>
/// <param name="MatchedQualifiedNames">
/// The subset of the candidate qualified names for which <c>expression</c> evaluated to
/// <see langword="true"/>.
/// </param>
/// <param name="Diagnostics">Diagnostics produced while evaluating (always empty — evaluation of an
/// already-parsed Phase 1 expression never fails; kept for symmetry with
/// <see cref="FilterExpressionParser.Parse"/> and to leave room for future evaluation-time
/// diagnostics without a breaking API change).</param>
public sealed record FilterEvaluationResult(
    IReadOnlyList<string> MatchedQualifiedNames,
    IReadOnlyList<SysmlDiagnostic> Diagnostics);

/// <summary>
/// Evaluates a parsed <see cref="FilterExpression"/> (see <see cref="FilterExpressionParser"/>)
/// against a set of candidate elements, narrowing them to those the expression's boolean predicate
/// matches. Never throws.
/// </summary>
/// <remarks>
/// Classification-test atoms (<c>@Type</c>) and <c>(as Type).attribute</c> reads are evaluated
/// against each candidate's directly-owned <c>SysmlMetadataNode</c> children (see
/// <c>AstBuilder</c>'s <c>metadataFeature</c> capture): a metadata annotation matches when its
/// resolved <see cref="SysmlEdgeKind.MetadataType"/> target's qualified name equals the filter's
/// type reference (exact match), or when it ends with <c>"::" + TypeName</c> (a bare simple-name
/// reference resolving to a qualified metadata type), or — when the annotation's type reference
/// never resolved (see <see cref="ReferenceResolver"/>'s "Unresolved reference" diagnostic) — when
/// its raw <see cref="SysmlMetadataNode.TypeReference"/> text equals the filter's type reference
/// verbatim (a graceful fallback so an otherwise-valid filter still works against a metadata
/// annotation whose defining package failed to resolve for an unrelated reason).
/// An attribute read whose metadata annotation is absent, or whose named attribute was not
/// assigned a literal value, evaluates to "absent": a bare read is treated as
/// <see langword="false"/>, and any comparison against an absent read is treated as
/// <see langword="false"/> regardless of operator — a conservative, documented Phase 1 limitation
/// (see <c>docs/design/sysml2-tools-core/filtering/filter-expression-evaluator.md</c>).
/// </remarks>
public static class FilterExpressionEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="expression"/> against each candidate in
    /// <paramref name="candidateQualifiedNames"/>, returning the subset that matches.
    /// </summary>
    /// <param name="workspace">The workspace to resolve candidate declarations from.</param>
    /// <param name="candidateQualifiedNames">The qualified names of the candidate elements to test.</param>
    /// <param name="expression">The parsed filter expression to evaluate.</param>
    /// <returns>The evaluation result (matched subset + diagnostics).</returns>
    public static FilterEvaluationResult Evaluate(
        SysmlWorkspace workspace,
        IReadOnlyList<string> candidateQualifiedNames,
        FilterExpression expression)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(candidateQualifiedNames);
        ArgumentNullException.ThrowIfNull(expression);

        var matched = new List<string>();
        foreach (var qualifiedName in candidateQualifiedNames)
        {
            if (workspace.Declarations.TryGetValue(qualifiedName, out var node) &&
                Evaluate(node, expression))
            {
                matched.Add(qualifiedName);
            }
        }

        return new FilterEvaluationResult(matched, Array.Empty<SysmlDiagnostic>());
    }

    /// <summary>Evaluates <paramref name="expression"/>'s boolean value against a single candidate node.</summary>
    private static bool Evaluate(SysmlNode node, FilterExpression expression) =>
        expression switch
        {
            ClassificationTestExpression classificationTest => FindMetadata(node, classificationTest.TypeName) is not null,
            NotFilterExpression not => !Evaluate(node, not.Operand),
            BooleanFilterExpression boolean => boolean.Connective switch
            {
                BooleanConnective.And => Evaluate(node, boolean.Left) && Evaluate(node, boolean.Right),
                BooleanConnective.Or => Evaluate(node, boolean.Left) || Evaluate(node, boolean.Right),
                BooleanConnective.Xor => Evaluate(node, boolean.Left) ^ Evaluate(node, boolean.Right),
                _ => false,
            },
            AttributeReadExpression attributeRead => ReadAttribute(node, attributeRead) is { Kind: MetadataAttributeValueKind.Boolean } value
                && (value.BooleanValue ?? false),
            ComparisonFilterExpression comparison => EvaluateComparison(node, comparison),
            _ => false,
        };

    /// <summary>Evaluates a comparison expression: absent attribute reads always evaluate to false.</summary>
    private static bool EvaluateComparison(SysmlNode node, ComparisonFilterExpression comparison)
    {
        var value = ReadAttribute(node, comparison.Left);
        if (value is null)
        {
            return false;
        }

        var equal = ValuesEqual(value, comparison.Right);
        return comparison.Operator == ComparisonOperator.Equal ? equal : !equal;
    }

    /// <summary>Compares a captured literal attribute value against a filter-expression literal.</summary>
    private static bool ValuesEqual(MetadataAttributeValue value, LiteralFilterExpression literal) =>
        (value.Kind, literal.Kind) switch
        {
            (MetadataAttributeValueKind.Boolean, FilterLiteralKind.Boolean) => value.BooleanValue == literal.BooleanValue,
            (MetadataAttributeValueKind.Number, FilterLiteralKind.Number) => NumbersEqual(value.NumberValue, literal.NumberValue),
            (MetadataAttributeValueKind.String, FilterLiteralKind.String) => value.StringValue == literal.StringValue,
            _ => false,
        };

    /// <summary>
    /// Compares two nullable numeric values for equality using a small relative/absolute tolerance,
    /// avoiding a direct floating-point equality check (both literal integers and reals share the
    /// <see cref="MetadataAttributeValueKind.Number"/>/<see cref="FilterLiteralKind.Number"/>
    /// representation, so an exact-integer comparison like <c>4 == 4</c> must still succeed).
    /// </summary>
    private static bool NumbersEqual(double? left, double? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return Math.Abs(left.Value - right.Value) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(left.Value), Math.Abs(right.Value)));
    }

    /// <summary>Reads the named literal attribute value off the candidate's matching metadata annotation, or null when absent.</summary>
    private static MetadataAttributeValue? ReadAttribute(SysmlNode node, AttributeReadExpression attributeRead)
    {
        var metadata = FindMetadata(node, attributeRead.TypeName);
        return metadata?.Attributes.FirstOrDefault(a => a.Name == attributeRead.AttributeName);
    }

    /// <summary>
    /// Finds the first directly-owned <see cref="SysmlMetadataNode"/> child of <paramref name="node"/>
    /// whose annotating type matches <paramref name="typeName"/> (see class remarks for the exact
    /// matching rules), or <see langword="null"/> when none match.
    /// </summary>
    private static SysmlMetadataNode? FindMetadata(SysmlNode node, string typeName)
    {
        foreach (var child in node.Children)
        {
            if (child is not SysmlMetadataNode metadata)
            {
                continue;
            }

            var resolvedTarget = metadata.ResolvedEdges
                .FirstOrDefault(e => e.Kind == SysmlEdgeKind.MetadataType)
                ?.TargetQualifiedName;

            if (resolvedTarget is not null &&
                (resolvedTarget == typeName || resolvedTarget.EndsWith("::" + typeName, StringComparison.Ordinal)))
            {
                return metadata;
            }

            if (resolvedTarget is null && metadata.TypeReference == typeName)
            {
                return metadata;
            }
        }

        return null;
    }
}
