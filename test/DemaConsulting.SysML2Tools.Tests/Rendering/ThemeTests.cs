// <copyright file="ThemeTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Rendering;

namespace DemaConsulting.SysML2Tools.Tests.Rendering;

/// <summary>
///     Tests for <see cref="Theme"/> connector geometry fields and the approach-zone helper.
/// </summary>
public sealed class ThemeTests
{
    /// <summary>The approach zone sums the stub, bend radius, and supplied clearance.</summary>
    [Fact]
    public void ConnectorApproachZone_SumsStubBendAndClearance()
    {
        // Arrange: Light theme has ConnectorStub 8 and BendRadius 4
        var theme = Themes.Light;

        // Act
        var zone = theme.ConnectorApproachZone(connectorClearance: 10.0);

        // Assert: 8 + 4 + 10
        Assert.Equal(22.0, zone, 6);
    }

    /// <summary>Light and Dark themes carry the same connector geometry; Print is tighter.</summary>
    [Fact]
    public void Themes_HaveExpectedConnectorGeometry()
    {
        Assert.Equal(8.0, Themes.Light.ConnectorStub, 6);
        Assert.Equal(4.0, Themes.Light.BendRadius, 6);
        Assert.Equal(8.0, Themes.Dark.ConnectorStub, 6);
        Assert.Equal(4.0, Themes.Dark.BendRadius, 6);
        Assert.Equal(6.0, Themes.Print.ConnectorStub, 6);
        Assert.Equal(0.0, Themes.Print.BendRadius, 6);
    }
}
