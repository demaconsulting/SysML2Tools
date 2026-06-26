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

using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.SelfTest;

namespace DemaConsulting.SysML2Tools.Tests;

/// <summary>
///     Unit tests for the Validation class.
/// </summary>
[Collection("Sequential")]
public class ValidationTests
{
    /// <summary>
    ///     Test that Run throws ArgumentNullException when context is null.
    /// </summary>
    [Fact]
    public async Task Validation_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange: setup test conditions
        // No setup required — null is the input under test.

        // Act & Assert: invoke Run with null context and verify ArgumentNullException is thrown
        await Assert.ThrowsAsync<ArgumentNullException>(() => Validation.RunAsync(null!));
    }

    /// <summary>
    ///     Test that Run prints a summary containing total, passed, and failed counts.
    /// </summary>
    [Fact]
    public async Task Validation_Run_WithSilentContext_PrintsSummary()
    {
        // Arrange: setup unique log file path to capture silent context output
        var logFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.log");
        try
        {
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: run validation with silent context and log file
                await Validation.RunAsync(context);
            }

            // Assert: verify summary lines are written to log file
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("Total Tests:", logContent);
            Assert.Contains("Passed:", logContent);
            Assert.Contains("Failed:", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that Run exits with code zero when all self-validation tests pass.
    /// </summary>
    [Fact]
    public async Task Validation_Run_WithSilentContext_ExitCodeIsZero()
    {
        // Arrange: create silent context for validation run
        using var context = Context.Create(["--silent"]);

        // Act: run validation with silent context
        await Validation.RunAsync(context);

        // Assert: verify exit code is zero indicating successful validation
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that Run writes a valid TRX file when the results path ends with .trx.
    /// </summary>
    [Fact]
    public async Task Validation_Run_WithTrxResultsFile_WritesTrxFile()
    {
        // Arrange: setup TRX file path for test results output
        var trxFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.trx");
        try
        {
            using var context = Context.Create(["--silent", "--results", trxFile]);

            // Act: run validation with TRX results output
            await Validation.RunAsync(context);

            // Assert: verify TRX file is created with expected content
            Assert.True(File.Exists(trxFile));
            var content = await File.ReadAllTextAsync(trxFile, TestContext.Current.CancellationToken);
            Assert.Contains("<TestRun", content);
        }
        finally
        {
            if (File.Exists(trxFile))
            {
                File.Delete(trxFile);
            }
        }
    }

    /// <summary>
    ///     Test that Run writes a valid JUnit XML file when the results path ends with .xml.
    /// </summary>
    [Fact]
    public async Task Validation_Run_WithXmlResultsFile_WritesXmlFile()
    {
        // Arrange: setup XML file path for JUnit results output
        var xmlFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.xml");
        try
        {
            using var context = Context.Create(["--silent", "--results", xmlFile]);

            // Act: run validation with XML results output  
            await Validation.RunAsync(context);

            // Assert: verify XML file is created with JUnit format content
            Assert.True(File.Exists(xmlFile));
            var content = await File.ReadAllTextAsync(xmlFile, TestContext.Current.CancellationToken);
            Assert.Contains("<testsuites", content);
        }
        finally
        {
            if (File.Exists(xmlFile))
            {
                File.Delete(xmlFile);
            }
        }
    }

    /// <summary>
    ///     Test that Run does not write a results file when the extension is unsupported.
    /// </summary>
    [Fact]
    public async Task Validation_Run_WithUnsupportedResultsFormat_DoesNotWriteFile()
    {
        // Arrange: setup unsupported file extension and log file to capture error output
        var jsonFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.json");
        var logFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.log");
        try
        {
            using (var context = Context.Create(["--silent", "--results", jsonFile, "--log", logFile]))
            {
                // Act: run validation with unsupported results file extension
                await Validation.RunAsync(context);

                // Assert context state while still valid: no results file and non-zero exit code
                Assert.False(File.Exists(jsonFile));
                Assert.Equal(1, context.ExitCode);
            }

            // Assert log content after disposal to ensure the log writer has been closed and flushed
            Assert.True(File.Exists(logFile));
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("Unsupported results file format", logContent);
        }
        finally
        {
            if (File.Exists(jsonFile))
            {
                File.Delete(jsonFile);
            }

            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that the lint self-test passes for the built-in self-test model.
    /// </summary>
    [Fact]
    public async Task Validation_RunLintSelfTest_ValidModel_Passes()
    {
        // Arrange: capture validation output via log file
        var logFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.log");
        try
        {
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: run the full validation suite (includes lint self-test)
                await Validation.RunAsync(context);
            }

            // Assert: the lint self-test produced a pass marker in the log
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("✓ SysML2Tools_LintSelfTest", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that the SVG render self-test passes for the built-in self-test model.
    /// </summary>
    [Fact]
    public async Task Validation_RunRenderSvgSelfTest_ValidModel_Passes()
    {
        // Arrange: capture validation output via log file
        var logFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.log");
        try
        {
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: run the full validation suite (includes SVG render self-test)
                await Validation.RunAsync(context);
            }

            // Assert: the SVG render self-test produced a pass marker in the log
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("✓ SysML2Tools_RenderSvgSelfTest", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that the PNG render self-test passes (or is skipped) when SkiaSharp is available.
    /// </summary>
    [Fact]
    public async Task Validation_RunRenderPngSelfTest_SkiaSharpAvailable_Passes()
    {
        // Guard: check if SkiaSharp native library is loadable without triggering type
        // initializers. If unavailable, the self-test skips internally and we only check
        // that the suite still exits cleanly.
        if (!System.Runtime.InteropServices.NativeLibrary.TryLoad("libSkiaSharp", out var nativeHandle))
        {
            // SkiaSharp unavailable — just verify the suite exits cleanly (skip is recorded as pass)
            using var context = Context.Create(["--silent"]);
            await Validation.RunAsync(context);
            Assert.Equal(0, context.ExitCode);
            return;
        }

        System.Runtime.InteropServices.NativeLibrary.Free(nativeHandle);

        // Arrange: capture validation output via log file
        var logFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.log");
        try
        {
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: run the full validation suite (includes PNG render self-test)
                await Validation.RunAsync(context);
            }

            // Assert: the PNG render self-test produced a pass marker in the log
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("✓ SysML2Tools_RenderPngSelfTest", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that Run prints a "PASSED" summary line when all self-tests pass.
    /// </summary>
    [Fact]
    public async Task Validation_Run_AllTestsPass_PrintsPassedSummary()
    {
        // Arrange: capture validation output via log file
        var logFile = Path.Combine(Path.GetTempPath(), $"validation_test_{Guid.NewGuid()}.log");
        try
        {
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                // Act: run the full validation suite
                await Validation.RunAsync(context);
            }

            // Assert: the PASSED summary line is present in the log
            var logContent = await File.ReadAllTextAsync(logFile, TestContext.Current.CancellationToken);
            Assert.Contains("SysML2Tools self-test: PASSED", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }
}

