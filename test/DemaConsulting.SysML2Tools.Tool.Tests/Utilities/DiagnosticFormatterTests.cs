// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Utilities;

namespace DemaConsulting.SysML2Tools.Tests;

/// <summary>
///     Tests for the DiagnosticFormatter class.
/// </summary>
[Collection("Sequential")]
public class DiagnosticFormatterTests
{
    /// <summary>
    ///     Test that Format renders a located warning as path(line,col): warning: message.
    /// </summary>
    [Fact]
    public void DiagnosticFormatter_Format_LocatedWarning_UsesPathLineColumnSeverityMessage()
    {
        // Arrange: a warning with a file path and position
        var diagnostic = new SysmlDiagnostic(
            "model.sysml", 3, 18, DiagnosticSeverity.Warning, "Unresolved reference: 'DoesNotExist'");

        // Act: format the diagnostic
        var text = DiagnosticFormatter.Format(diagnostic);

        // Assert: the exact path(line,col): severity: message format is produced
        Assert.Equal("model.sysml(3,18): warning: Unresolved reference: 'DoesNotExist'", text);
    }

    /// <summary>
    ///     Test that Format renders every severity as its lowercase name.
    /// </summary>
    [Theory]
    [InlineData(DiagnosticSeverity.Info, "info")]
    [InlineData(DiagnosticSeverity.Warning, "warning")]
    [InlineData(DiagnosticSeverity.Error, "error")]
    public void DiagnosticFormatter_Format_Severity_IsLowercase(DiagnosticSeverity severity, string expected)
    {
        // Arrange: a diagnostic of the given severity
        var diagnostic = new SysmlDiagnostic("a.sysml", 1, 0, severity, "message");

        // Act: format the diagnostic
        var text = DiagnosticFormatter.Format(diagnostic);

        // Assert: the severity appears as its lowercase name
        Assert.Equal($"a.sysml(1,0): {expected}: message", text);
    }

    /// <summary>
    ///     Test that Format omits the location when the diagnostic has no file path.
    /// </summary>
    [Fact]
    public void DiagnosticFormatter_Format_NoFilePath_OmitsLocation()
    {
        // Arrange: a diagnostic that is not tied to a file
        var diagnostic = new SysmlDiagnostic(string.Empty, 0, 0, DiagnosticSeverity.Warning, "something");

        // Act: format the diagnostic
        var text = DiagnosticFormatter.Format(diagnostic);

        // Assert: only the severity and message appear
        Assert.Equal("warning: something", text);
    }

    /// <summary>
    ///     Test that Format never produces the compiler-generated record dump.
    /// </summary>
    [Fact]
    public void DiagnosticFormatter_Format_Diagnostic_IsNotRecordDump()
    {
        // Arrange: a located diagnostic
        var diagnostic = new SysmlDiagnostic("a.sysml", 2, 4, DiagnosticSeverity.Error, "bad");

        // Act: format the diagnostic
        var text = DiagnosticFormatter.Format(diagnostic);

        // Assert: no record-dump syntax leaks into the text
        Assert.DoesNotContain("SysmlDiagnostic", text);
        Assert.DoesNotContain("FilePath =", text);
    }

    /// <summary>
    ///     Test that Format rejects a null diagnostic.
    /// </summary>
    [Fact]
    public void DiagnosticFormatter_Format_NullDiagnostic_ThrowsArgumentNullException()
    {
        // Act & Assert: a null diagnostic is rejected
        Assert.Throws<ArgumentNullException>(() => DiagnosticFormatter.Format(null!));
    }
}
