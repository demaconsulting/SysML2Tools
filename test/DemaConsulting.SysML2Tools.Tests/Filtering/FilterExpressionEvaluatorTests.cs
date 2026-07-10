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
            return result.Workspace!;
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
}
