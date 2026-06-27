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
}
