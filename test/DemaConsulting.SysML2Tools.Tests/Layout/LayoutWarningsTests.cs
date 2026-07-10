// <copyright file="LayoutWarningsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout.Internal;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="LayoutWarnings"/>.
/// </summary>
public sealed class LayoutWarningsTests
{
    /// <summary>Zero crossings produces no warnings.</summary>
    [Fact]
    public void ForCrossings_Zero_ReturnsEmpty()
    {
        Assert.Empty(LayoutWarnings.ForCrossings("View", 0));
    }

    /// <summary>A single crossing produces a singular-form warning naming the view.</summary>
    [Fact]
    public void ForCrossings_One_ReturnsSingularWarning()
    {
        var warnings = LayoutWarnings.ForCrossings("MyView", 1);

        var message = Assert.Single(warnings);
        Assert.Contains("1 connector", message);
        Assert.Contains("MyView", message);
    }

    /// <summary>Multiple crossings produce a plural-form warning with the count.</summary>
    [Fact]
    public void ForCrossings_Many_ReturnsPluralWarning()
    {
        var warnings = LayoutWarnings.ForCrossings("V", 3);

        var message = Assert.Single(warnings);
        Assert.Contains("3 connectors", message);
    }

    /// <summary>A null filter expression text produces no warnings.</summary>
    [Fact]
    public void ForUnevaluatedFilter_NullText_ReturnsEmpty()
    {
        Assert.Empty(LayoutWarnings.ForUnevaluatedFilter("View", null));
    }

    /// <summary>
    ///     A non-null filter expression text produces a single warning naming the view and stating
    ///     that the filter expression could not be evaluated.
    /// </summary>
    [Fact]
    public void ForUnevaluatedFilter_NonNullText_ReturnsNotYetEvaluatedWarning()
    {
        var warnings = LayoutWarnings.ForUnevaluatedFilter("MyView", "@SysML::PartUsage");

        var message = Assert.Single(warnings);
        Assert.Contains("MyView", message);
        Assert.Contains("filter expression", message);
        Assert.Contains("could not be evaluated", message);
    }

    /// <summary>A reason, when supplied, is included in the warning message.</summary>
    [Fact]
    public void ForUnevaluatedFilter_WithReason_IncludesReason()
    {
        var warnings = LayoutWarnings.ForUnevaluatedFilter("MyView", "istype Foo", "unsupported construct");

        var message = Assert.Single(warnings);
        Assert.Contains("unsupported construct", message);
    }

    /// <summary>An empty bracket-filter list produces no warnings.</summary>
    [Fact]
    public void ForUnevaluatedExposeBracketFilter_Empty_ReturnsEmpty()
    {
        Assert.Empty(LayoutWarnings.ForUnevaluatedExposeBracketFilter("View", []));
    }

    /// <summary>A non-empty bracket-filter list produces a single warning naming the view.</summary>
    [Fact]
    public void ForUnevaluatedExposeBracketFilter_NonEmpty_ReturnsWarning()
    {
        var warnings = LayoutWarnings.ForUnevaluatedExposeBracketFilter("MyView", ["@Safety"]);

        var message = Assert.Single(warnings);
        Assert.Contains("MyView", message);
        Assert.Contains("bracket-filter", message);
        Assert.Contains("not yet evaluated", message);
    }
}
