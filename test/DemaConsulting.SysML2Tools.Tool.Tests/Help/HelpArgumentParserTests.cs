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

using DemaConsulting.SysML2Tools.Help;

namespace DemaConsulting.SysML2Tools.Tests.Help;

/// <summary>
///     Unit tests for <see cref="HelpArgumentParser"/> — parser-only tests that do not invoke
///     <see cref="DemaConsulting.SysML2Tools.Cli.Context"/> or <see cref="Program"/>.
/// </summary>
[Collection("Sequential")]
public class HelpArgumentParserTests
{
    /// <summary>
    ///     The full ordered list of query verb tokens recognized by <c>help query &lt;verb&gt;</c>.
    /// </summary>
    public static TheoryData<string> QueryVerbTokens =>
    [
        "uses",
        "used-by",
        "dependencies",
        "impact",
        "describe",
        "hierarchy",
        "requirements",
        "interface",
        "connections",
        "states",
        "list",
        "find"
    ];

    /// <summary>
    ///     No arguments (bare 'help') leaves both TargetCommand and TargetVerb null.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_NoArguments_ReturnsBothFieldsNull()
    {
        // Act
        var options = HelpArgumentParser.Parse([]);

        // Assert
        Assert.Null(options.TargetCommand);
        Assert.Null(options.TargetVerb);
    }

    /// <summary>
    ///     'help lint' sets TargetCommand to "lint" and leaves TargetVerb null.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_Lint_SetsTargetCommandLint()
    {
        // Act
        var options = HelpArgumentParser.Parse(["lint"]);

        // Assert
        Assert.Equal("lint", options.TargetCommand);
        Assert.Null(options.TargetVerb);
    }

    /// <summary>
    ///     'help render' sets TargetCommand to "render" and leaves TargetVerb null.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_Render_SetsTargetCommandRender()
    {
        // Act
        var options = HelpArgumentParser.Parse(["render"]);

        // Assert
        Assert.Equal("render", options.TargetCommand);
        Assert.Null(options.TargetVerb);
    }

    /// <summary>
    ///     'help query' (no verb) sets TargetCommand to "query" and leaves TargetVerb null.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_QueryNoVerb_SetsTargetCommandQueryOnly()
    {
        // Act
        var options = HelpArgumentParser.Parse(["query"]);

        // Assert
        Assert.Equal("query", options.TargetCommand);
        Assert.Null(options.TargetVerb);
    }

    /// <summary>
    ///     'help query &lt;verb&gt;' sets both TargetCommand and TargetVerb for every one of the
    ///     12 recognized verbs.
    /// </summary>
    [Theory]
    [MemberData(nameof(QueryVerbTokens))]
    public void HelpArgumentParser_Parse_QueryWithVerb_SetsTargetVerb(string verbToken)
    {
        // Act
        var options = HelpArgumentParser.Parse(["query", verbToken]);

        // Assert
        Assert.Equal("query", options.TargetCommand);
        Assert.Equal(verbToken, options.TargetVerb);
    }

    /// <summary>
    ///     An unrecognized target command throws ArgumentException naming the valid targets.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_UnknownTargetCommand_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => HelpArgumentParser.Parse(["bogus"]));
        Assert.Contains("bogus", exception.Message);
        Assert.Contains("lint", exception.Message);
        Assert.Contains("render", exception.Message);
        Assert.Contains("query", exception.Message);
    }

    /// <summary>
    ///     'help query bogus-verb' — an unrecognized verb under 'query' throws ArgumentException,
    ///     reusing QueryVerbParsing.Parse's existing error message rather than duplicating the
    ///     verb vocabulary.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_QueryWithUnknownVerb_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => HelpArgumentParser.Parse(["query", "bogus-verb"]));
        Assert.Contains("bogus-verb", exception.Message);
    }

    /// <summary>
    ///     A '-'-prefixed token as the target command throws ArgumentException rather than being
    ///     silently treated as a command name.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_FlagAsTargetCommand_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => HelpArgumentParser.Parse(["--bogus"]));
    }

    /// <summary>
    ///     An extra trailing token after 'help lint' throws ArgumentException naming the extra
    ///     argument and the 'help' command.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_ExtraArgumentAfterCommand_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => HelpArgumentParser.Parse(["lint", "extra"]));
        Assert.Contains("extra", exception.Message);
        Assert.Contains("help", exception.Message);
    }

    /// <summary>
    ///     An extra trailing token after 'help query &lt;verb&gt;' throws ArgumentException naming
    ///     the extra argument and the 'help' command.
    /// </summary>
    [Fact]
    public void HelpArgumentParser_Parse_ExtraArgumentAfterQueryVerb_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => HelpArgumentParser.Parse(["query", "uses", "extra"]));
        Assert.Contains("extra", exception.Message);
        Assert.Contains("help", exception.Message);
    }
}
