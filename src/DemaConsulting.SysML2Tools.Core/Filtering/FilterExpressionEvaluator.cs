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
/// <remarks>
/// A classification test (<c>@Type</c>/<c>@Pkg::Type</c>) also matches when the candidate's own
/// AST node kind — <see cref="SysmlDefinitionNode.DefinitionKeyword"/> (e.g. <c>"part def"</c>) or
/// <see cref="SysmlFeatureNode.FeatureKeyword"/> (e.g. <c>"part"</c>) — maps to the requested
/// built-in SysML metaclass name (Phase 2d, see <c>ROADMAP.md</c>'s "View
/// <c>filter [&lt;expr&gt;];</c> expression evaluation" section), via the
/// <see cref="MetaclassNames"/> keyword-to-metaclass-name lookup table. This is an
/// <em>additional</em> match path, evaluated alongside (not instead of) the existing applied-
/// annotation match above, since both are legitimate under the OMG <c>@</c> classification-test
/// semantics (metaclass membership <em>or</em> explicit domain metadata). A keyword with no known
/// stdlib metaclass mapping (see <see cref="MetaclassNames"/>'s remarks for the documented list of
/// known gaps) never matches via this path. When the candidate's own mapped metaclass does not
/// match the requested type name exactly, this also walks the mapped metaclass's stdlib
/// <c>specializes</c> chain (the raw, unresolved <see cref="SysmlNode.SupertypeNames"/> captured
/// for every stdlib <c>metadata def</c> declaration — <em>not</em>
/// <see cref="SysmlNode.ResolvedEdges"/>, which is never populated for stdlib-only nodes; see that
/// property's remarks — resolved here by a same-simple-name lookup, cycle-guarded) looking for a
/// matching ancestor metaclass — e.g. a candidate whose own kind maps to <c>RequirementUsage</c>
/// also matches <c>@ConstraintUsage</c>, since <c>RequirementUsage specializes ConstraintUsage</c>
/// in the stdlib.
/// </remarks>
public static class FilterExpressionEvaluator
{
    /// <summary>
    /// Maps a <see cref="SysmlDefinitionNode.DefinitionKeyword"/> or
    /// <see cref="SysmlFeatureNode.FeatureKeyword"/> to its corresponding built-in SysML metaclass's
    /// bare (simple) name, used to match a classification test (<c>@Type</c>) against a candidate's
    /// own AST node kind (Phase 2d). A candidate matches a classification test's type reference
    /// when the reference equals this bare name, or equals <c>"SysML::" + name</c> — the canonical
    /// spelling real-world <c>filter</c>/bracket-filter expressions use (e.g.
    /// <c>filter @SysML::PartUsage;</c>), mirroring how the stdlib's actual nested declaration
    /// package (<c>SysML::Systems::PartUsage</c>) is conventionally referred to by its outer
    /// <c>SysML::</c> namespace in corpus usage rather than its literal declaring package path.
    /// Covers every definition/feature keyword this project's <c>AstBuilder</c> models that has a
    /// corresponding stdlib metaclass declaration in <c>SysML.sysml</c> — verified by grepping
    /// every <c>metadata def \w+Usage|\w+Definition</c> declaration in the stdlib and cross-
    /// checking each keyword this project's <c>AstBuilder</c> assigns.
    /// </summary>
    /// <remarks>
    /// Known gaps (keywords this project models with no corresponding stdlib metaclass, or whose
    /// mapping was deliberately not guessed — see <c>ROADMAP.md</c> Phase 2d for the full
    /// rationale): <c>individual def</c> (no <c>IndividualDefinition</c> metaclass in stdlib); raw
    /// KerML classifier keywords (<c>datatype</c>/<c>class</c>/<c>struct</c>/<c>assoc</c>/
    /// <c>assoc struct</c>/<c>function</c>/<c>predicate</c>, captured via <c>BuildClassifierNode</c>,
    /// not a SysML <c>*Definition</c> metaclass); <c>subject</c>/<c>actor</c>/<c>stakeholder</c> (no
    /// distinct stdlib metaclass); bare <c>enum value</c> members; control-node keywords
    /// (<c>merge</c>/<c>decide</c>/<c>join</c>/<c>fork</c>, KerML control-node kinds not modeled as
    /// metadata definitions in <c>SysML.sysml</c>); <c>assume constraint</c>/<c>require constraint</c>
    /// (no distinct stdlib metaclass beyond the generic <c>ConstraintUsage</c> — deliberately not
    /// merged in, to avoid over-claiming semantics not evidenced in the stdlib); <c>entry</c>/
    /// <c>do</c>/<c>exit</c> (deliberately not mapped to <c>ActionUsage</c> despite plausibility,
    /// since these are captured as minimal, deliberately non-behavioral features, not full semantic
    /// <c>ActionUsage</c> instances). Also out of scope by construction: <see cref="SysmlConnectionNode"/>
    /// (<c>connection</c>/<c>allocation</c>/<c>binding</c>/<c>message</c> keywords — corresponding
    /// stdlib metaclasses exist, but this node kind is not enumerated in Phase 2d's scope) and
    /// <see cref="SysmlViewNode"/>/<see cref="SysmlViewpointNode"/> (<c>view def</c>/<c>view</c>/
    /// <c>viewpoint def</c>/<c>viewpoint</c> — these elements use dedicated node types, not
    /// <see cref="SysmlDefinitionNode"/>/<see cref="SysmlFeatureNode"/>).
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> MetaclassNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Definition keywords (SysmlDefinitionNode.DefinitionKeyword).
        ["part def"] = "PartDefinition",
        ["attribute def"] = "AttributeDefinition",
        ["item def"] = "ItemDefinition",
        ["port def"] = "PortDefinition",
        ["connection def"] = "ConnectionDefinition",
        ["allocation def"] = "AllocationDefinition",
        ["flow def"] = "FlowDefinition",
        ["occurrence def"] = "OccurrenceDefinition",
        ["rendering def"] = "RenderingDefinition",
        ["metadata def"] = "MetadataDefinition",
        ["enum def"] = "EnumerationDefinition",
        ["interface def"] = "InterfaceDefinition",
        ["action def"] = "ActionDefinition",
        ["state def"] = "StateDefinition",
        ["calc def"] = "CalculationDefinition",
        ["constraint def"] = "ConstraintDefinition",
        ["requirement def"] = "RequirementDefinition",
        ["concern def"] = "ConcernDefinition",
        ["case def"] = "CaseDefinition",
        ["analysis def"] = "AnalysisCaseDefinition",
        ["verification def"] = "VerificationCaseDefinition",
        ["use case def"] = "UseCaseDefinition",

        // Feature (usage) keywords (SysmlFeatureNode.FeatureKeyword).
        ["part"] = "PartUsage",
        ["port"] = "PortUsage",
        ["attribute"] = "AttributeUsage",
        ["item"] = "ItemUsage",
        ["ref"] = "ReferenceUsage",
        ["enum"] = "EnumerationUsage",
        ["occurrence"] = "OccurrenceUsage",
        ["action"] = "ActionUsage",
        ["accept"] = "AcceptActionUsage",
        ["send"] = "SendActionUsage",
        ["requirement"] = "RequirementUsage",
        ["concern"] = "ConcernUsage",
        ["constraint"] = "ConstraintUsage",
        ["state"] = "StateUsage",
    };


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
                Evaluate(workspace, node, expression))
            {
                matched.Add(qualifiedName);
            }
        }

        return new FilterEvaluationResult(matched, Array.Empty<SysmlDiagnostic>());
    }

    /// <summary>Evaluates <paramref name="expression"/>'s boolean value against a single candidate node.</summary>
    private static bool Evaluate(SysmlWorkspace workspace, SysmlNode node, FilterExpression expression) =>
        expression switch
        {
            ClassificationTestExpression classificationTest =>
                FindMetadata(node, classificationTest.TypeName) is not null ||
                MatchesMetaclassKind(workspace, node, classificationTest.TypeName),
            NotFilterExpression not => !Evaluate(workspace, node, not.Operand),
            BooleanFilterExpression boolean => boolean.Connective switch
            {
                BooleanConnective.And => Evaluate(workspace, node, boolean.Left) && Evaluate(workspace, node, boolean.Right),
                BooleanConnective.Or => Evaluate(workspace, node, boolean.Left) || Evaluate(workspace, node, boolean.Right),
                BooleanConnective.Xor => Evaluate(workspace, node, boolean.Left) ^ Evaluate(workspace, node, boolean.Right),
                _ => false,
            },
            AttributeReadExpression attributeRead => ReadAttribute(node, attributeRead) is { Kind: MetadataAttributeValueKind.Boolean } value
                && (value.BooleanValue ?? false),
            ComparisonFilterExpression comparison => EvaluateComparison(node, comparison),
            _ => false,
        };

    /// <summary>
    /// Determines whether <paramref name="node"/>'s own AST node kind (its
    /// <see cref="SysmlDefinitionNode.DefinitionKeyword"/> or
    /// <see cref="SysmlFeatureNode.FeatureKeyword"/>) maps — directly, or transitively via the
    /// stdlib's <c>specializes</c> chain — to the requested built-in SysML metaclass name
    /// <paramref name="typeName"/> (bare, e.g. <c>PartUsage</c>, or qualified, e.g.
    /// <c>SysML::PartUsage</c>). See <see cref="MetaclassNames"/> for the keyword mapping table
    /// and its documented known gaps.
    /// </summary>
    private static bool MatchesMetaclassKind(SysmlWorkspace workspace, SysmlNode node, string typeName)
    {
        var keyword = node switch
        {
            SysmlDefinitionNode definition => definition.DefinitionKeyword,
            SysmlFeatureNode feature => feature.FeatureKeyword,
            _ => null,
        };

        if (string.IsNullOrEmpty(keyword) || !MetaclassNames.TryGetValue(keyword, out var bareMetaclassName))
        {
            return false;
        }

        return MetaclassNameMatches(bareMetaclassName, typeName) ||
            ConformsToMetaclass(workspace, bareMetaclassName, typeName, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="bareMetaclassName"/> (e.g.
    /// <c>PartUsage</c>) matches the requested <paramref name="typeName"/>: either bare
    /// (<c>typeName == bareMetaclassName</c>) or qualified with the canonical <c>SysML::</c>
    /// spelling real-world filter expressions use (<c>typeName == "SysML::" + bareMetaclassName</c>).
    /// </summary>
    private static bool MetaclassNameMatches(string bareMetaclassName, string typeName) =>
        typeName == bareMetaclassName ||
        typeName == "SysML::" + bareMetaclassName;

    /// <summary>
    /// Walks the stdlib <c>specializes</c> chain starting from the stdlib <c>metadata def</c>
    /// declaration whose simple name is <paramref name="bareMetaclassName"/>, looking for an
    /// ancestor metaclass matching <paramref name="typeName"/>.
    /// </summary>
    /// <remarks>
    /// Stdlib-only nodes are never passed through <see cref="ReferenceResolver.ResolveAll"/> (see
    /// <see cref="SysmlNode.ResolvedEdges"/>'s remarks), so this walk cannot use resolved
    /// <see cref="SysmlEdgeKind.Supertype"/> edges the way ordinary user-model specialization
    /// lookups do (e.g. <c>ReferenceResolver.FindMemberInTypeHierarchy</c>). Instead it uses each
    /// stdlib metaclass declaration's raw, unresolved <see cref="SysmlNode.SupertypeNames"/> text
    /// (populated unconditionally by <c>AstBuilder</c> during parsing, before any resolution pass
    /// runs) and resolves each simple supertype name to its declaring stdlib node by a
    /// same-simple-name suffix lookup restricted to <see cref="SysmlWorkspace.StdlibNames"/> entries
    /// of <see cref="SysmlWorkspace.Declarations"/> (stdlib metaclasses are declared with a longer
    /// nested-package qualified name than their conventional <c>SysML::</c> spelling, e.g.
    /// <c>SysML::Systems::PartUsage</c>). Restricting the lookup to <see cref="SysmlWorkspace.StdlibNames"/>
    /// keeps this sound in the presence of a user model that happens to declare its own
    /// definition/usage with a colliding simple name (e.g. a user <c>part def PartUsage;</c>): such
    /// user declarations are never candidates for the stdlib specialization walk. This is a
    /// deliberately narrow, bounded heuristic (not a general reference-resolution mechanism): it
    /// assumes the stdlib's metaclass simple names are unique enough for a suffix match to be
    /// unambiguous, which holds for the metaclass names this table covers.
    /// <paramref name="visited"/> guards against a cyclical specialization chain (defensive only —
    /// the stdlib itself is well-formed), mirroring <c>ReferenceResolver.FindMemberInTypeHierarchy</c>'s
    /// cycle-guard pattern.
    /// </remarks>
    private static bool ConformsToMetaclass(
        SysmlWorkspace workspace,
        string bareMetaclassName,
        string typeName,
        HashSet<string> visited)
    {
        if (!visited.Add(bareMetaclassName))
        {
            return false;
        }

        var declaration = workspace.Declarations
            .FirstOrDefault(kvp =>
                workspace.StdlibNames.Contains(kvp.Key) &&
                (kvp.Key == bareMetaclassName || kvp.Key.EndsWith("::" + bareMetaclassName, StringComparison.Ordinal)))
            .Value;
        if (declaration is null)
        {
            return false;
        }

        foreach (var supertypeName in declaration.SupertypeNames)
        {
            var bareSupertypeName = supertypeName.Contains("::", StringComparison.Ordinal)
                ? supertypeName[(supertypeName.LastIndexOf("::", StringComparison.Ordinal) + 2)..]
                : supertypeName;

            if (MetaclassNameMatches(bareSupertypeName, typeName) ||
                ConformsToMetaclass(workspace, bareSupertypeName, typeName, visited))
            {
                return true;
            }
        }

        return false;
    }

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
