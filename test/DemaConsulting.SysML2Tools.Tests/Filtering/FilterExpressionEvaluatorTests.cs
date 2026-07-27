// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Filtering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Tests.Filtering;

/// <summary>
///     Tests for <see cref="FilterExpressionEvaluator"/>.
/// </summary>
public sealed class FilterExpressionEvaluatorTests
{
    /// <summary>Loads a workspace from inline SysML v2 source text (temp-file round trip).</summary>
    private static async Task<SysmlWorkspace> LoadAsync(string source)
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        try
        {
            await File.WriteAllTextAsync(tempFile, source, TestContext.Current.CancellationToken);
            var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
            var result = await WorkspaceLoader.LoadAsync([tempFile], stdlibTable);
            Assert.NotNull(result.Workspace);
            return result.Workspace;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private const string Source = """
        package P {
            metadata def Safety {
                attribute isMandatory : Boolean;
                attribute level : String;
            }

            metadata def Critical {
            }

            part def Engine {
                @Safety {
                    isMandatory = true;
                    level = "high";
                }
            }

            part def Wiring {
                @Safety {
                    isMandatory = false;
                }
            }

            part def Housing {
            }
        }
        """;

    /// <summary>A classification test matches only candidates carrying the referenced metadata annotation.</summary>
    [Fact]
    public async Task Evaluate_ClassificationTest_MatchesOnlyAnnotatedCandidates()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ClassificationTestExpression("Safety");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["P::Engine", "P::Wiring", "P::Housing"], expression);

        Assert.Equal(["P::Engine", "P::Wiring"], result.MatchedQualifiedNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>A qualified classification test also matches via its qualified type reference.</summary>
    [Fact]
    public async Task Evaluate_QualifiedClassificationTest_Matches()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ClassificationTestExpression("P::Safety");

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine"], expression);

        Assert.Contains("P::Engine", result.MatchedQualifiedNames);
    }

    /// <summary>A classification test that no candidate carries matches nothing.</summary>
    [Fact]
    public async Task Evaluate_ClassificationTestNoMatch_ReturnsEmpty()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ClassificationTestExpression("Critical");

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring", "P::Housing"], expression);

        Assert.Empty(result.MatchedQualifiedNames);
    }

    /// <summary><c>not</c> inverts the classification test's match set.</summary>
    [Fact]
    public async Task Evaluate_Not_InvertsMatchSet()
    {
        var workspace = await LoadAsync(Source);
        var expression = new NotFilterExpression(new ClassificationTestExpression("Safety"));

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring", "P::Housing"], expression);

        Assert.Equal(["P::Housing"], result.MatchedQualifiedNames);
    }

    /// <summary><c>and</c> matches only candidates satisfying both operands.</summary>
    [Fact]
    public async Task Evaluate_And_MatchesIntersection()
    {
        var workspace = await LoadAsync(Source);
        var expression = new BooleanFilterExpression(
            BooleanConnective.And,
            "and",
            new ClassificationTestExpression("Safety"),
            new AttributeReadExpression("Safety", "isMandatory"));

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring", "P::Housing"], expression);

        Assert.Equal(["P::Engine"], result.MatchedQualifiedNames);
    }

    /// <summary><c>or</c> matches candidates satisfying either operand.</summary>
    [Fact]
    public async Task Evaluate_Or_MatchesUnion()
    {
        var workspace = await LoadAsync(Source);
        var expression = new BooleanFilterExpression(
            BooleanConnective.Or,
            "or",
            new ClassificationTestExpression("Critical"),
            new ClassificationTestExpression("Safety"));

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring", "P::Housing"], expression);

        Assert.Equal(["P::Engine", "P::Wiring"], result.MatchedQualifiedNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>A bare attribute read is truthy only when the captured boolean value is true.</summary>
    [Fact]
    public async Task Evaluate_BareAttributeRead_TrueOnlyWhenBooleanValueTrue()
    {
        var workspace = await LoadAsync(Source);
        var expression = new AttributeReadExpression("Safety", "isMandatory");

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring"], expression);

        Assert.Equal(["P::Engine"], result.MatchedQualifiedNames);
    }

    /// <summary>An attribute read comparison matches candidates whose captured value equals the literal.</summary>
    [Fact]
    public async Task Evaluate_ComparisonEqual_MatchesEqualValue()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ComparisonFilterExpression(
            new AttributeReadExpression("Safety", "level"),
            ComparisonOperator.Equal,
            new LiteralFilterExpression(FilterLiteralKind.String, StringValue: "high"));

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring"], expression);

        Assert.Equal(["P::Engine"], result.MatchedQualifiedNames);
    }

    /// <summary>An attribute read comparison with <c>!=</c> matches candidates whose value differs.</summary>
    [Fact]
    public async Task Evaluate_ComparisonNotEqual_MatchesDifferingValue()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ComparisonFilterExpression(
            new AttributeReadExpression("Safety", "isMandatory"),
            ComparisonOperator.NotEqual,
            new LiteralFilterExpression(FilterLiteralKind.Boolean, BooleanValue: true));

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Engine", "P::Wiring"], expression);

        Assert.Equal(["P::Wiring"], result.MatchedQualifiedNames);
    }

    /// <summary>An attribute read against a candidate with no matching metadata annotation is absent (never matches).</summary>
    [Fact]
    public async Task Evaluate_AttributeReadAbsent_NeverMatchesComparison()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ComparisonFilterExpression(
            new AttributeReadExpression("Safety", "isMandatory"),
            ComparisonOperator.NotEqual,
            new LiteralFilterExpression(FilterLiteralKind.Boolean, BooleanValue: true));

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::Housing"], expression);

        Assert.Empty(result.MatchedQualifiedNames);
    }

    /// <summary>Evaluation never throws for candidates missing from the workspace's declarations.</summary>
    [Fact]
    public async Task Evaluate_UnknownCandidate_SkipsGracefully()
    {
        var workspace = await LoadAsync(Source);
        var expression = new ClassificationTestExpression("Safety");

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["P::DoesNotExist"], expression);

        Assert.Empty(result.MatchedQualifiedNames);
    }

    private const string UsageAndDefinitionSource = """
        package Q {
            metadata def Safety {
            }

            part def Engine {
                part cylinder;
            }

            requirement def Req1;

            part myEngine : Engine {
                @Safety;
            }

            requirement myRequirement : Req1;

            item myItem;
        }
        """;

    /// <summary>
    ///     Existing applied-annotation matching (<c>@Safety</c>) still works on a usage-level
    ///     candidate, unaffected by the new metaclass-kind match path being additionally checked.
    /// </summary>
    [Fact]
    public async Task Evaluate_ClassificationTest_AppliedAnnotationMatchingUnaffectedByMetaclassKindAddition()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSource);
        var expression = new ClassificationTestExpression("Safety");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["Q::myEngine", "Q::myRequirement", "Q::myItem"], expression);

        Assert.Equal(["Q::myEngine"], result.MatchedQualifiedNames);
    }

    /// <summary>A bare metaclass-kind classification test matches a usage of the matching keyword.</summary>
    [Fact]
    public async Task Evaluate_BareMetaclassKind_MatchesUsage()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSource);
        var expression = new ClassificationTestExpression("PartUsage");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["Q::myEngine", "Q::myRequirement", "Q::myItem"], expression);

        Assert.Equal(["Q::myEngine"], result.MatchedQualifiedNames);
    }

    /// <summary>A <c>SysML::</c>-qualified metaclass-kind classification test matches a usage identically to the bare form.</summary>
    [Fact]
    public async Task Evaluate_QualifiedMetaclassKind_MatchesUsage()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSource);
        var expression = new ClassificationTestExpression("SysML::PartUsage");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["Q::myEngine", "Q::myRequirement", "Q::myItem"], expression);

        Assert.Equal(["Q::myEngine"], result.MatchedQualifiedNames);
    }

    /// <summary>A metaclass-kind classification test also matches a definition of the corresponding <c>*Definition</c> metaclass.</summary>
    [Fact]
    public async Task Evaluate_MetaclassKind_MatchesDefinition()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSource);
        var expression = new ClassificationTestExpression("SysML::PartDefinition");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["Q::Engine", "Q::Req1"], expression);

        Assert.Equal(["Q::Engine"], result.MatchedQualifiedNames);
    }

    /// <summary>A metaclass-kind classification test for an unrelated metaclass does not match.</summary>
    [Fact]
    public async Task Evaluate_MetaclassKind_NonMatchingMetaclass_DoesNotMatch()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSource);
        var expression = new ClassificationTestExpression("RequirementUsage");

        var result = FilterExpressionEvaluator.Evaluate(workspace, ["Q::myEngine"], expression);

        Assert.Empty(result.MatchedQualifiedNames);
    }

    /// <summary>
    ///     A metaclass-kind classification test also matches via the stdlib's <c>specializes</c>
    ///     chain: a <c>requirement</c> usage's mapped metaclass (<c>RequirementUsage</c>)
    ///     specializes <c>ConstraintUsage</c> in the stdlib, so <c>@ConstraintUsage</c> matches it
    ///     too.
    /// </summary>
    [Fact]
    public async Task Evaluate_MetaclassKind_SpecializationConformance_MatchesAncestorMetaclass()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSource);
        var expression = new ClassificationTestExpression("SysML::ConstraintUsage");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["Q::myRequirement", "Q::myEngine"], expression);

        Assert.Equal(["Q::myRequirement"], result.MatchedQualifiedNames);
    }

    /// <summary>
    ///     The specialization-conformance walk in <c>ConformsToMetaclass</c> resolves the stdlib
    ///     metaclass declaration by simple name (see that method's remarks). A user model may
    ///     happen to declare its own definition/usage whose simple name collides with a genuine
    ///     stdlib metaclass name (here, a user <c>part def RequirementUsage;</c> that has nothing
    ///     to do with the real <c>SysML::RequirementUsage</c> metaclass and does not specialize
    ///     anything). The walk must still resolve the genuine stdlib <c>RequirementUsage</c>
    ///     metaclass declaration — not the colliding user declaration — so
    ///     <c>@ConstraintUsage</c> still matches a <c>requirement</c> usage via the stdlib's
    ///     <c>RequirementUsage specializes ConstraintUsage</c> chain, unaffected by the collision.
    /// </summary>
    [Fact]
    public async Task Evaluate_MetaclassKind_SpecializationConformance_UnaffectedByUserModelNameCollision()
    {
        var workspace = await LoadAsync(UsageAndDefinitionSourceWithCollidingUserDeclaration);
        var expression = new ClassificationTestExpression("SysML::ConstraintUsage");

        var result = FilterExpressionEvaluator.Evaluate(
            workspace, ["Q::myRequirement", "Q::myEngine"], expression);

        Assert.Equal(["Q::myRequirement"], result.MatchedQualifiedNames);
    }

    private const string UsageAndDefinitionSourceWithCollidingUserDeclaration = """
        package Q {
            metadata def Safety {
            }

            part def Engine {
                part cylinder;
            }

            requirement def Req1;

            part myEngine : Engine {
                @Safety;
            }

            requirement myRequirement : Req1;

            item myItem;

            // Colliding user-model declaration: same simple name as the stdlib
            // "RequirementUsage" metaclass, but an unrelated user part definition
            // that does not specialize anything.
            part def RequirementUsage;
        }
        """;
}
