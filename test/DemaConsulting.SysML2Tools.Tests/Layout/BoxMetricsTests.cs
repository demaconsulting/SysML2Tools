// <copyright file="BoxMetricsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Layout;
using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Tests.Layout;

/// <summary>
///     Tests for <see cref="BoxMetrics"/>, the shared box title-area and folder-tab height
///     formulas that keep space reserved by the layout strategies equal to space drawn by the
///     renderers. Values are asserted against the Light theme's font sizes and label padding.
/// </summary>
public sealed class BoxMetricsTests
{
    /// <summary>The folder-tab height is the body font size plus two label paddings.</summary>
    [Fact]
    public void BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding()
    {
        // Arrange: the Light theme (FontSizeBody 12, LabelPadding 6).
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.FolderTabHeight(theme);

        // Assert: body font size plus two label paddings (12 + 2*6 = 24).
        Assert.Equal(theme.FontSizeBody + (2.0 * theme.LabelPadding), height);
        Assert.Equal(24.0, height);
    }

    /// <summary>A box with neither a name label nor a keyword reserves no title area.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: false, hasKeyword: false);

        // Assert
        Assert.Equal(0.0, height);
    }

    /// <summary>A labelled box reserves padding plus one title line.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        // Assert: leading padding + (title font + trailing padding).
        Assert.Equal(theme.LabelPadding + theme.FontSizeTitle + theme.LabelPadding, height);
    }

    /// <summary>A box with a keyword and a name reserves padding plus both lines.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        // Assert: leading padding + keyword line + name line, each followed by padding.
        Assert.Equal(
            theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding + theme.FontSizeTitle + theme.LabelPadding,
            height);
    }
}
