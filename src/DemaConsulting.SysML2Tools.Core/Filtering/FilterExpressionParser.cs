// <copyright file="FilterExpressionParser.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

// cspell:ignore parenthesization istype hastype ISTYPE HASTYPE LPAREN RPAREN LBRACK RBRACK uncatchable

using Antlr4.Runtime;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Parser.Antlr;

namespace DemaConsulting.SysML2Tools.Filtering;

/// <summary>
/// The outcome of <see cref="FilterExpressionParser.Parse"/>: either a successfully-built
/// <see cref="FilterExpression"/> tree, or a set of diagnostics explaining why parsing failed
/// (a syntax error, or a construct outside the Phase 1 subset).
/// </summary>
/// <param name="Expression">
/// The parsed expression tree, or <see langword="null"/> when the raw text could not be parsed as
/// valid syntax, or contained a construct outside the Phase 1 subset (see <see cref="Diagnostics"/>
/// for the reason).
/// </param>
/// <param name="Diagnostics">
/// Diagnostics produced while parsing. Empty when <see cref="Expression"/> is non-null and no
/// warnings apply.
/// </param>
public sealed record FilterParseResult(FilterExpression? Expression, IReadOnlyList<SysmlDiagnostic> Diagnostics);

/// <summary>
/// Adapts the ANTLR-generated <see cref="SysMLv2Parser"/>'s <c>ownedExpression()</c> parse of a raw
/// filter-expression fragment (e.g. <see cref="DemaConsulting.SysML2Tools.Semantic.Model.SysmlViewNode.FilterExpressionText"/>)
/// into a <see cref="FilterExpression"/> tree, restricted to the Phase 1 construct subset:
/// classification-test atoms, boolean connectives, parenthesization, and
/// <c>(as Type).attribute</c> reads (bare, or compared with <c>==</c>/<c>!=</c> against a scalar
/// literal). This class never throws — any syntax error or unsupported construct is reported as a
/// <see cref="SysmlDiagnostic"/> in the returned <see cref="FilterParseResult"/> instead.
/// </summary>
public static class FilterExpressionParser
{
    /// <summary>
    /// Virtual file path used when reporting diagnostics for a standalone filter-expression parse
    /// (there is no real source file to name, since the expression text is a fragment already
    /// extracted from its enclosing view by <c>AstBuilder</c>).
    /// </summary>
    private const string VirtualFilePath = "[filter-expression]";

    /// <summary>
    /// Maximum permitted nesting depth (parenthesization, sequence-indexing brackets, and/or
    /// prefix unary operators such as <c>not</c>) before <see cref="Parse"/> rejects the input
    /// with a diagnostic instead of invoking ANTLR's recursive-descent
    /// <c>ownedExpression()</c>/<c>baseExpression()</c>/<c>sequenceExpressionList()</c> parse,
    /// which recurses once per nesting level and has no depth guard of its own — beyond a few
    /// thousand levels (empirically, far fewer for bracket indexing than for parens — see
    /// <see cref="ExceedsMaxNestingDepth"/>'s remarks) that recursion overflows the native call
    /// stack with an uncatchable <see cref="StackOverflowException"/> that terminates the whole
    /// process. This ceiling is chosen well above any realistic Phase 1 filter expression's
    /// nesting (a handful of levels at most) but far below the depth that overflows the stack for
    /// any of the recursing token shapes.
    /// </summary>
    private const int MaxNestingDepth = 200;

    /// <summary>
    /// Parses <paramref name="expressionText"/> into a <see cref="FilterExpression"/> tree.
    /// </summary>
    /// <param name="expressionText">The raw filter-expression source text to parse.</param>
    /// <returns>
    /// A <see cref="FilterParseResult"/> whose <see cref="FilterParseResult.Expression"/> is
    /// non-null only when the entire expression parsed as valid syntax within the Phase 1
    /// construct subset.
    /// </returns>
    public static FilterParseResult Parse(string expressionText)
    {
        ArgumentNullException.ThrowIfNull(expressionText);

        var diagnostics = new List<SysmlDiagnostic>();

        SysMLv2Parser.OwnedExpressionContext cst;
        CommonTokenStream tokenStream;
        try
        {
            var listener = new CollectingErrorListener(diagnostics);
            var inputStream = new AntlrInputStream(expressionText);

            var lexer = new SysMLv2Lexer(inputStream);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(listener);

            tokenStream = new CommonTokenStream(lexer);

            // Eagerly tokenize (lexing is iterative and never recurses) so the nesting depth of
            // the token stream can be checked *before* handing it to the recursive-descent parser
            // below. See MaxNestingDepth's remarks for why this guard is required.
            if (ExceedsMaxNestingDepth(tokenStream))
            {
                diagnostics.Add(Diagnostic(
                    $"Filter expression is too deeply nested (nesting exceeds {MaxNestingDepth} levels)."));
                return new FilterParseResult(null, diagnostics);
            }

            var parser = new SysMLv2Parser(tokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(listener);

            cst = parser.ownedExpression();
        }
        catch (RecognitionException ex)
        {
            diagnostics.Add(Diagnostic($"Filter expression syntax error: {ex.Message}"));
            return new FilterParseResult(null, diagnostics);
        }
        catch (Exception ex)
        {
            // Generic catch is justified here: this class's documented contract is that it never
            // throws for any input, but ANTLR's own lexer/parser internals are not fully hardened
            // against every malformed-input shape. For example, Antlr4.Runtime.Lexer.GetErrorDisplay
            // throws ArgumentException (not RecognitionException) via Char.ConvertToUtf32 when
            // formatting a lexer error for input containing an unpaired UTF-16 surrogate/astral-plane
            // character. Converting any such unexpected failure into a diagnostic instead of letting
            // it propagate is the only way to uphold the "never throws" guarantee this class
            // documents, given a future GUI will call Parse live on every keystroke.
            diagnostics.Add(Diagnostic($"Filter expression could not be parsed: {ex.Message}"));
            return new FilterParseResult(null, diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            // Syntax errors already reported by the ANTLR error listener above.
            return new FilterParseResult(null, diagnostics);
        }

        if (tokenStream.LA(1) != TokenConstants.EOF)
        {
            // parser.ownedExpression() only requires a syntactically valid expression *prefix* —
            // it happily returns without consuming any trailing tokens. Left unchecked, that
            // silently accepts input like "@Foo and @Bar extra garbage tokens" as if it were just
            // "@Foo and @Bar", discarding the trailing content with no diagnostic at all.
            diagnostics.Add(Diagnostic(
                "Unexpected trailing content after expression: the filter expression must consist of a single expression with no trailing tokens."));
            return new FilterParseResult(null, diagnostics);
        }

        var expression = TryBuild(cst, diagnostics);
        return new FilterParseResult(expression, diagnostics);
    }

    /// <summary>
    /// Scans the already-lexed <paramref name="tokenStream"/> for the maximum nesting depth that
    /// ANTLR's recursive-descent <c>ownedExpression()</c>/<c>baseExpression()</c> parse would need
    /// to reach in order to parse this token sequence, without itself recursing. This is a simple
    /// stack-depth simulation over the token kinds that push or pop a parse frame in that grammar:
    /// <c>(</c>, <c>[</c> (the <c>ownedExpression LBRACK sequenceExpressionList? RBRACK</c>
    /// sequence-indexing production, which recurses back into <c>ownedExpression</c> for its
    /// bracketed contents exactly like parenthesization does), and each prefix unary operator
    /// (<c>not</c>/<c>+</c>/<c>-</c>/<c>~</c>/<c>if</c>/<c>all</c>) push a frame; a matching
    /// <c>)</c> or <c>]</c> pops one balanced frame (plus any still-pending unary frames above
    /// it), and reaching any other token (an atom, or a subsequent operator) pops the innermost
    /// run of still-pending unary frames, since those are fully closed only once the
    /// sub-expression they qualify has been recognized.
    /// </summary>
    /// <returns><see langword="true"/> when the simulated depth would exceed <see cref="MaxNestingDepth"/>.</returns>
    private static bool ExceedsMaxNestingDepth(CommonTokenStream tokenStream)
    {
        tokenStream.Fill();

        var pendingIsParen = new Stack<bool>();
        foreach (var token in tokenStream.GetTokens())
        {
            switch (token.Type)
            {
                case SysMLv2Lexer.LPAREN:
                case SysMLv2Lexer.LBRACK:
                    pendingIsParen.Push(true);
                    break;

                case SysMLv2Lexer.NOT:
                case SysMLv2Lexer.PLUS:
                case SysMLv2Lexer.MINUS:
                case SysMLv2Lexer.TILDE:
                case SysMLv2Lexer.IF:
                case SysMLv2Lexer.ALL:
                    pendingIsParen.Push(false);
                    break;

                case SysMLv2Lexer.RPAREN:
                case SysMLv2Lexer.RBRACK:
                    while (pendingIsParen.Count > 0 && !pendingIsParen.Peek())
                    {
                        pendingIsParen.Pop();
                    }

                    if (pendingIsParen.Count > 0)
                    {
                        pendingIsParen.Pop();
                    }

                    break;

                case TokenConstants.EOF:
                    break;

                default:
                    while (pendingIsParen.Count > 0 && !pendingIsParen.Peek())
                    {
                        pendingIsParen.Pop();
                    }

                    break;
            }

            if (pendingIsParen.Count > MaxNestingDepth)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds a <see cref="SysmlDiagnostic"/> for the virtual filter-expression file.</summary>
    private static SysmlDiagnostic Diagnostic(string message) =>
        new(VirtualFilePath, 1, 0, DiagnosticSeverity.Error, message);

    /// <summary>
    /// Recursively converts an <c>ownedExpression</c> CST node into a <see cref="FilterExpression"/>,
    /// restricted to the Phase 1 construct subset. Returns <see langword="null"/> and appends an
    /// "unsupported construct" diagnostic when the node (or one of its descendants) uses a
    /// construct outside that subset.
    /// </summary>
    private static FilterExpression? TryBuild(
        SysMLv2Parser.OwnedExpressionContext context, List<SysmlDiagnostic> diagnostics)
    {
        // Classification test: (AT_SIGN|AT_AT) typeReference — the prefix form only (no left
        // operand). The postfix forms (`x @ Type`, `x istype Type`, `x hastype Type`) are
        // unsupported in Phase 1 (general feature-chain navigation on the left-hand side).
        if (context.typeReference() is { } typeRef && context.ownedExpression().Length == 0 &&
            (context.AT_SIGN() is not null || context.AT_AT() is not null))
        {
            return new ClassificationTestExpression(typeRef.GetText());
        }

        if (context.ISTYPE() is not null || context.HASTYPE() is not null || context.ALL() is not null)
        {
            return Unsupported(context, diagnostics);
        }

        // Boolean connectives: and/or/xor (keyword and symbol spellings) and unary not.
        var operands = context.ownedExpression();
        if (context.AND() is not null && operands.Length == 2)
        {
            return BuildBoolean(BooleanConnective.And, "and", operands, diagnostics);
        }

        if (context.OR() is not null && operands.Length == 2)
        {
            return BuildBoolean(BooleanConnective.Or, "or", operands, diagnostics);
        }

        if (context.XOR() is not null && operands.Length == 2)
        {
            return BuildBoolean(BooleanConnective.Xor, "xor", operands, diagnostics);
        }

        if (context.AMP() is not null && operands.Length == 2)
        {
            return BuildBoolean(BooleanConnective.And, "&", operands, diagnostics);
        }

        if (context.PIPE() is not null && operands.Length == 2)
        {
            return BuildBoolean(BooleanConnective.Or, "|", operands, diagnostics);
        }

        if (context.NOT() is not null && operands.Length == 1)
        {
            var operand = TryBuild(operands[0], diagnostics);
            return operand is null ? null : new NotFilterExpression(operand);
        }

        // Equality comparison: (as Type).attribute == literal / != literal
        if ((context.EQ_EQ() is not null || context.BANG_EQ() is not null) && operands.Length == 2)
        {
            return BuildComparison(context, operands, diagnostics);
        }

        // (as Type).attribute — a DOT read on a metadata-cast base expression. Note: in this
        // grammar DOT binds looser than the boolean connectives (see BuildAttributeReadOnto's
        // remarks), so a DOT node's left operand may itself be an already-assembled boolean
        // chain (e.g. "@Safety and (as Safety)" for source text "@Safety and (as Safety).x")
        // that needs the attribute read re-associated onto its rightmost operand.
        if (context.DOT() is not null && operands.Length == 1 && context.qualifiedName().Length > 0)
        {
            return BuildAttributeReadOnto(operands[0], context.qualifiedName(0).GetText(), diagnostics);
        }

        // Parenthesized sub-expression with no other operator present: baseExpression covers the
        // `(as Type)` cast (handled above via DOT) and plain `( ownedExpression )` grouping —
        // ANTLR's flattened ownedExpression rule represents grouping via baseExpression's
        // `LPAREN sequenceExpressionList? RPAREN` alternative, which is only reachable when this
        // node has no other operator tokens and a single nested ownedExpression.
        if (operands.Length == 0 && context.baseExpression() is { } baseExpr)
        {
            return TryBuildBaseExpression(baseExpr, diagnostics);
        }

        return Unsupported(context, diagnostics);
    }

    /// <summary>Builds a <see cref="BooleanFilterExpression"/>, propagating operand failures.</summary>
    private static FilterExpression? BuildBoolean(
        BooleanConnective connective,
        string operatorText,
        SysMLv2Parser.OwnedExpressionContext[] operands,
        List<SysmlDiagnostic> diagnostics)
    {
        var left = TryBuild(operands[0], diagnostics);
        var right = TryBuild(operands[1], diagnostics);
        return left is null || right is null ? null : new BooleanFilterExpression(connective, operatorText, left, right);
    }

    /// <summary>
    /// Builds a <see cref="ComparisonFilterExpression"/> from an <c>==</c>/<c>!=</c> node. Only
    /// supported when the left operand is an <c>(as Type).attribute</c> read and the right operand
    /// is a scalar literal — any other shape is reported as unsupported.
    /// </summary>
    private static FilterExpression? BuildComparison(
        SysMLv2Parser.OwnedExpressionContext context,
        SysMLv2Parser.OwnedExpressionContext[] operands,
        List<SysmlDiagnostic> diagnostics)
    {
        var left = TryBuild(operands[0], diagnostics);
        if (left is not AttributeReadExpression attributeRead)
        {
            diagnostics.Add(Diagnostic(
                $"Unsupported filter construct: comparison left-hand side must be an '(as Type).attribute' read, found '{operands[0].GetText()}'."));
            return null;
        }

        var right = TryBuildLiteral(operands[1], diagnostics);
        if (right is null)
        {
            return null;
        }

        var op = context.EQ_EQ() is not null ? ComparisonOperator.Equal : ComparisonOperator.NotEqual;
        return new ComparisonFilterExpression(attributeRead, op, right);
    }

    /// <summary>
    /// Builds an <see cref="AttributeReadExpression"/> for a <c>(as Type).attribute</c> DOT read
    /// whose left-hand operand is <paramref name="left"/>.
    /// </summary>
    /// <remarks>
    /// In this project's ANTLR grammar (<c>SysMLv2Parser.g4</c>'s <c>ownedExpression</c> rule),
    /// <c>DOT</c> binds looser than the boolean connectives (<c>and</c>/<c>or</c>/<c>xor</c>/
    /// <c>&amp;</c>/<c>|</c>) and <c>not</c>: for source text like
    /// <c>@Safety and (as Safety).isMandatory</c>, the parser builds the boolean chain
    /// <c>@Safety and (as Safety)</c> first (as its two operands are adjacent in the token
    /// stream), then wraps the trailing <c>.isMandatory</c> DOT around that *entire* chain —
    /// i.e. the CST shape is <c>DOT(AND(@Safety, (as Safety)), isMandatory)</c>, not the
    /// intuitively-expected <c>AND(@Safety, DOT((as Safety), isMandatory))</c>. This mirrors the
    /// canonical OMG filter-expression idiom (see the SysML v2 "Filtering" training example),
    /// so rather than reporting it as unsupported, this method re-associates the attribute read
    /// onto the rightmost operand of a boolean/<c>not</c> chain, recursively, producing the
    /// intuitively-expected tree.
    /// </remarks>
    private static FilterExpression? BuildAttributeReadOnto(
        SysMLv2Parser.OwnedExpressionContext left, string attributeName, List<SysmlDiagnostic> diagnostics)
    {
        var leftOperands = left.ownedExpression();

        // Direct case: left is exactly the "(as Type)" cast primary — the attribute read applies
        // directly to it.
        if (leftOperands.Length == 0 &&
            left.baseExpression() is { } baseExpr && baseExpr.AS() is not null &&
            baseExpr.typeReference() is { } typeRef)
        {
            return new AttributeReadExpression(typeRef.GetText(), attributeName);
        }

        // Re-association case: left is a boolean connective chain — attach the attribute read to
        // its rightmost operand instead (see method remarks), keeping the leftmost operand as-is.
        if (leftOperands.Length == 2)
        {
            var (connective, operatorText) = left switch
            {
                _ when left.AND() is not null => (BooleanConnective.And, "and"),
                _ when left.OR() is not null => (BooleanConnective.Or, "or"),
                _ when left.XOR() is not null => (BooleanConnective.Xor, "xor"),
                _ when left.AMP() is not null => (BooleanConnective.And, "&"),
                _ when left.PIPE() is not null => (BooleanConnective.Or, "|"),
                _ => ((BooleanConnective?)null, (string?)null),
            };

            if (connective is { } c && operatorText is { } opText)
            {
                var leftmost = TryBuild(leftOperands[0], diagnostics);
                var rightmost = BuildAttributeReadOnto(leftOperands[1], attributeName, diagnostics);
                return leftmost is null || rightmost is null
                    ? null
                    : new BooleanFilterExpression(c, opText, leftmost, rightmost);
            }
        }

        // Re-association case: left is a unary "not" — attach the attribute read to its operand.
        if (leftOperands.Length == 1 && left.NOT() is not null)
        {
            var operand = BuildAttributeReadOnto(leftOperands[0], attributeName, diagnostics);
            return operand is null ? null : new NotFilterExpression(operand);
        }

        diagnostics.Add(Diagnostic(
            $"Unsupported filter construct: '.' navigation is only supported on an '(as Type)' cast, found '{left.GetText()}.{attributeName}'."));
        return null;
    }

    /// <summary>Handles a parenthesized-grouping <c>baseExpression</c> with a single nested expression.</summary>
    private static FilterExpression? TryBuildBaseExpression(
        SysMLv2Parser.BaseExpressionContext baseExpr, List<SysmlDiagnostic> diagnostics)
    {
        var inner = baseExpr.sequenceExpressionList()?.ownedExpression();
        if (baseExpr.LPAREN() is not null && baseExpr.AS() is null && inner is { Length: 1 })
        {
            return TryBuild(inner[0], diagnostics);
        }

        diagnostics.Add(Diagnostic(
            $"Unsupported filter construct: '{baseExpr.GetText()}'."));
        return null;
    }

    /// <summary>Builds a <see cref="LiteralFilterExpression"/> from a literal-only ownedExpression node.</summary>
    private static LiteralFilterExpression? TryBuildLiteral(
        SysMLv2Parser.OwnedExpressionContext context, List<SysmlDiagnostic> diagnostics)
    {
        var literal = context.baseExpression()?.literalExpression();
        if (literal is null)
        {
            diagnostics.Add(Diagnostic(
                $"Unsupported filter construct: comparison right-hand side must be a scalar literal, found '{context.GetText()}'."));
            return null;
        }

        if (literal.literalBoolean() is { } boolLiteral)
        {
            return new LiteralFilterExpression(FilterLiteralKind.Boolean, BooleanValue: boolLiteral.TRUE() is not null);
        }

        if (literal.literalString() is { } stringLiteral)
        {
            var text = stringLiteral.GetText();
            var unquoted = text.Length >= 2 ? text[1..^1] : text;
            return new LiteralFilterExpression(FilterLiteralKind.String, StringValue: unquoted);
        }

        if (literal.literalInteger() is { } intLiteral &&
            double.TryParse(intLiteral.GetText(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var iv))
        {
            if (!double.IsFinite(iv))
            {
                return NumericLiteralOutOfRange(context, diagnostics);
            }

            return new LiteralFilterExpression(FilterLiteralKind.Number, NumberValue: iv);
        }

        if (literal.literalReal() is { } realLiteral &&
            double.TryParse(realLiteral.GetText(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rv))
        {
            // double.TryParse succeeds (returning +/-Infinity) for literal text whose magnitude
            // overflows double (e.g. "3.14e400") rather than failing outright. Left unchecked,
            // FilterExpression.ToString() would later print such a value as the literal text
            // "Infinity", which is not valid SysML v2 numeric literal syntax and therefore fails
            // to re-parse — violating the documented round-trip guarantee. Treat non-finite
            // results the same as an unsupported/invalid literal instead.
            if (!double.IsFinite(rv))
            {
                return NumericLiteralOutOfRange(context, diagnostics);
            }

            return new LiteralFilterExpression(FilterLiteralKind.Number, NumberValue: rv);
        }

        diagnostics.Add(Diagnostic(
            $"Unsupported filter construct: comparison right-hand side must be a scalar literal, found '{context.GetText()}'."));
        return null;
    }

    /// <summary>Reports a "numeric literal out of range" diagnostic for a non-finite parsed literal.</summary>
    private static LiteralFilterExpression? NumericLiteralOutOfRange(
        SysMLv2Parser.OwnedExpressionContext context, List<SysmlDiagnostic> diagnostics)
    {
        diagnostics.Add(Diagnostic(
            $"Unsupported filter construct: numeric literal out of range, found '{context.GetText()}'."));
        return null;
    }

    /// <summary>Reports an "unsupported construct" diagnostic for a node outside the Phase 1 subset.</summary>
    private static FilterExpression? Unsupported(
        SysMLv2Parser.OwnedExpressionContext context, List<SysmlDiagnostic> diagnostics)
    {
        diagnostics.Add(Diagnostic($"Unsupported filter construct: '{context.GetText()}'."));
        return null;
    }

    /// <summary>
    /// ANTLR error listener that appends syntax errors to a diagnostics list rather than throwing
    /// or writing to the console, so <see cref="Parse"/> never crashes on malformed input.
    /// </summary>
    private sealed class CollectingErrorListener(List<SysmlDiagnostic> diagnostics) :
        IAntlrErrorListener<IToken>,
        IAntlrErrorListener<int>
    {
        void IAntlrErrorListener<IToken>.SyntaxError(
            TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            Append(line, charPositionInLine, msg);

        void IAntlrErrorListener<int>.SyntaxError(
            TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            Append(line, charPositionInLine, msg);

        private void Append(int line, int column, string msg) =>
            diagnostics.Add(new SysmlDiagnostic(VirtualFilePath, line, column, DiagnosticSeverity.Error, $"Filter expression syntax error: {msg}"));
    }
}
