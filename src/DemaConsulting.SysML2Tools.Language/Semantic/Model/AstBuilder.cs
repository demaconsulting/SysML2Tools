// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using DemaConsulting.SysML2Tools.Parser.Antlr;

// cspell:ignore unlexable

namespace DemaConsulting.SysML2Tools.Semantic.Model;

/// <summary>
///     Builds a SysML/KerML AST from an ANTLR4 CST produced by <see cref="SysMLv2Parser"/>.
/// </summary>
internal sealed class AstBuilder : SysMLv2ParserBaseVisitor<SysmlNode?>
{
    /// <summary>
    ///     Tracks the current nesting path as a stack of simple name segments.
    /// </summary>
    private readonly List<string> _namespaceStack = new();

    /// <summary>
    ///     The source file path recorded on view nodes, or null when unknown.
    /// </summary>
    private readonly string? _filePath;

    /// <summary>
    ///     Initializes a new <see cref="AstBuilder"/>.
    /// </summary>
    /// <param name="filePath">
    ///     The path of the source file being built, recorded on each <see cref="SysmlViewNode"/>
    ///     so view-level diagnostics can name their declaring file; null when unknown.
    /// </param>
    public AstBuilder(string? filePath = null)
    {
        _filePath = filePath;
    }

    /// <summary>
    ///     Gets the current namespace prefix by joining the stack with "::".
    /// </summary>
    private string CurrentPrefix => _namespaceStack.Count > 0
        ? string.Join("::", _namespaceStack)
        : string.Empty;

    /// <summary>
    ///     Builds a fully-qualified name from the given simple name and the current namespace stack.
    /// </summary>
    private string QualifyName(string name)
    {
        var prefix = CurrentPrefix;
        return prefix.Length > 0 ? $"{prefix}::{name}" : name;
    }

    /// <summary>
    ///     Builds the AST root from the given CST root namespace context.
    /// </summary>
    public SysmlPackageNode? Build(SysMLv2Parser.RootNamespaceContext context)
    {
        return Visit(context) as SysmlPackageNode;
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRootNamespace(SysMLv2Parser.RootNamespaceContext context)
    {
        var (children, annotations) = CollectBodyElements(context.packageBodyElement());
        return new SysmlPackageNode
        {
            Children = children,
            Annotations = annotations,
        };
    }

    /// <summary>
    ///     Visits <paramref name="tree"/> and records, on the node it builds, the source position of
    ///     the declaration and of each reference text it declares (see <see cref="Stamp"/>). Every
    ///     body element, nested member, and the root is built through this single entry point, so
    ///     positions are captured for every node without touching each individual visitor.
    /// </summary>
    public override SysmlNode? Visit(Antlr4.Runtime.Tree.IParseTree tree)
    {
        var node = base.Visit(tree);
        if (node is not null && tree is Antlr4.Runtime.ParserRuleContext context)
        {
            switch (node)
            {
                case AnnotationCapture:
                    break;
                case MultiNodeCapture multiple:
                    foreach (var inner in multiple.Nodes)
                    {
                        Stamp(inner, context);
                    }

                    break;
                default:
                    Stamp(node, context);
                    break;
            }
        }

        return node;
    }

    /// <summary>
    ///     Records the declaration position of <paramref name="node"/> (the first token of
    ///     <paramref name="context"/>) the first time the node is seen, and locates each reference
    ///     text the node declares among the context's tokens (see <see cref="LocateReferences"/>).
    /// </summary>
    private void Stamp(SysmlNode node, Antlr4.Runtime.ParserRuleContext context)
    {
        if (node.SourceLine == 0 && context.Start is { } start)
        {
            node.SourceFilePath ??= _filePath;
            node.SourceLine = start.Line;
            node.SourceColumn = start.Column + 1;
        }

        LocateReferences(node, context);
    }

    /// <summary>
    ///     Records in <see cref="SysmlNode.ReferencePositions"/> the position of the first token of
    ///     each reference text <paramref name="node"/> declares that is not yet located.
    /// </summary>
    /// <remarks>
    ///     A reference's text is built by concatenating the grammar's tokens (e.g. <c>A::B</c> or
    ///     <c>a.b</c>), so it is found by matching a run of consecutive tokens whose texts
    ///     concatenate to it. The declaration's header (up to its first <c>{</c> or <c>;</c>) is
    ///     searched first, because that is where specialization, typing, redefinition, and
    ///     import references are written; the whole context is searched only when the header does
    ///     not contain the text (references written in the body such as <c>expose</c> and
    ///     <c>verify</c> members). A text with no match (a synthesized name) is left unlocated.
    ///     The declaration's own identifier tokens (its name and short name) are not references,
    ///     so a reference spelled like them (<c>part def A :&gt; A;</c>) is located at the
    ///     reference token; they are only used when nothing else matches.
    /// </remarks>
    private static void LocateReferences(SysmlNode node, Antlr4.Runtime.ParserRuleContext context)
    {
        Dictionary<string, SourcePosition>? positions = null;
        List<Antlr4.Runtime.IToken>? header = null;
        List<Antlr4.Runtime.IToken>? all = null;
        HashSet<int>? headerSkip = null;
        HashSet<int>? allSkip = null;

        foreach (var text in ReferenceTexts(node))
        {
            if (string.IsNullOrEmpty(text) || node.ReferencePositions.ContainsKey(text) ||
                positions?.ContainsKey(text) == true)
            {
                continue;
            }

            header ??= CollectTokens(context, headerOnly: true);
            headerSkip ??= DeclarationTokenIndexes(header, node);
            var position = FindReference(header, text, headerSkip);
            if (position is null)
            {
                all ??= CollectTokens(context, headerOnly: false);
                allSkip ??= DeclarationTokenIndexes(all, node);
                position = FindReference(all, text, allSkip);
            }

            if (position is { } found)
            {
                positions ??= new Dictionary<string, SourcePosition>(node.ReferencePositions, StringComparer.Ordinal);
                positions[text] = found;
            }
        }

        if (positions is not null)
        {
            node.ReferencePositions = positions;
        }
    }

    /// <summary>
    ///     Enumerates the reference texts <see cref="ReferenceResolver"/> resolves for
    ///     <paramref name="node"/>, exactly as the resolver passes them to name resolution.
    /// </summary>
    private static IEnumerable<string?> ReferenceTexts(SysmlNode node)
    {
        var texts = node.SupertypeNames
            .Concat(node.ImportedNames)
            .Concat(node.VerifiedRequirementNames)
            .Cast<string?>();

        return node switch
        {
            SysmlFeatureNode feature => texts.Append(feature.FeatureTyping).Append(feature.RedefinedFeatureName),
            SysmlMetadataNode metadata => texts.Append(metadata.TypeReference),
            SysmlViewNode view => texts.Concat(view.ExposeMembers.Select(m => m.QualifiedName)).Append(view.ViewDefinitionName),
            SysmlConnectionNode connection => texts.Append(connection.EndpointA).Append(connection.EndpointB),
            SysmlTransitionNode transition => texts.Append(transition.Source).Append(transition.Target),
            SysmlSatisfyNode satisfy => texts.Append(satisfy.RequirementName).Append(satisfy.SubjectName),
            SysmlDependencyNode dependency => texts.Concat(dependency.FromNames).Concat(dependency.ToNames),
            _ => texts,
        };
    }

    /// <summary>
    ///     Collects the terminal tokens of <paramref name="root"/> in source order, optionally
    ///     stopping after the declaration header's first <c>{</c> or <c>;</c>.
    /// </summary>
    private static List<Antlr4.Runtime.IToken> CollectTokens(Antlr4.Runtime.Tree.IParseTree root, bool headerOnly)
    {
        var tokens = new List<Antlr4.Runtime.IToken>();
        var pending = new Stack<Antlr4.Runtime.Tree.IParseTree>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is Antlr4.Runtime.Tree.ITerminalNode terminal)
            {
                var token = terminal.Symbol;
                if (token.Type == Antlr4.Runtime.TokenConstants.EOF)
                {
                    continue;
                }

                tokens.Add(token);
                if (headerOnly && token.Text is "{" or ";")
                {
                    break;
                }

                continue;
            }

            for (var i = current.ChildCount - 1; i >= 0; i--)
            {
                pending.Push(current.GetChild(i));
            }
        }

        return tokens;
    }

    /// <summary>
    ///     Finds the first run of consecutive <paramref name="tokens"/> whose texts concatenate to
    ///     <paramref name="reference"/> and returns the position of its first token. A run that is
    ///     a whole reference (not preceded or followed by a <c>::</c> or <c>.</c> qualifier token)
    ///     is preferred over one embedded in a longer qualified name.
    /// </summary>
    private static SourcePosition? FindReference(
        List<Antlr4.Runtime.IToken> tokens, string reference, HashSet<int> declarationTokens)
    {
        // A reference spelled like the declaring element's own name (`part def A :> A;`,
        // `attribute x redefines x;`) is located at the reference, not at the declaration
        // identifier; the declaration tokens are only used when nothing else matches (an implied
        // name taken from the reference itself, e.g. `part :>> x;`).
        return FindReferenceRun(tokens, reference, declarationTokens)
               ?? FindReferenceRun(tokens, reference, []);
    }

    /// <summary>
    ///     Returns the index of the first token spelling the declaration's own name and the index of
    ///     the token inside its <c>&lt;short name&gt;</c> — the identification tokens, which are the
    ///     declaration rather than a reference to it.
    /// </summary>
    private static HashSet<int> DeclarationTokenIndexes(List<Antlr4.Runtime.IToken> tokens, SysmlNode node)
    {
        var indexes = new HashSet<int>();
        if (!string.IsNullOrEmpty(node.Name))
        {
            var quoted = $"'{node.Name}'";
            var index = tokens.FindIndex(t => t.Text == node.Name || t.Text == quoted);
            if (index >= 0)
            {
                indexes.Add(index);
            }
        }

        // The short name is assigned to the node only after the node is built, so it is
        // recognized by its tokens (`<` name `>`) rather than by node.ShortName.
        for (var i = 0; i + 2 < tokens.Count; i++)
        {
            if (tokens[i].Text == "<" && tokens[i + 2].Text == ">")
            {
                indexes.Add(i + 1);
                break;
            }
        }

        return indexes;
    }

    /// <summary>
    ///     Finds the first run of tokens spelling <paramref name="reference"/> whose first token is
    ///     not in <paramref name="skipStarts"/>.
    /// </summary>
    private static SourcePosition? FindReferenceRun(
        List<Antlr4.Runtime.IToken> tokens, string reference, HashSet<int> skipStarts)
    {
        SourcePosition? embedded = null;
        for (var start = 0; start < tokens.Count; start++)
        {
            if (skipStarts.Contains(start))
            {
                continue;
            }

            var consumed = 0;
            var end = start;
            while (end < tokens.Count && consumed < reference.Length)
            {
                var text = tokens[end].Text;
                if (text.Length == 0 || consumed + text.Length > reference.Length ||
                    string.CompareOrdinal(reference, consumed, text, 0, text.Length) != 0)
                {
                    break;
                }

                consumed += text.Length;
                end++;
            }

            if (consumed != reference.Length)
            {
                continue;
            }

            var position = new SourcePosition(tokens[start].Line, tokens[start].Column + 1);
            var whole = (start == 0 || !IsQualifier(tokens[start - 1])) &&
                        (end == tokens.Count || !IsQualifier(tokens[end]));
            if (whole)
            {
                return position;
            }

            embedded ??= position;
        }

        return embedded;
    }

    /// <summary>Returns whether <paramref name="token"/> joins the parts of a qualified name or feature chain.</summary>
    private static bool IsQualifier(Antlr4.Runtime.IToken token) => token.Text is "::" or ".";

    /// <inheritdoc/>
    public override SysmlNode? VisitPackage(SysMLv2Parser.PackageContext context)
    {
        var name = GetDeclaredName(context.packageDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);

        _namespaceStack.Add(name);
        var (children, annotations) = CollectBodyElements(context.packageBody()?.packageBodyElement() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlPackageNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitLibraryPackage(SysMLv2Parser.LibraryPackageContext context)
    {
        var name = GetDeclaredName(context.packageDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);

        _namespaceStack.Add(name);
        var (children, annotations) = CollectBodyElements(context.packageBody()?.packageBodyElement() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlPackageNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAnnotatingElement(SysMLv2Parser.AnnotatingElementContext context)
    {
        if (context.comment() is { } comment)
        {
            return new AnnotationCapture
            {
                Annotation = new SysmlAnnotation(
                    SysmlAnnotationKind.Comment,
                    ExtractCommentText(comment.REGULAR_COMMENT())),
            };
        }

        if (context.documentation() is { } documentation)
        {
            return new AnnotationCapture
            {
                Annotation = new SysmlAnnotation(
                    SysmlAnnotationKind.Documentation,
                    ExtractCommentText(documentation.REGULAR_COMMENT())),
            };
        }

        // metadataFeature: build a SysmlMetadataNode capturing the annotating type reference and
        // any literal attribute values assigned in its body. textualRepresentation remains out of
        // scope for this unit, preserving the existing drop behavior.
        if (context.metadataFeature() is { } metadataFeature)
        {
            return BuildMetadataNode(metadataFeature);
        }

        return base.VisitAnnotatingElement(context);
    }

    /// <summary>
    ///     Builds a <see cref="SysmlMetadataNode"/> from a <c>metadataFeature</c> parse (the
    ///     <c>{@Type{attr = value;}}</c> / bare <c>@Type;</c> forms), capturing the annotating
    ///     type's raw reference text and any literal (boolean/number/string) attribute values
    ///     assigned directly in its body. Non-literal value expressions are captured as raw text
    ///     with <see cref="MetadataAttributeValueKind.Unsupported"/> — never evaluated, per the
    ///     Phase 1 construct boundary (see ROADMAP.md).
    /// </summary>
    private static SysmlMetadataNode BuildMetadataNode(SysMLv2Parser.MetadataFeatureContext context)
    {
        var typeReference = context.metadataFeatureDeclaration()?.ownedFeatureTyping()?.GetText() ?? string.Empty;

        var attributes = new List<MetadataAttributeValue>();
        foreach (var element in context.metadataBody()?.metadataBodyElement() ?? [])
        {
            var feature = element.metadataBodyFeatureMember()?.metadataBodyFeature();
            var name = feature?.ownedRedefinition()?.GetText();
            var valueExpr = feature?.valuePart()?.featureValue()?.ownedExpression();
            if (string.IsNullOrEmpty(name) || valueExpr is null)
            {
                continue;
            }

            attributes.Add(BuildMetadataAttributeValue(name, valueExpr));
        }

        return new SysmlMetadataNode
        {
            TypeReference = typeReference,
            Attributes = attributes,
        };
    }

    /// <summary>
    ///     Classifies a metadata attribute's assigned value expression as a scalar literal
    ///     (boolean/number/string) when possible, or as
    ///     <see cref="MetadataAttributeValueKind.Unsupported"/> (raw text preserved, never
    ///     evaluated) for any other value expression shape.
    /// </summary>
    private static MetadataAttributeValue BuildMetadataAttributeValue(
        string name, SysMLv2Parser.OwnedExpressionContext expr)
    {
        var raw = expr.GetText();
        var literal = expr.baseExpression()?.literalExpression();

        if (literal?.literalBoolean() is { } boolLiteral)
        {
            return new MetadataAttributeValue(
                name, MetadataAttributeValueKind.Boolean, raw, BooleanValue: boolLiteral.TRUE() is not null);
        }

        if (literal?.literalString() is { } stringLiteral)
        {
            var text = stringLiteral.GetText();
            var unquoted = text.Length >= 2 ? text[1..^1] : text;
            return new MetadataAttributeValue(name, MetadataAttributeValueKind.String, raw, StringValue: unquoted);
        }

        if (literal?.literalInteger() is { } integerLiteral &&
            double.TryParse(integerLiteral.GetText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
        {
            return new MetadataAttributeValue(name, MetadataAttributeValueKind.Number, raw, NumberValue: iv);
        }

        if (literal?.literalReal() is { } realLiteral &&
            double.TryParse(realLiteral.GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rv))
        {
            return new MetadataAttributeValue(name, MetadataAttributeValueKind.Number, raw, NumberValue: rv);
        }

        return new MetadataAttributeValue(name, MetadataAttributeValueKind.Unsupported, raw);
    }

    /// <summary>
    ///     Strips the <c>/*</c>/<c>//*</c> opening delimiter and trailing <c>*/</c> closing
    ///     delimiter from a <c>REGULAR_COMMENT</c> token's text, preserving all interior
    ///     whitespace, newlines, and bullet characters verbatim.
    /// </summary>
    private static string ExtractCommentText(Antlr4.Runtime.Tree.ITerminalNode? token)
    {
        var raw = token?.GetText() ?? string.Empty;
        if (raw.StartsWith("//*", StringComparison.Ordinal))
        {
            raw = raw[3..];
        }
        else if (raw.StartsWith("/*", StringComparison.Ordinal))
        {
            raw = raw[2..];
        }

        if (raw.EndsWith("*/", StringComparison.Ordinal))
        {
            raw = raw[..^2];
        }

        return raw;
    }

    /// <summary>
    ///     Sentinel node used to carry a captured <see cref="SysmlAnnotation"/> up through the
    ///     generic ANTLR <c>Visit</c> pipeline. Never appears in a real
    ///     <see cref="SysmlNode.Children"/> list — the collection helpers below always
    ///     intercept it and route its <see cref="Annotation"/> into the owning node's
    ///     <see cref="SysmlNode.Annotations"/> list instead.
    /// </summary>
    private sealed class AnnotationCapture : SysmlNode
    {
        public required SysmlAnnotation Annotation { get; init; }
    }

    /// <summary>
    ///     Sentinel node used to carry more than one real <see cref="SysmlNode"/> up through the
    ///     generic ANTLR <c>Visit</c> pipeline from a single grammar alternative that actually
    ///     produces several sibling AST nodes (e.g. a <c>stateBodyItem</c>'s attached-transition
    ///     shapes: the preceding state/entry-action usage plus one <see cref="SysmlTransitionNode"/>
    ///     per attached transition; or an <c>actionBodyItem</c>'s analogous combined-succession
    ///     shapes: <c>(sourceSuccessionMember)? actionBehaviorMember
    ///     (actionTargetSuccessionMember)*</c> and <c>initialNodeMember
    ///     (actionTargetSuccessionMember)*</c>). Never appears in a real
    ///     <see cref="SysmlNode.Children"/> list — <see cref="CollectChildren"/> always intercepts
    ///     it and flattens its <see cref="Nodes"/> into the owning node's children instead,
    ///     mirroring how <see cref="AnnotationCapture"/> is intercepted and routed into
    ///     <see cref="SysmlNode.Annotations"/> rather than becoming a child itself.
    /// </summary>
    private sealed class MultiNodeCapture : SysmlNode
    {
        public required IReadOnlyList<SysmlNode> Nodes { get; init; }
    }

    /// <summary>
    ///     Monotonically-increasing counter used to synthesize a unique internal
    ///     <c>$&lt;keyword&gt;&lt;n&gt;</c> name for an anonymous control/accept/send node (see
    ///     <see cref="BuildActionNodeFeature"/>). A synthetic name is required (rather than leaving
    ///     <see cref="SysmlNode.Name"/> null, as plain anonymous actions do) because anonymous
    ///     <c>fork</c>/<c>decide</c>/<c>send</c> nodes are the dominant idiom in the real OMG
    ///     corpus and must still render as a distinct shape and act as the implicit
    ///     <see cref="SysmlTransitionNode.Source"/> of their attached successions.
    /// </summary>
    private int _anonymousNodeCounter;

    /// <summary>
    ///     Tracks the name of the most recently established "flow position" while
    ///     <see cref="CollectActionBodyChildren"/> iterates an action body's <c>actionBodyItem</c>s
    ///     in source order, so <see cref="VisitActionBodyItem"/> can resolve the implicit
    ///     <c>Source</c> of a leading bare <c>then</c> (<c>sourceSuccessionMember</c>) on a
    ///     subsequent sibling. The grammar's <c>sourceSuccessionMember: THEN sourceSuccession</c>
    ///     carries no name at all (it is just the <c>THEN</c> token plus an always-empty
    ///     <c>sourceEnd</c>) — its meaning is "this node's incoming edge comes from whatever
    ///     action/node immediately precedes it in the same enclosing action body." That identity
    ///     can only come from order-sensitive traversal state maintained by the caller that
    ///     iterates the sibling <c>actionBodyItem</c>s, not from anything inside the grammar node
    ///     itself. Null outside an active <see cref="CollectActionBodyChildren"/> call, or when the
    ///     preceding sibling did not establish a nameable flow position (e.g. the very first item
    ///     in the body).
    /// </summary>
    private string? _actionBodyPreviousNodeName;

    /// <inheritdoc/>
    public override SysmlNode? VisitPartDefinition(SysMLv2Parser.PartDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "part def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAttributeDefinition(SysMLv2Parser.AttributeDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "attribute def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitItemDefinition(SysMLv2Parser.ItemDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "item def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitPortDefinition(SysMLv2Parser.PortDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "port def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConnectionDefinition(SysMLv2Parser.ConnectionDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "connection def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAllocationDefinition(SysMLv2Parser.AllocationDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "allocation def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitFlowDefinition(SysMLv2Parser.FlowDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "flow def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitOccurrenceDefinition(SysMLv2Parser.OccurrenceDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "occurrence def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitIndividualDefinition(SysMLv2Parser.IndividualDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "individual def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRenderingDefinition(SysMLv2Parser.RenderingDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "rendering def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitMetadataDefinition(SysMLv2Parser.MetadataDefinitionContext context)
    {
        return BuildDefinitionNode(context.definition(), "metadata def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitEnumerationDefinition(SysMLv2Parser.EnumerationDefinitionContext context)
    {
        var decl = context.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());

        // Collect the enumeration body (literal values) as children, mirroring VisitStateDefinition.
        // enumerationBody is NOT a single wrapping rule (unlike stateDefBody/definitionBody/
        // requirementBody) — it directly alternates (annotatingMember | enumerationUsageMember)*
        // with two distinct context types and no shared wrapper, so recovering source-order
        // interleaving of annotations vs. literal values requires walking the raw ANTLR child
        // list (see CollectEnumerationBodyChildren) rather than passing a single typed accessor
        // array to CollectChildren, as every other body construct in this file does.
        _namespaceStack.Add(name);
        var (children, annotations) = CollectEnumerationBodyChildren(context.enumerationBody());
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "enum def",
            SupertypeNames = supertypeNames,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <summary>
    ///     Collects an <c>enumerationBody</c>'s child nodes (literal values and any interleaved
    ///     comment/documentation annotations), preserving source order. <c>enumerationBody</c> is
    ///     a narrow, deliberate exception to this file's usual "pass a single typed accessor array
    ///     to <see cref="CollectChildren"/>" convention: its grammar rule
    ///     (<c>LBRACE ( annotatingMember | enumerationUsageMember )* RBRACE</c>) alternates two
    ///     distinct child context types with no shared wrapping rule (unlike
    ///     <c>stateBodyItem</c>/<c>definitionBodyItem</c>/<c>requirementBodyItem</c>, which are
    ///     always wrapped in one rule per body element), so there is no single typed accessor that
    ///     yields the interleaved sequence. Walking <c>context.children</c> directly (the raw
    ///     ANTLR child list, filtering out the <c>LBRACE</c>/<c>RBRACE</c> terminals) recovers that
    ///     order without inventing a new collection mechanism — each remaining child is delegated
    ///     to the existing generic <see cref="CollectChildren(IEnumerable{Antlr4.Runtime.Tree.IParseTree})"/>.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)
        CollectEnumerationBodyChildren(SysMLv2Parser.EnumerationBodyContext? body)
    {
        if (body is null)
        {
            return (Array.Empty<SysmlNode>(), Array.Empty<SysmlAnnotation>());
        }

        var members = new List<Antlr4.Runtime.Tree.IParseTree>();
        for (var i = 0; i < body.ChildCount; i++)
        {
            // Filter to ParserRuleContext to drop the LBRACE/RBRACE terminals — only
            // annotatingMember/enumerationUsageMember contexts should reach CollectChildren.
            if (body.GetChild(i) is Antlr4.Runtime.ParserRuleContext member)
            {
                members.Add(member);
            }
        }

        return CollectChildren(members);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitEnumeratedValue(SysMLv2Parser.EnumeratedValueContext context)
    {
        // enumeratedValue: ENUM? usage — the exact same usage rule wrapped by
        // VisitEnumerationUsage/VisitPartUsage/etc., so BuildUsageNode is reused as-is. "enum
        // value" (not the bare "enum" keyword used by VisitEnumerationUsage) keeps an `enum def`'s
        // literal-value compartment title ("enum values", via Pluralize's default keyword + "s"
        // fallback) distinct from a definition's enum-typed attribute usages.
        return BuildUsageNode(context.usage(), "enum value");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitInterfaceDefinition(SysMLv2Parser.InterfaceDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "interface def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitActionDefinition(SysMLv2Parser.ActionDefinitionContext context)
    {
        var decl = context.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());

        // Collect the action body (action usages and successions) as children. Action bodies use
        // CollectActionBodyChildren (not the generic CollectChildren) because resolving an
        // implicit leading `then` requires order-sensitive tracking of the preceding sibling's
        // flow position — see VisitActionBodyItem and _actionBodyPreviousNodeName.
        _namespaceStack.Add(name);
        var (children, annotations) = CollectActionBodyChildren(context.actionBody()?.actionBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "action def",
            SupertypeNames = supertypeNames,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitActionUsage(SysMLv2Parser.ActionUsageContext context)
    {
        var name = GetDeclaredName(context.actionUsageDeclaration()?.usageDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var declaration = context.actionUsageDeclaration();
        var specialization = ExtractSpecialization(declaration?.usageDeclaration()?.featureSpecializationPart());
        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = QualifyName(name),
                FeatureKeyword = "action",
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
            },
            declaration?.usageDeclaration(),
            declaration?.valuePart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSuccessionAsUsage(SysMLv2Parser.SuccessionAsUsageContext context)
    {
        // A succession links two action ends: first <source> then <target>.
        var ends = context.connectorEndMember();
        if (ends.Length < 2)
        {
            return null;
        }

        var source = ConnectorEndReference(ends[0]);
        var target = ConnectorEndReference(ends[1]);
        if (source is null || target is null)
        {
            return null;
        }

        return new SysmlTransitionNode
        {
            Source = source,
            Target = target,
        };
    }

    /// <summary>
    ///     Dispatches a single <c>actionBodyItem</c> alternative to the appropriate builder logic.
    ///     Mirrors <see cref="VisitStateBodyItem"/>'s dispatch shape. <c>nonBehaviorBodyItem</c> and
    ///     <c>guardedSuccessionMember</c> already produce exactly one AST node via default ANTLR
    ///     visitor dispatch, so they are passed through unchanged (the latter's own internal shape
    ///     is left untouched here, exactly as <see cref="VisitStateBodyItem"/> leaves its own
    ///     untouched alternatives unhandled). The two combined-succession shapes —
    ///     <c>initialNodeMember (actionTargetSuccessionMember)*</c> (e.g. <c>first start;</c> or
    ///     <c>first start then off;</c>) and <c>(sourceSuccessionMember)? actionBehaviorMember
    ///     (actionTargetSuccessionMember)*</c> (e.g. the compact <c>action a1; then a2;</c> idiom) —
    ///     each may produce more than one sibling node (the referenced/behavior node itself, plus
    ///     one <see cref="SysmlTransitionNode"/> per attached target succession whose
    ///     <c>Source</c> is implicit); without this override, ANTLR's default <c>VisitChildren</c>
    ///     aggregation silently discards every child result but the last, dropping the earlier
    ///     nodes and losing successions entirely. The <c>actionBehaviorMember</c> alternative's
    ///     optional leading <c>sourceSuccessionMember</c> (a bare <c>then</c> immediately before
    ///     the node, e.g. <c>action a; then fork f; ...</c>) also synthesizes an additional
    ///     incoming <see cref="SysmlTransitionNode"/> whose <c>Source</c> is
    ///     <see cref="_actionBodyPreviousNodeName"/> — the name of the immediately preceding
    ///     sibling in the enclosing action body, tracked by <see cref="CollectActionBodyChildren"/>
    ///     since the grammar's leading marker itself carries no identity.
    /// </summary>
    public override SysmlNode? VisitActionBodyItem(SysMLv2Parser.ActionBodyItemContext context)
    {
        if (context.nonBehaviorBodyItem() is { } nonBehaviorBodyItem)
        {
            return Visit(nonBehaviorBodyItem);
        }

        if (context.guardedSuccessionMember() is { } guardedSuccessionMember)
        {
            return Visit(guardedSuccessionMember);
        }

        if (context.initialNodeMember() is { } initialNodeMember)
        {
            var targets = context.actionTargetSuccessionMember();
            if (targets.Length == 0)
            {
                // Bare `first start;` carries no attached succession. Unlike state-transition
                // pseudostates, ActionFlowViewLayoutStrategy infers its start/done markers purely
                // from succession topology (no declarative initial-marker concept exists for
                // actions), so there is nothing useful to synthesize here — unchanged from today.
                return null;
            }

            var sourceName = initialNodeMember.qualifiedName()?.GetText();
            var nodes = targets
                .Select(target => (SysmlNode)BuildActionTargetSuccession(sourceName, target.actionTargetSuccession()))
                .ToList();

            return nodes.Count == 1 ? nodes[0] : new MultiNodeCapture { Nodes = nodes };
        }

        if (context.actionBehaviorMember() is { } actionBehaviorMember)
        {
            var behaviorNode = Visit(actionBehaviorMember);
            if (behaviorNode is null)
            {
                return null;
            }

            var targets = context.actionTargetSuccessionMember();
            var hasImplicitSource = context.sourceSuccessionMember() is not null;
            if (!hasImplicitSource && targets.Length == 0)
            {
                return behaviorNode;
            }

            var nodes = new List<SysmlNode>();
            if (hasImplicitSource && _actionBodyPreviousNodeName is { } previousName)
            {
                // A bare leading `then` (sourceSuccessionMember) means this node's incoming edge
                // comes from whatever immediately preceded it in the same enclosing action body.
                // The grammar gives that marker no name, so the source is resolved from the
                // order-sensitive _actionBodyPreviousNodeName tracked by CollectActionBodyChildren.
                // When no previous position is known (e.g. this is the first item in the body), no
                // incoming edge is synthesized rather than fabricating a Source from nothing.
                nodes.Add(new SysmlTransitionNode
                {
                    Source = previousName,
                    Target = behaviorNode.Name,
                });
            }

            nodes.Add(behaviorNode);

            var sourceName = behaviorNode.Name;
            foreach (var target in targets)
            {
                nodes.Add(BuildActionTargetSuccession(sourceName, target.actionTargetSuccession()));
            }

            return nodes.Count == 1 ? nodes[0] : new MultiNodeCapture { Nodes = nodes };
        }

        return null;
    }

    /// <summary>
    ///     Builds the <see cref="SysmlTransitionNode"/> for one <c>actionTargetSuccession</c>
    ///     attached after an action-flow node (e.g. <c>then a2;</c>, <c>if g then a3;</c>, or
    ///     <c>else a4;</c>), whose <c>Source</c> is implicitly the preceding node's name (never
    ///     present in the grammar itself). Covers all three alternatives: <c>targetSuccession</c>
    ///     (unguarded, target via a bare <see cref="SysMLv2Parser.ConnectorEndMemberContext"/>),
    ///     <c>guardedTargetSuccession</c> (<c>if ... then ...</c>, guard captured), and
    ///     <c>defaultTargetSuccession</c> (<c>else ...</c>, no guard expression exists in the
    ///     grammar for this alternative; it is flagged with <see cref="SysmlTransitionNode.IsElse"/>
    ///     so it can be told apart from an unconditional succession). The trailing
    ///     <c>usageBody()</c> is ignored, mirroring <see cref="BuildAttachedTransition"/>'s
    ///     treatment of <c>targetTransitionUsage</c>'s own trailing <c>actionBody()</c>.
    /// </summary>
    private SysmlTransitionNode BuildActionTargetSuccession(
        string? sourceName,
        SysMLv2Parser.ActionTargetSuccessionContext? succession)
    {
        if (succession?.targetSuccession() is { } targetSuccession)
        {
            return new SysmlTransitionNode
            {
                Source = sourceName,
                Target = ConnectorEndReference(targetSuccession.connectorEndMember()),
            };
        }

        if (succession?.guardedTargetSuccession() is { } guardedTargetSuccession)
        {
            return new SysmlTransitionNode
            {
                Source = sourceName,
                Target = ConnectorEndReference(
                    guardedTargetSuccession.transitionSuccessionMember()?.transitionSuccession()?.connectorEndMember()),
                Guard = guardedTargetSuccession.guardExpressionMember()?.ownedExpression()?.GetText(),
                ReferenceOccurrences = CollectReferenceOccurrences(
                    null, guardedTargetSuccession.guardExpressionMember()?.ownedExpression()),
            };
        }

        if (succession?.defaultTargetSuccession() is { } defaultTargetSuccession)
        {
            return new SysmlTransitionNode
            {
                Source = sourceName,
                Target = ConnectorEndReference(
                    defaultTargetSuccession.transitionSuccessionMember()?.transitionSuccession()?.connectorEndMember()),
                IsElse = true,
            };
        }

        return new SysmlTransitionNode { Source = sourceName, Target = null };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitMergeNode(SysMLv2Parser.MergeNodeContext context)
    {
        return BuildActionNodeFeature(context.usageDeclaration(), "merge");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitDecisionNode(SysMLv2Parser.DecisionNodeContext context)
    {
        return BuildActionNodeFeature(context.usageDeclaration(), "decide");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitJoinNode(SysMLv2Parser.JoinNodeContext context)
    {
        return BuildActionNodeFeature(context.usageDeclaration(), "join");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitForkNode(SysMLv2Parser.ForkNodeContext context)
    {
        return BuildActionNodeFeature(context.usageDeclaration(), "fork");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAcceptNode(SysMLv2Parser.AcceptNodeContext context)
    {
        var declaration = context.acceptNodeDeclaration();
        var payload = OriginalTextOrNull(declaration?.acceptParameterPart());
        var node = BuildActionNodeFeature(
            declaration?.actionNodeUsageDeclaration()?.usageDeclaration(),
            "accept",
            payload is null ? null : $"accept {payload}");
        node.ReferenceOccurrences = AcceptDeclarationOccurrences(declaration);
        return node;
    }

    /// <summary>
    ///     Collects the reference occurrences of an <c>accept</c> declaration: the payload type or
    ///     expression, the <c>via</c> target, and the typing of the accept action itself.
    /// </summary>
    private IReadOnlyList<ReferenceOccurrence> AcceptDeclarationOccurrences(
        SysMLv2Parser.AcceptNodeDeclarationContext? declaration) =>
        CollectReferenceOccurrences(
            DeclaredPayloadNames(declaration?.acceptParameterPart()),
            declaration);

    /// <inheritdoc/>
    public override SysmlNode? VisitSendNode(SysMLv2Parser.SendNodeContext context)
    {
        var declaration = context.actionNodeUsageDeclaration()?.usageDeclaration()
            ?? context.actionUsageDeclaration()?.usageDeclaration();
        var payload = OriginalTextSpanOrNull(context.nodeParameterMember(), context.senderReceiverPart());
        var node = BuildActionNodeFeature(declaration, "send", payload is null ? null : $"send {payload}");
        node.ReferenceOccurrences = CollectReferenceOccurrences(
            null, declaration, context.nodeParameterMember(), context.senderReceiverPart());
        return node;
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitTerminateNode(SysMLv2Parser.TerminateNodeContext context)
    {
        return BuildActionNodeFeature(context.actionNodeUsageDeclaration()?.usageDeclaration(), "terminate");
    }

    /// <summary>
    ///     Builds a minimal, deliberately non-behavioral <see cref="SysmlFeatureNode"/> for a
    ///     <c>merge</c>/<c>decide</c>/<c>join</c>/<c>fork</c>/<c>accept</c>/<c>send</c>/<c>terminate</c>
    ///     action-flow node, registering only its (possibly synthesized) name with no children. When
    ///     <paramref name="decl"/> yields no declared name — the dominant real-world idiom for
    ///     <c>fork</c>/<c>decide</c>/<c>send</c> per the OMG training corpus (e.g. <c>then fork;</c>
    ///     immediately followed by several <c>then &lt;name&gt;;</c> successions) — a synthetic
    ///     <c>$&lt;keyword&gt;&lt;n&gt;</c> name is assigned via <see cref="_anonymousNodeCounter"/>
    ///     so the node still renders as a distinct shape and can still act as those successions'
    ///     implicit source; its <see cref="SysmlNode.QualifiedName"/> stays <see langword="null"/>
    ///     in that synthesized case (this is purely local succession-wiring data, never registered
    ///     in the symbol table or referenced across files/scopes). When the node instead has an
    ///     explicitly declared name (e.g. <c>fork buildFork;</c>), it is treated like any other
    ///     named feature in this file: its <see cref="SysmlNode.QualifiedName"/> is populated via
    ///     <see cref="QualifyName"/> so it is registered in the symbol table and correctly subject
    ///     to expose-scope filtering, mirroring <see cref="BuildStateActionFeatureNode"/>. The
    ///     nested <c>actionBody</c>'s internal semantics are deliberately NOT modeled, mirroring
    ///     <see cref="BuildStateActionFeatureNode"/>. <c>assignmentNode</c>/<c>ifNode</c>/
    ///     <c>whileLoopNode</c>/<c>forLoopNode</c> remain an intentional, out-of-scope
    ///     gap — not handled here or anywhere else in <see cref="AstBuilder"/>.
    /// </summary>
    /// <param name="decl">The node's usage declaration, or null when it declares none.</param>
    /// <param name="keyword">The node keyword stored as <see cref="SysmlFeatureNode.FeatureKeyword"/>.</param>
    /// <param name="actionText">The payload text to expose as <see cref="SysmlFeatureNode.ActionText"/>, or null.</param>
    private SysmlFeatureNode BuildActionNodeFeature(
        SysMLv2Parser.UsageDeclarationContext? decl,
        string keyword,
        string? actionText = null)
    {
        var declaredName = GetDeclaredName(decl?.identification());
        var name = declaredName ?? $"${keyword}{_anonymousNodeCounter++}";

        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = declaredName is not null ? QualifyName(name) : null,
                FeatureKeyword = keyword,
                Children = Array.Empty<SysmlNode>(),
                Annotations = Array.Empty<SysmlAnnotation>(),
                ActionText = actionText,
            },
            decl,
            null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitStateDefinition(SysMLv2Parser.StateDefinitionContext context)
    {
        var decl = context.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());

        // Collect the state body (state usages and transitions) as children.
        _namespaceStack.Add(name);
        var (children, annotations) = CollectChildren(context.stateDefBody()?.stateBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "state def",
            SupertypeNames = supertypeNames,
            Children = children,
            Annotations = annotations,
            IsParallel = context.stateDefBody()?.PARALLEL() is not null,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitCalculationDefinition(SysMLv2Parser.CalculationDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "calc def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConstraintDefinition(SysMLv2Parser.ConstraintDefinitionContext context)
    {
        var decl = context.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());

        // Synthesize one child SysmlFeatureNode (Name = null, FeatureKeyword = "constraint")
        // representing the definition's own calculationBody expression, so the definition's
        // constraint expression renders through the same Children-driven compartment mechanism
        // (GeneralViewLayoutStrategy.BuildCompartments) used for every other feature-kind
        // compartment, rather than adding a second, definition-level-only expression-text field
        // and a matching special-cased rendering path.
        var expressionText = context.calculationBody()?.GetText();
        IReadOnlyList<SysmlNode> children = expressionText is null
            ? Array.Empty<SysmlNode>()
            :
            [
                new SysmlFeatureNode
                {
                    FeatureKeyword = "constraint",
                    ExpressionText = expressionText,
                    ReferenceOccurrences = ConstraintBodyOccurrences(context.calculationBody()),
                },
            ];

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "constraint def",
            SupertypeNames = supertypeNames,
            Children = children,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRequirementDefinition(SysMLv2Parser.RequirementDefinitionContext context)
    {
        var decl = context.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());
        var body = context.requirementBody();

        // Collect the requirement body (subject/constraint/actor/stakeholder members, plus any
        // ordinary definitionBodyItem alternatives) as children, mirroring VisitStateDefinition.
        // requirementBody IS a single wrapping rule (requirementBodyItem), unlike enumerationBody,
        // so the generic CollectChildren works unmodified here.
        _namespaceStack.Add(name);
        var (children, annotations) = CollectChildren(body?.requirementBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        // VerifiedRequirementNames is still populated by the separate manual-walk mechanism
        // (FindVerificationMembers), unaffected by now also collecting Children.
        var verifiedRequirementNames = body is not null
            ? FindVerificationMembers(body)
            : Array.Empty<string>();

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "requirement def",
            SupertypeNames = supertypeNames,
            VerifiedRequirementNames = verifiedRequirementNames,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConcernDefinition(SysMLv2Parser.ConcernDefinitionContext context)
    {
        // concernDefinition's body is also requirementBody (grammar-confirmed identical shape to
        // requirementDefinition), so it gets the same full body-collecting treatment.
        var decl = context.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());
        var body = context.requirementBody();

        _namespaceStack.Add(name);
        var (children, annotations) = CollectChildren(body?.requirementBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        var verifiedRequirementNames = body is not null
            ? FindVerificationMembers(body)
            : Array.Empty<string>();

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "concern def",
            SupertypeNames = supertypeNames,
            VerifiedRequirementNames = verifiedRequirementNames,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitCaseDefinition(SysMLv2Parser.CaseDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "case def", context.caseBody());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAnalysisCaseDefinition(SysMLv2Parser.AnalysisCaseDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "analysis def", context.caseBody());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitVerificationCaseDefinition(SysMLv2Parser.VerificationCaseDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "verification def", context.caseBody());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitUseCaseDefinition(SysMLv2Parser.UseCaseDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "use case def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitCaseUsage(SysMLv2Parser.CaseUsageContext context)
    {
        return BuildCaseUsageNode(context.constraintUsageDeclaration(), "case", context.caseBody());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAnalysisCaseUsage(SysMLv2Parser.AnalysisCaseUsageContext context)
    {
        return BuildCaseUsageNode(context.constraintUsageDeclaration(), "analysis", context.caseBody());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitVerificationCaseUsage(SysMLv2Parser.VerificationCaseUsageContext context)
    {
        return BuildCaseUsageNode(context.constraintUsageDeclaration(), "verification", context.caseBody());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitUseCaseUsage(SysMLv2Parser.UseCaseUsageContext context)
    {
        return BuildCaseUsageNode(context.constraintUsageDeclaration(), "use case", null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitCalculationUsage(SysMLv2Parser.CalculationUsageContext context)
    {
        var declaration = context.actionUsageDeclaration();
        var name = GetDeclaredName(declaration?.usageDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var specialization = ExtractSpecialization(declaration?.usageDeclaration()?.featureSpecializationPart());
        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = QualifyName(name),
                FeatureKeyword = "calc",
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
            },
            declaration?.usageDeclaration(),
            declaration?.valuePart());
    }

    /// <summary>
    ///     Builds the <see cref="SysmlFeatureNode"/> of a <c>case</c>/<c>analysis</c>/<c>verification</c>/
    ///     <c>use case</c> usage (grammar shape <c>occurrenceUsagePrefix KEYWORD
    ///     constraintUsageDeclaration caseBody</c>), capturing its declared name, feature typing and
    ///     multiplicity, and (when a body is supplied) its nested
    ///     verified-requirement names. Mirrors the definition-side builders, which do not collect the
    ///     case body's other members; an unnamed usage yields no node, like the other usage builders.
    /// </summary>
    /// <param name="declaration">The usage's declaration, or null when it has none.</param>
    /// <param name="keyword">The feature keyword (e.g. <c>"analysis"</c>).</param>
    /// <param name="body">The case body to scan for <c>verify</c> members, or null to skip the scan.</param>
    private SysmlFeatureNode? BuildCaseUsageNode(
        SysMLv2Parser.ConstraintUsageDeclarationContext? declaration,
        string keyword,
        Antlr4.Runtime.Tree.IParseTree? body)
    {
        var decl = declaration?.usageDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var specialization = ExtractSpecialization(decl?.featureSpecializationPart());
        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = QualifyName(name),
                FeatureKeyword = keyword,
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
                VerifiedRequirementNames = body is not null
                    ? FindVerificationMembers(body)
                    : Array.Empty<string>(),
            },
            decl,
            declaration?.valuePart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitPartUsage(SysMLv2Parser.PartUsageContext context)
    {
        return BuildUsageNode(context.usage(), "part");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitPortUsage(SysMLv2Parser.PortUsageContext context)
    {
        return BuildUsageNode(context.usage(), "port");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAttributeUsage(SysMLv2Parser.AttributeUsageContext context)
    {
        return BuildUsageNode(context.usage(), "attribute");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitItemUsage(SysMLv2Parser.ItemUsageContext context)
    {
        return BuildUsageNode(context.usage(), "item");
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     A <c>rendering</c> usage (<c>rendering asTreeDiagram : GraphicalRendering;</c>, as the
    ///     Views library declares its renderings) is a named, referenceable member: a view's
    ///     <c>render asTreeDiagram;</c> resolves to it, so it must be a declaration.
    /// </remarks>
    public override SysmlNode? VisitRenderingUsage(SysMLv2Parser.RenderingUsageContext context)
    {
        return BuildUsageNode(context.usage(), "rendering");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitReferenceUsage(SysMLv2Parser.ReferenceUsageContext context)
    {
        return BuildUsageNode(context.usage(), "ref");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitEnumerationUsage(SysMLv2Parser.EnumerationUsageContext context)
    {
        return BuildUsageNode(context.usage(), "enum");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitOccurrenceUsage(SysMLv2Parser.OccurrenceUsageContext context)
    {
        return BuildUsageNode(context.usage(), "occurrence");
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Only the declaring form <c>event occurrence name [: Type];</c> is captured, as a
    ///     <see cref="SysmlFeatureNode"/> with the keyword <c>event occurrence</c>; the Sequence view
    ///     orders messages by the declaration order of these children. The referencing form
    ///     <c>event someFeature;</c> declares nothing new and is not captured.
    /// </remarks>
    public override SysmlNode? VisitEventOccurrenceUsage(SysMLv2Parser.EventOccurrenceUsageContext context)
    {
        var decl = context.usageDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (decl is null || name is null)
        {
            return null;
        }

        var node = new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = QualifyName(name),
            FeatureKeyword = "event occurrence",
            FeatureTyping = ExtractFeatureTyping(decl.featureSpecializationPart()),
            Multiplicity = ExtractMultiplicity(decl.featureSpecializationPart()),
        };

        return ApplyFeatureNotation(node, decl, valuePart: null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConnectionUsage(SysMLv2Parser.ConnectionUsageContext context)
    {
        var name = GetDeclaredName(context.usageDeclaration()?.identification());
        var (endA, endB) = ExtractConnectorEnds(context.connectorPart());

        return BuildConnectionNode(name, "connection", endA, endB, payloadText: null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitInterfaceUsage(SysMLv2Parser.InterfaceUsageContext context)
    {
        // An interface usage connects two ports (interface i connect a.p to b.q). Its body
        // (interface ends, nested features) is not modeled: the node carries the two ends only,
        // like a connection usage, so a view can draw the interface as a connector.
        var decl = context.interfaceUsageDeclaration();
        var name = GetDeclaredName(decl?.usageDeclaration()?.identification());
        var ends = decl?.interfacePart()?.binaryInterfacePart()?.interfaceEndMember();
        var endA = ends is { Length: >= 2 } ? ConnectorEndDetail(ends[0].interfaceEnd()) : default;
        var endB = ends is { Length: >= 2 } ? ConnectorEndDetail(ends[1].interfaceEnd()) : default;

        return BuildConnectionNode(name, "interface", endA, endB, payloadText: null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitFlowUsage(SysMLv2Parser.FlowUsageContext context)
    {
        // flow [name] [of Payload] from a.out to b.in; or flow a.out to b.in; The flow ends are
        // plain feature chains (no role names or multiplicities), unlike connector ends.
        var decl = context.flowDeclaration();
        var identification = decl?.featureDeclaration()?.featureIdentification();
        var name = identification is not null
            ? GetDeclaredName(identification)
            : GetDeclaredName(decl?.usageDeclaration()?.identification());

        var ends = decl?.flowEndMember();
        var endA = ends is { Length: >= 2 } ? new ConnectorEndInfo(ends[0].flowEnd()?.GetText(), null, null) : default;
        var endB = ends is { Length: >= 2 } ? new ConnectorEndInfo(ends[1].flowEnd()?.GetText(), null, null) : default;
        var payload = OriginalTextOrNull(decl?.payloadFeatureMember())
            ?? OriginalTextOrNull(decl?.flowPayloadFeatureMember());

        return BuildConnectionNode(name, "flow", endA, endB, payload);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitMessage(SysMLv2Parser.MessageContext context)
    {
        var decl = context.messageDeclaration();
        var name = GetDeclaredName(decl?.usageDeclaration()?.identification());

        // A message links two events: from <source> to <target>.
        string? from = null;
        string? to = null;
        var events = decl?.messageEventMember();
        if (events is { Length: >= 2 })
        {
            from = events[0].messageEvent()?.ownedReferenceSubsetting()?.GetText();
            to = events[1].messageEvent()?.ownedReferenceSubsetting()?.GetText();
        }

        return BuildConnectionNode(
            name,
            "message",
            new ConnectorEndInfo(from, null, null),
            new ConnectorEndInfo(to, null, null),
            OriginalTextOrNull(decl?.flowPayloadFeatureMember()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAllocationUsage(SysMLv2Parser.AllocationUsageContext context)
    {
        // allocationUsageDeclaration.connectorPart() returns the exact same ConnectorPartContext
        // type as connectionUsage.connectorPart(), so the existing ExtractConnectorEnds helper is
        // reusable as-is for allocate's two ends.
        var decl = context.allocationUsageDeclaration();
        var name = GetDeclaredName(decl?.usageDeclaration()?.identification());
        var (endA, endB) = ExtractConnectorEnds(decl?.connectorPart());

        return BuildConnectionNode(name, "allocation", endA, endB, payloadText: null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitBindingConnectorAsUsage(SysMLv2Parser.BindingConnectorAsUsageContext context)
    {
        // Only the common "bind A = B;" (bindingConnectorAsUsage) shape is supported; the longer
        // bindingConnector/typeBody form has zero corpus evidence and is a documented limitation.
        var name = GetDeclaredName(context.usageDeclaration()?.identification());
        var ends = context.connectorEndMember();
        var endA = ends.Length > 0 ? ConnectorEndDetail(ends[0].connectorEnd()) : default;
        var endB = ends.Length > 1 ? ConnectorEndDetail(ends[1].connectorEnd()) : default;

        return BuildConnectionNode(name, "binding", endA, endB, payloadText: null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSatisfyRequirementUsage(SysMLv2Parser.SatisfyRequirementUsageContext context)
    {
        // Prefer the ownedReferenceSubsetting form (satisfy <ref> ...). The "REQUIREMENT
        // usageDeclaration?" form declares a requirement usage (satisfy requirement r : R by s);
        // the name it declares is not a reference, so the requirement satisfied is the usage's
        // typing (R) and only a declaration without typing falls back to the declared name.
        var declaration = context.usageDeclaration();
        var requirementName = context.ownedReferenceSubsetting()?.GetText()
            ?? ExtractFeatureTyping(declaration?.featureSpecializationPart())
            ?? GetDeclaredName(declaration?.identification())
            ?? declaration?.GetText();

        var subjectName = context.satisfactionSubjectMember()?.GetText();

        return new SysmlSatisfyNode
        {
            RequirementName = requirementName,
            SubjectName = subjectName,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitDependency(SysMLv2Parser.DependencyContext context)
    {
        // Split the flat qualifiedName() list into from/to by comparing each name's start token
        // index against TO()'s token index: everything before TO is a "from" (client) name,
        // everything after is a "to" (supplier) name. The optional FROM keyword may be omitted
        // (e.g. "dependency z to x, y;"), in which case the single qualifiedName captured before
        // TO is still correctly classified as the (implicit) "from" name by this position check.
        var toTokenIndex = context.TO()?.Symbol.TokenIndex ?? int.MaxValue;
        var fromNames = new List<string>();
        var toNames = new List<string>();
        foreach (var qualifiedName in context.qualifiedName())
        {
            (qualifiedName.Start.TokenIndex < toTokenIndex ? fromNames : toNames).Add(qualifiedName.GetText());
        }

        return new SysmlDependencyNode
        {
            FromNames = fromNames,
            ToNames = toNames,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRequirementUsage(SysMLv2Parser.RequirementUsageContext context)
    {
        // Name/qualified-name capture, so that named requirement usages (the common real-world
        // satisfy/verify target pattern) become resolvable symbols. Also collects the requirement
        // body's subject/constraint/actor/stakeholder compartment members as Children — the
        // dominant real-corpus idiom (a named requirement usage specializing a requirement def and
        // supplying its own subject/assume-constraint body) nests these inside a requirement
        // *usage*, not only inside a requirement definition's own body, so both must be collected
        // for the "compartment depth" goal to be met for the corpus's idiomatic shape.
        var name = GetDeclaredName(context.constraintUsageDeclaration()?.usageDeclaration()?.identification());
        var body = context.requirementBody();

        if (name is not null)
        {
            _namespaceStack.Add(name);
        }

        var (children, annotations) = CollectChildren(body?.requirementBodyItem() ?? []);

        if (name is not null)
        {
            _namespaceStack.RemoveAt(_namespaceStack.Count - 1);
        }

        var verifiedRequirementNames = body is not null
            ? FindVerificationMembers(body)
            : Array.Empty<string>();

        var specialization = ExtractSpecialization(
            context.constraintUsageDeclaration()?.usageDeclaration()?.featureSpecializationPart());

        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = name is not null ? QualifyName(name) : null,
                FeatureKeyword = "requirement",
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
                VerifiedRequirementNames = verifiedRequirementNames,
                Children = children,
                Annotations = annotations,
            },
            context.constraintUsageDeclaration()?.usageDeclaration(),
            context.constraintUsageDeclaration()?.valuePart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConcernUsage(SysMLv2Parser.ConcernUsageContext context)
    {
        // concernUsage: occurrenceUsagePrefix CONCERN constraintUsageDeclaration requirementBody —
        // identical shape to requirementUsage, so the same name + VerifiedRequirementNames +
        // Children capture applies here (see VisitRequirementUsage).
        var name = GetDeclaredName(context.constraintUsageDeclaration()?.usageDeclaration()?.identification());
        var body = context.requirementBody();

        if (name is not null)
        {
            _namespaceStack.Add(name);
        }

        var (children, annotations) = CollectChildren(body?.requirementBodyItem() ?? []);

        if (name is not null)
        {
            _namespaceStack.RemoveAt(_namespaceStack.Count - 1);
        }

        var verifiedRequirementNames = body is not null
            ? FindVerificationMembers(body)
            : Array.Empty<string>();

        var specialization = ExtractSpecialization(
            context.constraintUsageDeclaration()?.usageDeclaration()?.featureSpecializationPart());

        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = name is not null ? QualifyName(name) : null,
                FeatureKeyword = "concern",
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
                VerifiedRequirementNames = verifiedRequirementNames,
                Children = children,
                Annotations = annotations,
            },
            context.constraintUsageDeclaration()?.usageDeclaration(),
            context.constraintUsageDeclaration()?.valuePart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSubjectUsage(SysMLv2Parser.SubjectUsageContext context)
    {
        // subjectUsage: SUBJECT usageExtensionKeyword* usage — reuses the same usage rule as
        // VisitPartUsage/VisitEnumerationUsage/etc., so BuildUsageNode is reused as-is. No
        // override needed for VisitSubjectMember itself — default ANTLR VisitChildren dispatch
        // already descends through memberPrefix (null-returning) into subjectUsage.
        return BuildUsageNode(context.usage(), "subject");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitActorUsage(SysMLv2Parser.ActorUsageContext context)
    {
        // actorUsage: ACTOR usageExtensionKeyword* usage — same trivial reuse as VisitSubjectUsage.
        return BuildUsageNode(context.usage(), "actor");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitStakeholderUsage(SysMLv2Parser.StakeholderUsageContext context)
    {
        // stakeholderUsage: STAKEHOLDER usageExtensionKeyword* usage — same trivial reuse.
        return BuildUsageNode(context.usage(), "stakeholder");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRequirementConstraintMember(SysMLv2Parser.RequirementConstraintMemberContext context)
    {
        // Must override at the member level (not requirementConstraintUsage) because the emitted
        // FeatureKeyword ("assume constraint" vs. "require constraint") depends on the sibling
        // requirementKind (ASSUME/REQUIRE token), which only this member context exposes.
        var keyword = context.requirementKind()?.REQUIRE() is not null ? "require constraint" : "assume constraint";
        return BuildConstraintFeatureNode(context.requirementConstraintUsage(), keyword);
    }

    /// <summary>
    ///     Builds a constraint-kind <see cref="SysmlFeatureNode"/> (<c>FeatureKeyword</c> of
    ///     <c>"assume constraint"</c>/<c>"require constraint"</c>/<c>"constraint"</c>) from a
    ///     <c>requirementConstraintUsage</c>, handling both grammar alternatives.
    /// </summary>
    /// <remarks>
    ///     Alternative 1 (<c>ownedReferenceSubsetting featureSpecializationPart? requirementBody</c>)
    ///     is a <em>reference</em> to an existing named constraint (e.g. <c>assume
    ///     myOtherConstraint;</c>): the raw reference text is captured into
    ///     <see cref="SysmlFeatureNode.ExpressionText"/> — a raw reference, not an evaluated
    ///     expression, when this constraint form is used — and its own nested
    ///     <c>requirementBody</c> is not recursed into (out of scope). Alternative 2
    ///     (<c>(usageExtensionKeyword* CONSTRAINT | usageExtensionKeyword+)
    ///     constraintUsageDeclaration calculationBody</c>, the corpus-dominant inline form, e.g.
    ///     <c>require constraint { massActual &lt;= massReqd }</c>) captures the declared name
    ///     (usually anonymous per corpus) and the raw, unparsed, brace-included
    ///     <c>calculationBody</c> text into <see cref="SysmlFeatureNode.ExpressionText"/> —
    ///     mirroring <see cref="VisitTransitionUsage"/>'s <c>Guard</c> raw-text-capture precedent.
    ///     No <c>Children</c> are collected in either case — calculation-body internals
    ///     (<c>calculationBodyItem</c>/<c>actionBodyItem</c> control nodes) are explicitly out of
    ///     scope per this unit's "do not model expression trees" boundary.
    /// </remarks>
    private SysmlFeatureNode? BuildConstraintFeatureNode(
        SysMLv2Parser.RequirementConstraintUsageContext? usage, string keyword)
    {
        if (usage is null)
        {
            return null;
        }

        if (usage.ownedReferenceSubsetting() is { } reference)
        {
            return new SysmlFeatureNode
            {
                FeatureKeyword = keyword,
                ExpressionText = reference.GetText(),
                ReferenceOccurrences = CollectReferenceOccurrences(null, reference, usage.featureSpecializationPart()),
            };
        }

        var name = GetDeclaredName(usage.constraintUsageDeclaration()?.usageDeclaration()?.identification());
        var expressionText = usage.calculationBody()?.GetText();
        var specialization = ExtractSpecialization(
            usage.constraintUsageDeclaration()?.usageDeclaration()?.featureSpecializationPart());

        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = name is not null ? QualifyName(name) : null,
                FeatureKeyword = keyword,
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
                ExpressionText = expressionText,
                ReferenceOccurrences = ConstraintBodyOccurrences(usage.calculationBody()),
            },
            usage.constraintUsageDeclaration()?.usageDeclaration(),
            usage.constraintUsageDeclaration()?.valuePart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConstraintUsage(SysMLv2Parser.ConstraintUsageContext context)
    {
        // constraintUsage: occurrenceUsagePrefix CONSTRAINT constraintUsageDeclaration
        // calculationBody — a top-level, non-nested-in-requirement `constraint { expr }` usage.
        // Previously entirely absent from this file (no override at all), so such a usage
        // vanished with zero AST trace. Minimal capture: name (usually anonymous per corpus) plus
        // the raw calculationBody expression text.
        var name = GetDeclaredName(context.constraintUsageDeclaration()?.usageDeclaration()?.identification());
        var expressionText = context.calculationBody()?.GetText();
        var specialization = ExtractSpecialization(
            context.constraintUsageDeclaration()?.usageDeclaration()?.featureSpecializationPart());

        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = name is not null ? QualifyName(name) : null,
                FeatureKeyword = "constraint",
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
                ExpressionText = expressionText,
                ReferenceOccurrences = ConstraintBodyOccurrences(context.calculationBody()),
            },
            context.constraintUsageDeclaration()?.usageDeclaration(),
            context.constraintUsageDeclaration()?.valuePart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRequirementVerificationMember(
        SysMLv2Parser.RequirementVerificationMemberContext context)
    {
        // Newly-required safeguard: now that VisitRequirementDefinition/VisitConcernDefinition
        // call CollectChildren over requirementBodyItems, ANTLR's default VisitChildren dispatch
        // would, for the first time, recurse into a `verify` member's own nested requirementBody
        // (e.g. a nested subjectMember/constraint) — content that would otherwise incorrectly
        // bubble up and be added as a child of the OUTER enclosing requirement, via the aggregate-
        // last-non-null default dispatch behavior. A `verify` member's target is already captured
        // separately by FindVerificationMembers (VerifiedRequirementNames), so this override
        // short-circuits to null to suppress the now-newly-reachable recursive descent. Do not
        // remove this override without re-verifying that CollectChildren no longer walks
        // requirementBodyItem, since removing it would silently corrupt an enclosing requirement's
        // Children with spuriously-hoisted nested content.
        return null;
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitFramedConcernMember(SysMLv2Parser.FramedConcernMemberContext context)
    {
        // Same safeguard as VisitRequirementVerificationMember above: a `frame` member's nested
        // concern/calculationBody content is explicitly out of scope and must not be spuriously
        // hoisted onto the enclosing requirement's Children now that requirementBodyItems are
        // collected. See that override's remarks for the full rationale.
        return null;
    }

    /// <summary>
    ///     Recursively scans a parse (sub)tree for <see cref="SysMLv2Parser.RequirementVerificationMemberContext"/>
    ///     nodes at any depth, extracting the raw requirement reference name from each. This is a
    ///     manual tree-walk (not <c>Visit</c>/<c>VisitChildren</c> dispatch) because nothing else
    ///     in <see cref="AstBuilder"/> currently visits into <c>requirementBody</c>/<c>caseBody</c>
    ///     subtrees, so no double-counting risk exists; it is intentionally narrow (only looks for
    ///     this one context type) rather than a general-purpose parse-tree utility.
    /// </summary>
    private static IReadOnlyList<string> FindVerificationMembers(Antlr4.Runtime.Tree.IParseTree root)
    {
        var names = new List<string>();
        CollectVerificationMembers(root, names);
        return names;
    }

    /// <summary>Recursive helper for <see cref="FindVerificationMembers"/>.</summary>
    private static void CollectVerificationMembers(Antlr4.Runtime.Tree.IParseTree node, List<string> names)
    {
        if (node is SysMLv2Parser.RequirementVerificationMemberContext member)
        {
            var name = ExtractVerifiedRequirementName(member.requirementVerificationUsage());
            if (name is { Length: > 0 })
            {
                names.Add(name);
            }

            // requirementVerificationMember does not nest further verification members, but keep
            // walking regardless (safe: none exist for this context in practice, and consistent
            // with the "no special casing" design).
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (child is not null)
            {
                CollectVerificationMembers(child, names);
            }
        }
    }

    /// <summary>
    ///     Extracts the raw requirement reference name from a <c>verify</c> member's usage: either
    ///     the redefine/reference form (<c>ownedReferenceSubsetting</c>) or the typed-placeholder
    ///     form (<c>constraintUsageDeclaration</c>'s feature typing, reusing <see cref="ExtractFeatureTyping"/>).
    /// </summary>
    private static string? ExtractVerifiedRequirementName(SysMLv2Parser.RequirementVerificationUsageContext? usage)
    {
        if (usage is null)
        {
            return null;
        }

        var byReference = usage.ownedReferenceSubsetting()?.GetText();
        if (byReference is { Length: > 0 })
        {
            return byReference;
        }

        return ExtractFeatureTyping(usage.constraintUsageDeclaration()?.usageDeclaration()?.featureSpecializationPart());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitStateUsage(SysMLv2Parser.StateUsageContext context)
    {
        var decl = context.actionUsageDeclaration()?.usageDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        // A state usage's own feature typing (e.g. `state vehicleStates : VehicleStates { ... }`)
        // was previously dropped entirely — unlike BuildUsageNode's generic usage handling, no
        // Typing edge was ever recorded for state usages. This is necessary so
        // ReferenceResolver's inherited-pseudostate-feature fallback (start/done) can walk from
        // this usage to its state def and on to Actions::Action via the Supertype chain.
        var specialization = ExtractSpecialization(decl?.featureSpecializationPart());

        // Collect the state body (nested state usages and transitions) as children, mirroring
        // VisitStateDefinition. Anonymous ("state x;") usages have no body items to collect.
        _namespaceStack.Add(name);
        var (children, annotations) = CollectChildren(context.stateUsageBody()?.stateBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return ApplyFeatureNotation(
            new SysmlFeatureNode
            {
                Name = name,
                QualifiedName = QualifyName(name),
                FeatureKeyword = "state",
                FeatureTyping = specialization.Typing,
                Multiplicity = specialization.Multiplicity,
                Children = children,
                Annotations = annotations,
                IsParallel = context.stateUsageBody()?.PARALLEL() is not null,
            },
            decl,
            context.actionUsageDeclaration()?.valuePart());
    }

    /// <summary>
    ///     Dispatches a single <c>stateBodyItem</c> alternative to the appropriate builder logic.
    ///     Most alternatives (<c>nonBehaviorBodyItem</c>, <c>transitionUsageMember</c>,
    ///     <c>doActionMember</c>, <c>exitActionMember</c>) already produce exactly one AST node via
    ///     the default ANTLR visitor dispatch, so they are simply passed through unchanged. The two
    ///     "attached transition" shapes — <c>(sourceSuccessionMember)? behaviorUsageMember
    ///     (targetTransitionUsageMember)*</c> and <c>entryActionMember (entryTransitionMember)*</c>
    ///     — each produce more than one sibling node (the preceding state/entry-action usage, plus
    ///     one <see cref="SysmlTransitionNode"/> per attached transition whose <c>Source</c> is
    ///     implicitly that preceding usage's declared name); without this override, ANTLR's default
    ///     <c>VisitChildren</c> aggregation silently discards every child result but the last,
    ///     dropping both the preceding usage and any earlier attached transitions.
    /// </summary>
    public override SysmlNode? VisitStateBodyItem(SysMLv2Parser.StateBodyItemContext context)
    {
        if (context.nonBehaviorBodyItem() is { } nonBehaviorBodyItem)
        {
            return Visit(nonBehaviorBodyItem);
        }

        if (context.transitionUsageMember() is { } transitionUsageMember)
        {
            return Visit(transitionUsageMember);
        }

        if (context.doActionMember() is { } doActionMember)
        {
            return Visit(doActionMember);
        }

        if (context.exitActionMember() is { } exitActionMember)
        {
            return Visit(exitActionMember);
        }

        if (context.behaviorUsageMember() is { } behaviorUsageMember)
        {
            var usageNode = Visit(behaviorUsageMember);
            var targets = context.targetTransitionUsageMember();
            if (usageNode is null || targets.Length == 0)
            {
                return usageNode;
            }

            var sourceName = usageNode.Name;
            var nodes = new List<SysmlNode> { usageNode };
            foreach (var target in targets)
            {
                nodes.Add(BuildAttachedTransition(sourceName, target.targetTransitionUsage()));
            }

            return new MultiNodeCapture { Nodes = nodes };
        }

        if (context.entryActionMember() is { } entryActionMember)
        {
            var entryNode = Visit(entryActionMember);
            var transitions = context.entryTransitionMember();
            if (entryNode is null || transitions.Length == 0)
            {
                return entryNode;
            }

            var sourceName = entryNode.Name;
            var nodes = new List<SysmlNode> { entryNode };
            foreach (var transition in transitions)
            {
                nodes.Add(BuildEntryAttachedTransition(sourceName, transition));
            }

            return new MultiNodeCapture { Nodes = nodes };
        }

        return null;
    }

    /// <summary>
    ///     Builds the <see cref="SysmlTransitionNode"/> for one <c>targetTransitionUsageMember</c>
    ///     attached after a state/action usage (e.g. <c>accept Sig then starting;</c>), whose
    ///     <c>Source</c> is implicitly the preceding usage's declared name (never present in the
    ///     grammar itself). Extraction mirrors <see cref="VisitTransitionUsage"/>'s handling of the
    ///     explicit <c>transition ... then ...;</c> form exactly (target via
    ///     <see cref="ConnectorEndReference"/>, guard via the raw <c>if</c> expression text).
    /// </summary>
    private SysmlTransitionNode BuildAttachedTransition(
        string? sourceName,
        SysMLv2Parser.TargetTransitionUsageContext? usage)
    {
        var target = ConnectorEndReference(
            usage?.transitionSuccessionMember()?.transitionSuccession()?.connectorEndMember());
        var guard = usage?.guardExpressionMember()?.ownedExpression()?.GetText();

        return new SysmlTransitionNode
        {
            Source = sourceName,
            Target = target,
            Guard = guard,
            Trigger = OriginalTextOrNull(usage?.triggerActionMember()?.triggerAction()),
            Effect = OriginalTextOrNull(usage?.effectBehaviorMember()?.effectBehaviorUsage()),
            ReferenceOccurrences = TransitionOccurrences(
                usage?.guardExpressionMember(), usage?.triggerActionMember(), usage?.effectBehaviorMember()),
        };
    }

    /// <summary>
    ///     Builds the <see cref="SysmlTransitionNode"/> for one <c>entryTransitionMember</c>
    ///     attached after an entry action (e.g. <c>entry action initial; ... then off;</c>),
    ///     whose <c>Source</c> is implicitly the preceding entry action's declared name. Handles
    ///     both the guarded (<c>guardedTargetSuccession</c>: <c>if ... then ...</c>) and bare
    ///     (<c>THEN transitionSuccessionMember</c>) alternatives.
    /// </summary>
    private SysmlTransitionNode BuildEntryAttachedTransition(
        string? sourceName,
        SysMLv2Parser.EntryTransitionMemberContext transition)
    {
        var guarded = transition.guardedTargetSuccession();
        var successionMember = guarded?.transitionSuccessionMember()
            ?? transition.transitionSuccessionMember();
        var target = ConnectorEndReference(
            successionMember?.transitionSuccession()?.connectorEndMember());
        var guard = guarded?.guardExpressionMember()?.ownedExpression()?.GetText();

        return new SysmlTransitionNode
        {
            Source = sourceName,
            Target = target,
            Guard = guard,
            ReferenceOccurrences = CollectReferenceOccurrences(null, guarded?.guardExpressionMember()?.ownedExpression()),
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitEntryActionMember(SysMLv2Parser.EntryActionMemberContext context)
    {
        return BuildStateActionFeatureNode(context.stateActionUsage(), "entry");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitDoActionMember(SysMLv2Parser.DoActionMemberContext context)
    {
        return BuildStateActionFeatureNode(context.stateActionUsage(), "do");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitExitActionMember(SysMLv2Parser.ExitActionMemberContext context)
    {
        return BuildStateActionFeatureNode(context.stateActionUsage(), "exit");
    }

    /// <summary>
    ///     Builds a minimal, deliberately non-behavioral <see cref="SysmlFeatureNode"/> for an
    ///     <c>entry</c>/<c>do</c>/<c>exit</c> action member, registering only its declared name (so
    ///     it becomes a resolvable transition source per the OMG spec's Annex A.7 idiom, e.g.
    ///     <c>entry action initial; ... transition initial then off;</c>) with no children. The
    ///     nested action body's internal semantics — parameter bindings
    ///     (<c>performSelfTest{ in vehicle = operatingVehicle; }</c>), nested steps
    ///     (<c>do action providePower { /* ... */ }</c>), accept/send/assignment action-usage
    ///     alternatives — are deliberately NOT modeled; this tool only needs the named feature
    ///     itself to exist and be resolvable, not a full action-body AST.
    /// </summary>
    private SysmlFeatureNode BuildStateActionFeatureNode(
        SysMLv2Parser.StateActionUsageContext? usage,
        string keyword)
    {
        var name = ExtractStateActionName(usage);

        // The declaration (not the nested body) of whichever action alternative is present.
        var acceptDeclaration = usage?.stateAcceptActionUsage()?.acceptNodeDeclaration();

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            FeatureKeyword = keyword,
            Children = Array.Empty<SysmlNode>(),
            Annotations = Array.Empty<SysmlAnnotation>(),
            ActionText = ExtractStateActionText(usage),
            ReferenceOccurrences = CollectReferenceOccurrences(
                DeclaredPayloadNames(acceptDeclaration?.acceptParameterPart()),
                usage?.statePerformActionUsage()?.performActionUsageDeclaration(),
                acceptDeclaration,
                usage?.stateSendActionUsage()?.sendNodeDeclaration(),
                usage?.stateAssignmentActionUsage()?.assignmentNodeDeclaration()),
        };
    }

    /// <summary>
    ///     Extracts the verbatim declaration text of a <c>stateActionUsage</c> (the
    ///     <c>performActionUsageDeclaration</c>, <c>acceptNodeDeclaration</c>,
    ///     <c>sendNodeDeclaration</c>, or <c>assignmentNodeDeclaration</c> of whichever alternative
    ///     is present), excluding its action body, or <see langword="null"/> for the empty form
    ///     (<c>entry;</c>).
    /// </summary>
    private static string? ExtractStateActionText(SysMLv2Parser.StateActionUsageContext? usage) =>
        OriginalTextOrNull(usage?.statePerformActionUsage()?.performActionUsageDeclaration())
        ?? OriginalTextOrNull(usage?.stateAcceptActionUsage()?.acceptNodeDeclaration())
        ?? OriginalTextOrNull(usage?.stateSendActionUsage()?.sendNodeDeclaration())
        ?? OriginalTextOrNull(usage?.stateAssignmentActionUsage()?.assignmentNodeDeclaration());

    /// <summary>
    ///     Extracts the declared name of a <c>stateActionUsage</c>, or <see langword="null"/> when
    ///     the action is unnamed. Only the named <c>ACTION usageDeclaration?</c> alternative of
    ///     <c>performActionUsageDeclaration</c> (e.g. <c>action providePower { ... }</c>) yields a
    ///     name; the unnamed reference-subsetting form (e.g. <c>entry performSelfTest{ ... }</c>,
    ///     which subsets/references an existing behavior rather than declaring a new named
    ///     feature) and the <c>stateAcceptActionUsage</c>/<c>stateSendActionUsage</c>/
    ///     <c>stateAssignmentActionUsage</c>/<c>emptyActionUsage_</c> alternatives are out of scope
    ///     per ROADMAP.md — only NAMED entry actions need to be resolvable transition sources.
    /// </summary>
    private static string? ExtractStateActionName(SysMLv2Parser.StateActionUsageContext? usage)
    {
        var declaration = usage?.statePerformActionUsage()?.performActionUsageDeclaration();
        return GetDeclaredName(declaration?.usageDeclaration()?.identification());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitTransitionUsage(SysMLv2Parser.TransitionUsageContext context)
    {
        var name = GetDeclaredName(context.usageDeclaration()?.identification());

        // Source is the feature chain after FIRST; target is the connector end after THEN.
        var source = context.featureChainMember()?.GetText();
        var target = ConnectorEndReference(
            context.transitionSuccessionMember()?.transitionSuccession()?.connectorEndMember());
        var guard = context.guardExpressionMember()?.ownedExpression()?.GetText();

        return new SysmlTransitionNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            Source = source,
            Target = target,
            Guard = guard,
            Trigger = OriginalTextOrNull(context.triggerActionMember()?.triggerAction()),
            Effect = OriginalTextOrNull(context.effectBehaviorMember()?.effectBehaviorUsage()),
            ReferenceOccurrences = TransitionOccurrences(
                context.guardExpressionMember(), context.triggerActionMember(), context.effectBehaviorMember()),
        };
    }

    /// <summary>
    ///     The parts of one end of a connector: the referenced feature chain, the optional role
    ///     name of the <c>name ::&gt; reference</c> form, and the optional cross multiplicity text.
    /// </summary>
    /// <param name="Reference">Referenced feature chain text (for example <c>a.p</c>), or null.</param>
    /// <param name="RoleName">Role name declared before <c>::&gt;</c>, or null.</param>
    /// <param name="Multiplicity">Cross multiplicity text (for example <c>[2]</c>), or null.</param>
    private readonly record struct ConnectorEndInfo(string? Reference, string? RoleName, string? Multiplicity);

    /// <summary>
    ///     Extracts both ends of a binary connector (the features either side of
    ///     <c>connect … to …</c>) with their role names and multiplicities, or empty ends when the
    ///     connector is not a simple binary connection.
    /// </summary>
    private static (ConnectorEndInfo A, ConnectorEndInfo B) ExtractConnectorEnds(SysMLv2Parser.ConnectorPartContext? connectorPart)
    {
        var ends = connectorPart?.binaryConnectorPart()?.connectorEndMember();
        if (ends is not { Length: >= 2 })
        {
            return (default, default);
        }

        return (ConnectorEndDetail(ends[0].connectorEnd()), ConnectorEndDetail(ends[1].connectorEnd()));
    }

    /// <summary>
    ///     Reads the reference, role name, and cross multiplicity of a connector end
    ///     (<c>[multiplicity]? (name ::&gt;)? reference</c>).
    /// </summary>
    private static ConnectorEndInfo ConnectorEndDetail(SysMLv2Parser.ConnectorEndContext? end)
    {
        return end is null
            ? default
            : ConnectorEndDetail(end.name(), end.ownedCrossMultiplicityMember(), end.ownedReferenceSubsetting());
    }

    /// <summary>
    ///     Reads the reference, role name, and cross multiplicity of an interface end
    ///     (<c>[multiplicity]? (name ::&gt;)? reference</c>, the same shape as a connector end).
    /// </summary>
    private static ConnectorEndInfo ConnectorEndDetail(SysMLv2Parser.InterfaceEndContext? end)
    {
        return end is null
            ? default
            : ConnectorEndDetail(end.name(), end.ownedCrossMultiplicityMember(), end.ownedReferenceSubsetting());
    }

    /// <summary>Builds a <see cref="ConnectorEndInfo"/> from the three optional parse-tree parts of an end.</summary>
    private static ConnectorEndInfo ConnectorEndDetail(
        SysMLv2Parser.NameContext? name,
        SysMLv2Parser.OwnedCrossMultiplicityMemberContext? multiplicity,
        SysMLv2Parser.OwnedReferenceSubsettingContext? reference)
    {
        var multiplicityText = multiplicity?.GetText();
        return new ConnectorEndInfo(
            reference?.GetText(),
            name?.GetText(),
            string.IsNullOrEmpty(multiplicityText) ? null : multiplicityText);
    }

    /// <summary>Builds a <see cref="SysmlConnectionNode"/> from its keyword, two ends, and optional payload text.</summary>
    private SysmlConnectionNode BuildConnectionNode(
        string? name,
        string keyword,
        ConnectorEndInfo endA,
        ConnectorEndInfo endB,
        string? payloadText)
    {
        return new SysmlConnectionNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            ConnectionKeyword = keyword,
            EndpointA = endA.Reference,
            EndpointB = endB.Reference,
            EndpointAName = endA.RoleName,
            EndpointAMultiplicity = endA.Multiplicity,
            EndpointBName = endB.RoleName,
            EndpointBMultiplicity = endB.Multiplicity,
            PayloadText = payloadText,
        };
    }

    /// <summary>Returns the qualified feature reference named by a connector end, or null.</summary>
    private static string? ConnectorEndReference(SysMLv2Parser.ConnectorEndMemberContext? member)
    {
        var end = member?.connectorEnd();
        var reference = end?.ownedReferenceSubsetting();
        return reference?.GetText();
    }

    /// <summary>
    ///     Builds a usage/feature AST node from a <see cref="SysMLv2Parser.UsageContext"/>, capturing
    ///     the keyword, declared name, feature typing, multiplicity, and any nested usage children.
    /// </summary>
    private SysmlFeatureNode? BuildUsageNode(SysMLv2Parser.UsageContext? usage, string keyword)
    {
        if (usage is null)
        {
            return null;
        }

        var decl = usage.usageDeclaration();
        var name = GetDeclaredName(decl?.identification());
        var typing = ExtractFeatureTyping(decl?.featureSpecializationPart());
        var redefined = ExtractRedefinedFeature(decl?.featureSpecializationPart());
        var supertypeNames = ExtractSubsettingTargetNames(decl?.featureSpecializationPart());
        var multiplicity = ExtractMultiplicity(decl?.featureSpecializationPart());

        // An unnamed usage that redefines a feature (e.g. `port redefines fuelTankPort { ... }`,
        // with no name token of its own) implicitly takes the redefined feature's own simple name
        // per SysML v2 semantics — without this fallback, such a usage's Name/QualifiedName stay
        // null and it can never be referenced or resolved by name (e.g. as a `connect`/`bind`
        // endpoint), even though the model clearly identifies it. Only the trailing segment of the
        // (possibly qualified) redefined reference is used, mirroring how a redefined feature's own
        // declared name is always just its simple name.
        var effectiveName = name ?? (redefined is not null ? SimpleNameFromReference(redefined) : null);

        // Named usages contribute a namespace segment for any nested usages they own.
        var qualifiedName = effectiveName is not null ? QualifyName(effectiveName) : null;
        IReadOnlyList<SysmlNode> children = Array.Empty<SysmlNode>();
        IReadOnlyList<SysmlAnnotation> annotations = Array.Empty<SysmlAnnotation>();
        var body = usage.usageCompletion()?.usageBody()?.definitionBody();
        if (body is not null)
        {
            if (effectiveName is not null)
            {
                _namespaceStack.Add(effectiveName);
            }

            (children, annotations) = CollectDefinitionBodyItems(body.definitionBodyItem());

            if (effectiveName is not null)
            {
                _namespaceStack.RemoveAt(_namespaceStack.Count - 1);
            }
        }

        var node = new SysmlFeatureNode
        {
            Name = effectiveName,
            QualifiedName = qualifiedName,
            FeatureKeyword = keyword,
            FeatureTyping = typing,
            RedefinedFeatureName = redefined,
            SupertypeNames = supertypeNames,
            Multiplicity = multiplicity,
            Children = children,
            Annotations = annotations,
        };

        return ApplyFeatureNotation(node, decl, usage.usageCompletion()?.valuePart());
    }

    /// <summary>
    ///     The feature-specialization data (<c>:</c> typing and multiplicity) captured from a usage
    ///     declaration.
    /// </summary>
    /// <param name="Typing">The first feature-typing reference, or null when untyped.</param>
    /// <param name="Multiplicity">The multiplicity text, or null.</param>
    private readonly record struct FeatureSpecialization(string? Typing, string? Multiplicity);

    /// <summary>
    ///     Extracts the <see cref="FeatureSpecialization"/> of a usage declaration's
    ///     <c>featureSpecializationPart</c>. Shared by every usage builder that produces a
    ///     <see cref="SysmlFeatureNode"/> outside <see cref="BuildUsageNode"/> (requirement, concern,
    ///     constraint, action, state, and the case-kind usages), so each keeps the feature typing and
    ///     multiplicity that a generic usage keeps. Redefinition and subsetting clauses are
    ///     deliberately not captured here: <see cref="NameResolver"/> resolves implicit
    ///     specialization (#93) for name lookup only, and capturing these clauses is deferred
    ///     until the remaining resolver gaps (#129) are closed, so capturing them now would only
    ///     add unresolved-reference warnings for valid models.
    /// </summary>
    private static FeatureSpecialization ExtractSpecialization(
        SysMLv2Parser.FeatureSpecializationPartContext? fsp) =>
        new(ExtractFeatureTyping(fsp), ExtractMultiplicity(fsp));

    /// <summary>
    ///     Shared notation-data enrichment applied by every builder that produces a
    ///     <see cref="SysmlFeatureNode"/> from a <c>usageDeclaration</c>/<c>valuePart</c> pair
    ///     (generic usages, and the specialized action, state, requirement, concern and constraint
    ///     usages): captures <c>ordered</c>/<c>nonunique</c>, the verbatim value clause, the
    ///     <c>::&gt;</c> reference-subsetting target and any conjugated typing name. Leading
    ///     modifiers (<c>abstract</c>, <c>ref</c>, direction, ...) are applied separately and
    ///     centrally by <see cref="ApplyLeadingModifiers"/>.
    /// </summary>
    /// <param name="node">The feature node to enrich.</param>
    /// <param name="decl">The usage declaration, or null when the usage has none.</param>
    /// <param name="valuePart">The value part (<c>= expr</c>), or null when none is declared.</param>
    /// <returns><paramref name="node"/>, for call chaining.</returns>
    private static SysmlFeatureNode ApplyFeatureNotation(
        SysmlFeatureNode node,
        SysMLv2Parser.UsageDeclarationContext? decl,
        SysMLv2Parser.ValuePartContext? valuePart)
    {
        var specializationPart = decl?.featureSpecializationPart();
        var multiplicityPart = specializationPart?.multiplicityPart();
        node.IsOrdered = multiplicityPart?.ORDERED() is not null;
        node.IsNonunique = multiplicityPart?.NONUNIQUE() is not null;
        node.ReferenceSubsettingName = ExtractReferenceSubsettingName(specializationPart);
        node.ConjugatedFeatureTyping = ExtractConjugatedTypingName(specializationPart);
        node.ValueText = OriginalTextOrNull(valuePart?.featureValue());
        return node;
    }

    /// <summary>
    ///     Collects the reference occurrences of a constraint or calculation body: the feature
    ///     references of its expressions. Every name the body declares for itself (its parameters
    ///     and local usages) is excluded, because the body's declarations are not modeled as AST
    ///     nodes and so cannot be resolved.
    /// </summary>
    private IReadOnlyList<ReferenceOccurrence> ConstraintBodyOccurrences(SysMLv2Parser.CalculationBodyContext? body) =>
        body is null ? Array.Empty<ReferenceOccurrence>() : CollectReferenceOccurrences(NamesDeclaredWithin(body), body);

    /// <summary>
    ///     Returns every name declared (by an identification or feature identification) inside
    ///     <paramref name="tree"/>, without descending into nested bodies (an inline
    ///     <c>{ in i : T; ... }</c> expression body or a nested calculation/definition body): a
    ///     name such a body declares is local to it and must not hide a reference outside it.
    /// </summary>
    private static IReadOnlySet<string> NamesDeclaredWithin(Antlr4.Runtime.Tree.IParseTree tree)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<Antlr4.Runtime.Tree.IParseTree>();
        pending.Push(tree);
        while (pending.Count > 0)
        {
            var item = pending.Pop();
            if (!ReferenceEquals(item, tree) &&
                item is SysMLv2Parser.BodyExpressionContext or SysMLv2Parser.CalculationBodyContext or SysMLv2Parser.DefinitionBodyContext)
            {
                continue;
            }
            var declared = item switch
            {
                SysMLv2Parser.IdentificationContext identification => GetDeclaredName(identification),
                SysMLv2Parser.FeatureIdentificationContext featureIdentification => GetDeclaredName(featureIdentification),
                _ => null,
            };
            if (declared is not null)
            {
                names.Add(declared);
            }

            for (var i = 0; i < item.ChildCount; i++)
            {
                pending.Push(item.GetChild(i));
            }
        }

        return names;
    }

    /// <summary>
    ///     Extracts the first feature-typing qualified name from a feature specialization part
    ///     (the type that follows <c>:</c> or <c>typed by</c>), or null when the feature is untyped.
    /// </summary>
    private static string? ExtractFeatureTyping(SysMLv2Parser.FeatureSpecializationPartContext? fsp)
    {
        if (fsp is null)
        {
            return null;
        }

        foreach (var fs in fsp.featureSpecialization())
        {
            var typings = fs.typings();
            if (typings is null)
            {
                continue;
            }

            // The first typing is held by the typedBy clause; additional typings follow as a list.
            var fromTypedBy = TypingName(typings.typedBy()?.featureTyping());
            if (fromTypedBy is not null)
            {
                return fromTypedBy;
            }

            foreach (var ft in typings.featureTyping())
            {
                var name = TypingName(ft);
                if (name is not null)
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>Extracts the qualified type name from a single feature-typing context.</summary>
    private static string? TypingName(SysMLv2Parser.FeatureTypingContext? ft)
    {
        if (ft is null)
        {
            return null;
        }

        var owned = ft.ownedFeatureTyping();
        return owned?.GetText();
    }

    /// <summary>
    ///     Extracts the first redefined-feature raw reference text from a feature specialization
    ///     part (the target that follows <c>redefines</c>/<c>:&gt;&gt;</c>), or null when the
    ///     feature declares no redefinition.
    /// </summary>
    private static string? ExtractRedefinedFeature(SysMLv2Parser.FeatureSpecializationPartContext? fsp)
    {
        if (fsp is null)
        {
            return null;
        }

        foreach (var fs in fsp.featureSpecialization())
        {
            var redefinitions = fs.redefinitions();
            if (redefinitions is null)
            {
                continue;
            }

            // The first redefined feature is held by the redefines clause; additional
            // redefinitions follow as a list.
            var fromRedefines = redefinitions.redefines()?.ownedRedefinition();
            if (fromRedefines is not null)
            {
                return fromRedefines.GetText();
            }

            var fromList = redefinitions.ownedRedefinition().FirstOrDefault(owned => owned is not null);
            if (fromList is not null)
            {
                return fromList.GetText();
            }
        }

        return null;
    }

    /// <summary>
    ///     Derives the trailing simple-name segment from a raw (possibly qualified and/or
    ///     dot-chained) reference text, e.g. <c>"Owner::fuelTankPort"</c> → <c>"fuelTankPort"</c>,
    ///     and <c>"tank.fuelTankPort"</c> → <c>"fuelTankPort"</c> (an <c>ownedRedefinition</c> is
    ///     grammatically a <c>qualifiedName ( DOT qualifiedName )*</c> chain, so a redefinition
    ///     reference can be a dotted feature path, not just a single <c>::</c>-qualified name). Takes
    ///     whichever of the last <c>::</c> or last <c>.</c> separator occurs furthest to the right, so
    ///     a reference with neither separator is returned unchanged. Used to derive an unnamed usage's
    ///     implicit name from the feature it redefines (see the <c>effectiveName</c> fallback in
    ///     <see cref="BuildUsageNode"/>).
    /// </summary>
    private static string SimpleNameFromReference(string reference)
    {
        var afterColonColon = reference.LastIndexOf("::", StringComparison.Ordinal) is var colonIndex && colonIndex >= 0
            ? colonIndex + 2
            : 0;
        var afterDot = reference.LastIndexOf('.') is var dotIndex && dotIndex >= 0
            ? dotIndex + 1
            : 0;
        var start = Math.Max(afterColonColon, afterDot);
        return start > 0 ? reference[start..] : reference;
    }

    /// <summary>
    ///     Extracts the raw reference text of every <c>subsets &lt;target&gt;;</c>/<c>:&gt;
    ///     &lt;target&gt;</c> clause on a usage/feature — a subsetting reference, grammatically
    ///     distinct from a redefinition (<see cref="ExtractRedefinedFeature"/>) even though both
    ///     share the same <c>featureSpecialization</c> alternative structure. Mirrors
    ///     <see cref="ExtractRedefinedFeature"/>'s structure for pulling both the first (held by
    ///     the <c>subsets</c> clause) and any subsequent comma-separated targets. Populates
    ///     <c>SysmlFeatureNode.SupertypeNames</c> so that a usage-level <c>:&gt;</c> (e.g.
    ///     <c>part vehicle1_c1 :&gt; vehicle1</c>) is resolved into a <see
    ///     cref="SysmlEdgeKind.Supertype"/> edge the same uniform way a definition-level <c>:&gt;</c>
    ///     already is, which <see cref="ReferenceResolver"/>'s name resolution depends on to reach
    ///     an inherited member through a usage's own subsetting ancestor.
    /// </summary>
    private static IReadOnlyList<string> ExtractSubsettingTargetNames(SysMLv2Parser.FeatureSpecializationPartContext? fsp)
    {
        if (fsp is null)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var fs in fsp.featureSpecialization())
        {
            var subsettingPart = fs.subsettings();
            if (subsettingPart is null)
            {
                continue;
            }

            // The first subsetting target is held by the `subsets`/`:>` clause; additional
            // targets follow as a comma-separated list.
            var fromSubsets = subsettingPart.subsets()?.ownedSubsetting();
            if (fromSubsets is not null)
            {
                names.Add(fromSubsets.GetText());
            }

            foreach (var owned in subsettingPart.ownedSubsetting())
            {
                names.Add(owned.GetText());
            }
        }

        return names;
    }

    /// <summary>
    ///     Extracts the raw reference text of the first <c>::&gt;</c>/<c>references</c>
    ///     reference-subsetting clause from a feature specialization part, or null when the feature
    ///     declares none. Deliberately kept out of <c>SupertypeNames</c>, which drives
    ///     <see cref="SysmlEdgeKind.Supertype"/> edges and subsetting classification.
    /// </summary>
    private static string? ExtractReferenceSubsettingName(SysMLv2Parser.FeatureSpecializationPartContext? fsp)
    {
        if (fsp is null)
        {
            return null;
        }

        foreach (var fs in fsp.featureSpecialization())
        {
            var target = fs.references()?.ownedReferenceSubsetting();
            if (target is not null)
            {
                return target.GetText();
            }
        }

        return null;
    }

    /// <summary>
    ///     Extracts the raw type name of the first conjugated feature typing (<c>~PortT</c>) from a
    ///     feature specialization part, or null when the feature is not conjugated-typed.
    ///     <see cref="ExtractFeatureTyping"/> deliberately ignores such typings, so no typing edge
    ///     is produced for them.
    /// </summary>
    private static string? ExtractConjugatedTypingName(SysMLv2Parser.FeatureSpecializationPartContext? fsp)
    {
        if (fsp is null)
        {
            return null;
        }

        foreach (var fs in fsp.featureSpecialization())
        {
            var typings = fs.typings();
            if (typings is null)
            {
                continue;
            }

            var fromTypedBy = typings.typedBy()?.featureTyping()?.conjugatedPortTyping()?.qualifiedName();
            if (fromTypedBy is not null)
            {
                return fromTypedBy.GetText();
            }

            foreach (var ft in typings.featureTyping())
            {
                var conjugated = ft.conjugatedPortTyping()?.qualifiedName();
                if (conjugated is not null)
                {
                    return conjugated.GetText();
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Extracts the multiplicity text (e.g. <c>[4]</c>) from a feature specialization part,
    ///     or null when no multiplicity is declared.
    /// </summary>
    private static string? ExtractMultiplicity(SysMLv2Parser.FeatureSpecializationPartContext? fsp)
    {
        var multiplicity = fsp?.multiplicityPart()?.ownedMultiplicity();
        var text = multiplicity?.GetText();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitViewDefinition(SysMLv2Parser.ViewDefinitionContext context)
    {
        var name = GetDeclaredName(context.definitionDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(
            context.definitionDeclaration()?.subclassificationPart());

        var (renderTargetName, filterExpressionTexts) =
            ExtractViewRenderAndFilter(context.viewDefinitionBody()?.viewDefinitionBodyItem() ?? []);

        return new SysmlViewNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            SupertypeNames = supertypeNames,
            RenderTargetName = renderTargetName,
            Children = ExtractViewImports(context.viewDefinitionBody()?.viewDefinitionBodyItem() ?? []),
            ReferenceOccurrences = ExtractRenderOccurrences(context.viewDefinitionBody()?.viewDefinitionBodyItem() ?? []),
            FilterExpressionText = filterExpressionTexts.FirstOrDefault(),
            FilterExpressionTexts = filterExpressionTexts,
            SourceFilePath = _filePath,
            SourceLine = context.Start.Line,
            SourceColumn = context.Start.Column + 1,
        };
    }

    /// <summary>
    ///     Builds a <see cref="SysmlViewNode"/> from a <c>view</c> usage (as opposed to a
    ///     <c>view def</c> definition), capturing the same render/filter members as
    ///     <see cref="VisitViewDefinition"/> plus <c>expose</c> members — the usage form's only
    ///     grammar addition over the definition form's body.
    /// </summary>
    /// <remarks>
    ///     Unnamed view usages (no declared name) are not registered as symbols and are skipped,
    ///     mirroring the existing anonymous-element convention used by
    ///     <see cref="VisitStateUsage"/> and other usage visitors.
    /// </remarks>
    public override SysmlNode? VisitViewUsage(SysMLv2Parser.ViewUsageContext context)
    {
        var name = GetDeclaredName(context.usageDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var bodyItems = context.viewBody()?.viewBodyItem() ?? [];

        var (renderTargetName, filterExpressionTexts) = ExtractViewRenderAndFilter(bodyItems);
        var exposeMembers = ExtractExposedNames(bodyItems);

        return new SysmlViewNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            RenderTargetName = renderTargetName,
            Children = ExtractViewImports(bodyItems),
            ReferenceOccurrences = ExtractRenderOccurrences(bodyItems),
            ExposeMembers = exposeMembers,
            FilterExpressionText = filterExpressionTexts.FirstOrDefault(),
            FilterExpressionTexts = filterExpressionTexts,
            ViewDefinitionName = ExtractFeatureTyping(context.usageDeclaration()?.featureSpecializationPart()),
            SourceFilePath = _filePath,
            SourceLine = context.Start.Line,
            SourceColumn = context.Start.Column + 1,
        };
    }

    /// <summary>
    ///     Scans a view's body items for a <c>render &lt;target&gt;;</c> member and every
    ///     <c>filter [&lt;expression&gt;];</c> member, returning the raw reference text (or
    ///     <see langword="null"/> when absent) and each filter's raw expression source text in order.
    ///     Shared by <see cref="VisitViewDefinition"/> (<c>viewDefinitionBodyItem</c>) and
    ///     <see cref="VisitViewUsage"/> (<c>viewBodyItem</c>) since both context types expose
    ///     identically-shaped <c>viewRenderingMember()</c>/<c>elementFilterMember()</c> accessors.
    ///     The first <c>render</c> member wins if more than one appears — SysML disallows more
    ///     than one render subject per view, so this is a defensive tie-break, not a validated
    ///     constraint enforced by this tool.
    /// </summary>
    private static (string? RenderTargetName, IReadOnlyList<string> FilterExpressionTexts) ExtractViewRenderAndFilter<TItem>(
        IEnumerable<TItem> bodyItems)
        where TItem : Antlr4.Runtime.ParserRuleContext
    {
        string? renderTargetName = null;
        var filterExpressionTexts = new List<string>();

        foreach (var item in bodyItems)
        {
            if (renderTargetName is null &&
                GetViewRenderingMember(item) is { } renderingMember)
            {
                renderTargetName = ExtractRenderTargetName(renderingMember.viewRenderingUsage());
            }

            // Every `filter` member is a view condition (SysML 8.3.26.11 viewCondition); all apply.
            if (GetElementFilterMember(item)?.ownedExpression() is { } filterExpr)
            {
                filterExpressionTexts.Add(GetOriginalText(filterExpr));
            }
        }

        return (renderTargetName, filterExpressionTexts);
    }

    /// <summary>
    ///     Reconstructs a parser rule context's original source text (preserving whitespace between
    ///     tokens), unlike <see cref="Antlr4.Runtime.RuleContext.GetText"/> which concatenates each
    ///     token's text with no separators. Required whenever the captured text will later be
    ///     re-lexed on its own (e.g. <c>FilterExpressionParser.Parse</c>) — without the original
    ///     inter-token spacing, adjacent keyword/identifier tokens can merge into a single token
    ///     (e.g. <c>"@Safety and (as Safety)"</c> would otherwise round-trip as the unlexable
    ///     <c>"@Safety" + "and" + "(as" + "Safety)"</c> run together with no separators, losing the
    ///     <c>and</c>/<c>as</c> keyword boundaries).
    /// </summary>
    private static string GetOriginalText(Antlr4.Runtime.ParserRuleContext context) =>
        context.Start.InputStream.GetText(
            new Antlr4.Runtime.Misc.Interval(context.Start.StartIndex, context.Stop.StopIndex));

    /// <summary>
    ///     Null-safe variant of <see cref="GetOriginalText"/> for contexts that may be absent or
    ///     match no tokens (epsilon rules such as <c>emptyActionUsage_</c>, whose <c>Stop</c> token
    ///     is null or precedes <c>Start</c>). Returns null in those cases.
    /// </summary>
    private static string? OriginalTextOrNull(Antlr4.Runtime.ParserRuleContext? context)
    {
        if (context?.Start is null || context.Stop is null || context.Stop.StopIndex < context.Start.StartIndex)
        {
            return null;
        }

        return GetOriginalText(context);
    }

    /// <summary>
    ///     Returns the original text spanning the present contexts among <paramref name="parts"/>
    ///     (from the earliest start to the latest stop, preserving the inter-token spacing), or
    ///     null when none of them matched any tokens.
    /// </summary>
    private static string? OriginalTextSpanOrNull(params Antlr4.Runtime.ParserRuleContext?[] parts)
    {
        var present = parts
            .Where(p => p?.Start is not null && p.Stop is not null && p.Stop.StopIndex >= p.Start.StartIndex)
            .Select(p => p!)
            .ToList();
        if (present.Count == 0)
        {
            return null;
        }

        var start = present.Min(p => p.Start.StartIndex);
        var stop = present.Max(p => p.Stop.StopIndex);
        return present[0].Start.InputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, stop));
    }

    /// <summary>Extracts the <c>viewRenderingMember()</c> accessor common to both view body item types.</summary>
    private static SysMLv2Parser.ViewRenderingMemberContext? GetViewRenderingMember(Antlr4.Runtime.ParserRuleContext item) =>
        item switch
        {
            SysMLv2Parser.ViewDefinitionBodyItemContext defItem => defItem.viewRenderingMember(),
            SysMLv2Parser.ViewBodyItemContext usageItem => usageItem.viewRenderingMember(),
            _ => null,
        };

    /// <summary>Extracts the <c>elementFilterMember()</c> accessor common to both view body item types.</summary>
    private static SysMLv2Parser.ElementFilterMemberContext? GetElementFilterMember(Antlr4.Runtime.ParserRuleContext item) =>
        item switch
        {
            SysMLv2Parser.ViewDefinitionBodyItemContext defItem => defItem.elementFilterMember(),
            SysMLv2Parser.ViewBodyItemContext usageItem => usageItem.elementFilterMember(),
            _ => null,
        };

    /// <summary>
    ///     Builds the children of a view from its body: the <c>import</c> statements it declares,
    ///     which apply to the view's own namespace (so a view-local <c>import Views::*;</c> makes
    ///     its <c>render asTreeDiagram;</c> resolve). Nothing else in a view body is a child — its
    ///     <c>render</c>, <c>filter</c> and <c>expose</c> members are captured as properties.
    /// </summary>
    private IReadOnlyList<SysmlNode> ExtractViewImports<TItem>(IEnumerable<TItem> bodyItems)
        where TItem : Antlr4.Runtime.ParserRuleContext
    {
        var imports = new List<SysmlNode>();
        foreach (var item in bodyItems)
        {
            var importRule = item switch
            {
                SysMLv2Parser.ViewDefinitionBodyItemContext definitionItem => definitionItem.definitionBodyItem()?.importRule(),
                SysMLv2Parser.ViewBodyItemContext usageItem => usageItem.definitionBodyItem()?.importRule(),
                _ => null,
            };
            if (importRule is not null && Visit(importRule) is { } import)
            {
                imports.Add(import);
            }
        }

        return imports.Count == 0 ? Array.Empty<SysmlNode>() : imports;
    }

    /// <summary>
    ///     Collects the <see cref="ReferenceOccurrence"/> of every <c>render &lt;target&gt;;</c>
    ///     member in a view's body: the direct reference of <c>render asTreeDiagram;</c>, or the
    ///     typing of the typed-placeholder form. Shared by both view builders like
    ///     <see cref="ExtractViewRenderAndFilter"/>.
    /// </summary>
    private IReadOnlyList<ReferenceOccurrence> ExtractRenderOccurrences<TItem>(IEnumerable<TItem> bodyItems)
        where TItem : Antlr4.Runtime.ParserRuleContext
    {
        var roots = new List<Antlr4.Runtime.Tree.IParseTree>();
        foreach (var item in bodyItems)
        {
            if (GetViewRenderingMember(item)?.viewRenderingUsage() is not { } usage)
            {
                continue;
            }

            // Direct form: the rendering is referenced (and may be further specialized). Placeholder
            // form: `render rendering r : T;` declares a usage whose typing is the reference.
            if (usage.ownedReferenceSubsetting() is { } reference)
            {
                roots.Add(reference);
                if (usage.featureSpecializationPart() is { } specialization)
                {
                    roots.Add(specialization);
                }
            }
            else if (usage.usage()?.usageDeclaration()?.featureSpecializationPart() is { } placeholderTyping)
            {
                roots.Add(placeholderTyping);
            }
        }

        // The SysML2Tools extension selectors stay in the occurrences: the resolver accepts them
        // with a portability warning (see ReferenceResolver.ToolRenderSelectors).
        return CollectReferenceOccurrences(null, roots.ToArray());
    }

    /// <summary>
    ///     The names a feature expression may use without any declaration: the features every
    ///     occurrence inherits from the kernel library (<c>self</c>, <c>that</c>) and the
    ///     <c>start</c>/<c>done</c> pseudo-features every action and state inherits. They are
    ///     recorded here as names an expression may use undeclared (the expression references are
    ///     syntactic captures that are not name-resolved against the implicit general types);
    ///     reporting them would flag valid models.
    /// </summary>
    private static readonly HashSet<string> ImplicitFeatureNames =
        new(StringComparer.Ordinal) { "self", "that", "start", "done" };

    /// <summary>
    ///     Collects the references written inside the given concrete-syntax subtrees (an
    ///     expression, an <c>accept</c> payload, a state action declaration, a <c>render</c>
    ///     target): the names an expression uses as features, and the types, subsetted features,
    ///     and references a declaration names, each with the position of its token.
    /// </summary>
    /// <remarks>
    ///     The subtrees are walked rather than scanned as text because only the syntax tree tells a
    ///     feature reference from everything else an expression is made of. Not collected, by
    ///     design: operators and literals (they are tokens, not names); the function of an
    ///     invocation (<c>f(x)</c>) and of an arrow call (<c>c-&gt;size()</c>), which are library
    ///     functions whose resolution needs implicit imports of the function libraries; the member
    ///     selected by <c>.</c> (<c>a.b</c> yields only <c>a</c>), which is resolved through the
    ///     type of <c>a</c>; parameter names of a named argument; redefinitions (resolved against
    ///     the general types of the owner); and anything inside a nested body (<c>{ … }</c>), whose
    ///     names are declared locally.
    /// </remarks>
    /// <param name="declaredHere">
    ///     Names the construct itself declares (an <c>accept</c> payload's name), which its guard
    ///     and effect may legitimately use; such a name is never a reference. May be null.
    /// </param>
    /// <param name="roots">The subtrees to collect from; null entries are ignored.</param>
    /// <returns>The occurrences in source order, or an empty list when there are none.</returns>
    private IReadOnlyList<ReferenceOccurrence> CollectReferenceOccurrences(
        IReadOnlySet<string>? declaredHere,
        params Antlr4.Runtime.Tree.IParseTree?[] roots)
    {
        var occurrences = new List<ReferenceOccurrence>();
        foreach (var root in roots.OfType<Antlr4.Runtime.Tree.IParseTree>())
        {
            var found = new List<ReferenceOccurrence>();
            CollectOccurrences(root, declaredHere, found);

            // Names declared by bare usages (`in x : T;`, `x : T;`) are members the AST does not
            // model, so name resolution cannot see them; they are filtered here, where the syntax
            // tree still shows them. Computed only when there is something to filter.
            if (found.Count > 0 && BareUsageNames(root) is { Count: > 0 } bareNames)
            {
                found.RemoveAll(occurrence => bareNames.Contains(occurrence.Name));
            }

            occurrences.AddRange(found);
        }

        return occurrences.Count == 0 ? Array.Empty<ReferenceOccurrence>() : occurrences;
    }

    /// <summary>
    ///     The names declared by bare usages, per outermost enclosing declaration, memoized so a
    ///     declaration with many expressions is scanned once.
    /// </summary>
    private readonly Dictionary<Antlr4.Runtime.ParserRuleContext, HashSet<string>> _bareUsageNamesByDeclaration = new();

    /// <summary>
    ///     Returns the names declared by bare usages (<c>in vehicle : Vehicle;</c>,
    ///     <c>x : T;</c> — the grammar's <c>defaultReferenceUsage</c>) anywhere inside the
    ///     outermost declaration (the one directly in a package) that encloses
    ///     <paramref name="tree"/>, or an empty set when there is none.
    /// </summary>
    /// <remarks>
    ///     <see cref="AstBuilder"/> builds no node for such a usage, so its name is a real, visible
    ///     member that name resolution cannot find; an expression using it (the parameters of a
    ///     state or action) would otherwise be flagged. The set is deliberately an
    ///     over-approximation — it spans the whole outermost declaration rather than only the
    ///     bodies enclosing the expression — because a false warning on a valid model costs more
    ///     than a missed warning on a typo that happens to match a nested parameter name.
    /// </remarks>
    private IReadOnlySet<string> BareUsageNames(Antlr4.Runtime.Tree.IParseTree tree)
    {
        // Find the outermost declaration: climb until the parent is a package body element.
        Antlr4.Runtime.ParserRuleContext? outermost = null;
        for (var current = tree.Parent as Antlr4.Runtime.ParserRuleContext;
             current is not null and not (SysMLv2Parser.PackageBodyElementContext or SysMLv2Parser.RootNamespaceContext);
             current = current.Parent as Antlr4.Runtime.ParserRuleContext)
        {
            outermost = current;
        }

        if (outermost is null)
        {
            return new HashSet<string>();
        }

        if (!_bareUsageNamesByDeclaration.TryGetValue(outermost, out var names))
        {
            names = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<Antlr4.Runtime.Tree.IParseTree>();
            pending.Push(outermost);
            while (pending.Count > 0)
            {
                var item = pending.Pop();
                if (item is SysMLv2Parser.BodyExpressionContext)
                {
                    // An inline expression body's parameters are local to it.
                    continue;
                }

                if (item is SysMLv2Parser.DefaultReferenceUsageContext bare &&
                    bare.usage()?.usageDeclaration()?.identification() is { } identification &&
                    GetDeclaredName(identification) is { } declared)
                {
                    names.Add(declared);
                }

                for (var i = 0; i < item.ChildCount; i++)
                {
                    pending.Push(item.GetChild(i));
                }
            }

            _bareUsageNamesByDeclaration[outermost] = names;
        }

        return names;
    }

    /// <summary>
    ///     Recursive worker of <see cref="CollectReferenceOccurrences"/>: records the reference
    ///     forms it recognizes and otherwise descends into the children of <paramref name="tree"/>.
    /// </summary>
    private static void CollectOccurrences(
        Antlr4.Runtime.Tree.IParseTree tree,
        IReadOnlySet<string>? declaredHere,
        List<ReferenceOccurrence> occurrences)
    {
        switch (tree)
        {
            case SysMLv2Parser.BodyExpressionContext or SysMLv2Parser.ActionBodyContext or SysMLv2Parser.ActionBodyItemContext:
                // A nested body declares its own names and is not part of this expression.
                return;
            case SysMLv2Parser.OwnedFeatureTypingContext typing:
                AddOccurrence(typing.qualifiedName(0), declaredHere, occurrences);
                return;
            case SysMLv2Parser.OwnedSubsettingContext subsetting:
                AddOccurrence(subsetting.qualifiedName(0), declaredHere, occurrences);
                return;
            case SysMLv2Parser.OwnedReferenceSubsettingContext referenceSubsetting:
                AddOccurrence(referenceSubsetting.qualifiedName(0), declaredHere, occurrences);
                return;
            case SysMLv2Parser.TypeReferenceContext typeReference:
                AddOccurrence(typeReference.qualifiedName(), declaredHere, occurrences);
                return;
            case SysMLv2Parser.NamedArgumentContext named:
                // The parameter name is not a reference; the argument expression is.
                CollectOccurrencesFromChild(named.ownedExpression(), declaredHere, occurrences);
                return;
            case SysMLv2Parser.ConstructorExpressionContext constructor:
                AddOccurrence(constructor.qualifiedName(), declaredHere, occurrences);
                CollectOccurrencesFromChild(constructor.argumentList(), declaredHere, occurrences);
                return;
            case SysMLv2Parser.BaseExpressionContext { } baseExpression when baseExpression.qualifiedName() is { } name:
                if (baseExpression.argumentList() is { } arguments)
                {
                    // An invocation: its function is not checked, its arguments are expressions.
                    CollectOccurrencesFromChild(arguments, declaredHere, occurrences);
                }
                else
                {
                    AddOccurrence(name, declaredHere, occurrences);
                }

                return;
        }

        for (var i = 0; i < tree.ChildCount; i++)
        {
            CollectOccurrences(tree.GetChild(i), declaredHere, occurrences);
        }
    }

    /// <summary>Null-tolerant entry to <see cref="CollectOccurrences"/> for an optional child context.</summary>
    private static void CollectOccurrencesFromChild(
        Antlr4.Runtime.Tree.IParseTree? child,
        IReadOnlySet<string>? declaredHere,
        List<ReferenceOccurrence> occurrences)
    {
        if (child is not null)
        {
            CollectOccurrences(child, declaredHere, occurrences);
        }
    }

    /// <summary>
    ///     Records <paramref name="name"/> as a reference occurrence at its first token, unless it
    ///     is missing or malformed (a parse error already reported it), an implicit feature name
    ///     (see <see cref="ImplicitFeatureNames"/>), or a name the enclosing construct declares.
    /// </summary>
    private static void AddOccurrence(
        SysMLv2Parser.QualifiedNameContext? name,
        IReadOnlySet<string>? declaredHere,
        List<ReferenceOccurrence> occurrences)
    {
        if (name is null || name.exception is not null || name.Start is not { } start)
        {
            return;
        }

        var text = name.GetText();
        if (text.Length == 0 || ImplicitFeatureNames.Contains(text) || declaredHere?.Contains(text) == true)
        {
            return;
        }

        occurrences.Add(new ReferenceOccurrence(text, new SourcePosition(start.Line, start.Column + 1)));
    }

    /// <summary>
    ///     Returns the name an <c>accept</c> payload declares for itself (<c>accept sig : Sig</c>
    ///     declares <c>sig</c>), as a set the guard and effect of the same transition may use; an
    ///     empty set when the payload is unnamed or absent.
    /// </summary>
    private static IReadOnlySet<string> DeclaredPayloadNames(SysMLv2Parser.AcceptParameterPartContext? acceptPart)
    {
        var payload = acceptPart?.payloadParameterMember()?.payloadParameter();
        var identification = payload?.payloadFeature()?.identification() ?? payload?.identification();
        var name = identification is null ? null : GetDeclaredName(identification);
        return name is null ? new HashSet<string>() : new HashSet<string> { name };
    }

    /// <summary>
    ///     Collects the reference occurrences of a transition's trigger, guard, and effect (in
    ///     source order) — the
    ///     parts the transition keeps only as raw text.
    /// </summary>
    private IReadOnlyList<ReferenceOccurrence> TransitionOccurrences(
        SysMLv2Parser.GuardExpressionMemberContext? guard,
        SysMLv2Parser.TriggerActionMemberContext? trigger,
        SysMLv2Parser.EffectBehaviorMemberContext? effect) =>
        CollectReferenceOccurrences(
            DeclaredPayloadNames(trigger?.triggerAction()?.acceptParameterPart()),
            trigger?.triggerAction(),
            guard?.ownedExpression(),
            effect?.effectBehaviorUsage());

    /// <summary>
    ///     Extracts the raw reference text of a <c>render &lt;target&gt;;</c> statement, preferring
    ///     the direct-reference form (<c>ownedReferenceSubsetting</c>) and falling back to the
    ///     typed-placeholder form's feature typing — the same two-form fallback pattern
    ///     <see cref="VisitSatisfyRequirementUsage"/> uses for <c>satisfy</c>'s two grammar forms.
    /// </summary>
    private static string? ExtractRenderTargetName(SysMLv2Parser.ViewRenderingUsageContext? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return usage.ownedReferenceSubsetting()?.GetText()
            ?? ExtractFeatureTyping(usage.featureSpecializationPart())
            ?? usage.usage()?.GetText();
    }

    /// <summary>
    ///     Collects each <c>expose &lt;name&gt;;</c> member in a <c>view</c> usage's body, in
    ///     source order, paired with its own bracket-filter text (if any) — reusing
    ///     <see cref="ExtractImportTarget"/>, the same namespace/membership-import shape
    ///     <c>import</c> statements use.
    /// </summary>
    private static IReadOnlyList<ExposeMember> ExtractExposedNames(
        IEnumerable<SysMLv2Parser.ViewBodyItemContext> bodyItems)
    {
        var members = new List<ExposeMember>();
        foreach (var item in bodyItems)
        {
            var expose = item.expose();
            if (expose is null)
            {
                continue;
            }

            var isNamespaceForm = expose.namespaceExpose() is not null;
            var (qn, _, bracketFilterText, isRecursive, referenceText) = ExtractImportTarget(
                expose.namespaceExpose()?.namespaceImport(),
                expose.membershipExpose()?.membershipImport());
            if (qn is { Length: > 0 })
            {
                var hasBracketFilter = bracketFilterText is not null;
                var recursionKind = (isNamespaceForm, isRecursive, hasBracketFilter) switch
                {
                    (true, _, true) => ExposeRecursionKind.NamespaceRecursive,
                    (false, _, true) => ExposeRecursionKind.MembershipRecursive,
                    (true, true, false) => ExposeRecursionKind.NamespaceRecursive,
                    (true, false, false) => ExposeRecursionKind.NamespaceDirectChildren,
                    (false, true, false) => ExposeRecursionKind.MembershipRecursive,
                    (false, false, false) => ExposeRecursionKind.MembershipExact,
                };
                members.Add(new ExposeMember(qn, bracketFilterText, recursionKind, referenceText));
            }
        }

        return members;
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitViewpointDefinition(SysMLv2Parser.ViewpointDefinitionContext context)
    {
        var name = GetDeclaredName(context.definitionDeclaration()?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(
            context.definitionDeclaration()?.subclassificationPart());

        return new SysmlViewpointNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            SupertypeNames = supertypeNames,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitImportRule(SysMLv2Parser.ImportRuleContext context)
    {
        var decl = context.importDeclaration();
        if (decl is null)
        {
            return null;
        }

        var (qn, isWildcard, bracketFilterText, isRecursive, referenceText) = ExtractImportTarget(decl.namespaceImport(), decl.membershipImport());
        if (qn is null)
        {
            return null;
        }

        return new SysmlImportNode
        {
            ImportedNamespace = qn,
            ImportedNames = [qn],
            RawReferenceText = referenceText,
            IsWildcard = isWildcard,
            IsRecursive = isRecursive,
            IsMembershipImport = decl.membershipImport() is not null,
            BracketFilterExpressionText = bracketFilterText,
            IsImportAll = context.ALL() is not null,
            Visibility = ToVisibility(context.visibilityIndicator(), SysmlVisibility.Private),
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAliasMember(SysMLv2Parser.AliasMemberContext context) =>
        BuildAliasNode(context.LT() is not null, context.name(), context.qualifiedName());

    /// <inheritdoc/>
    public override SysmlNode? VisitDefinitionBodyItemContent(SysMLv2Parser.DefinitionBodyItemContentContext context) =>
        context.ALIAS() is not null
            ? BuildAliasNode(context.LT() is not null, context.name(), context.qualifiedName())
            : base.VisitDefinitionBodyItemContent(context);

    /// <summary>
    ///     Builds a <see cref="SysmlAliasNode"/> from an alias member's optional short name, optional
    ///     name, and aliased qualified name, or returns null when the alias declares neither name.
    /// </summary>
    private static SysmlAliasNode? BuildAliasNode(
        bool hasShortName, SysMLv2Parser.NameContext[] names, SysMLv2Parser.QualifiedNameContext? target)
    {
        string? shortName = null;
        string? name;
        if (hasShortName)
        {
            shortName = names.Length > 0 ? names[0].GetText() : null;
            name = names.Length > 1 ? names[1].GetText() : null;
        }
        else
        {
            name = names.FirstOrDefault()?.GetText();
        }
        if ((name is null && shortName is null) || target is null)
        {
            return null;
        }

        return new SysmlAliasNode { Name = name, ShortName = shortName, TargetName = target.GetText() };
    }

    /// <summary>
    ///     Converts an optional visibility indicator to a <see cref="SysmlVisibility"/>, returning
    ///     <paramref name="defaultVisibility"/> when no indicator is present.
    /// </summary>
    private static SysmlVisibility ToVisibility(
        SysMLv2Parser.VisibilityIndicatorContext? indicator, SysmlVisibility defaultVisibility) =>
        indicator switch
        {
            null => defaultVisibility,
            _ when indicator.PRIVATE() is not null => SysmlVisibility.Private,
            _ when indicator.PROTECTED() is not null => SysmlVisibility.Protected,
            _ => SysmlVisibility.Public,
        };

    /// <summary>
    ///     Applies a body element's declared membership visibility (from its <c>memberPrefix</c>)
    ///     and its declared short name (from the element's own leading <c>identification</c>) to
    ///     the node built from it. Imports carry their own visibility from
    ///     <see cref="VisitImportRule"/> and so are left as built; aliases take the member
    ///     visibility but keep the short name <see cref="BuildAliasNode"/> already extracted. Unless
    ///     <paramref name="applyModifiers"/> is false, the element's leading prefix keywords are also
    ///     applied (see <see cref="ApplyLeadingModifiers"/>).
    /// </summary>
    private static void ApplyMembershipDetails(
        SysmlNode node,
        SysMLv2Parser.MemberPrefixContext? memberPrefix,
        Antlr4.Runtime.ParserRuleContext element,
        bool applyModifiers = true)
    {
        if (node is SysmlImportNode)
        {
            return;
        }

        node.Visibility = ToVisibility(memberPrefix?.visibilityIndicator(), SysmlVisibility.Public);
        if (node is not SysmlAliasNode &&
            node.Name is not null &&
            FindFirstIdentification(element) is { } identification &&
            identification.LT() is not null &&
            GetDeclaredName(identification) == node.Name)
        {
            node.ShortName = identification.name()[0].GetText();
        }

        if (applyModifiers)
        {
            ApplyLeadingModifiers(node, element);
        }
    }

    /// <summary>
    ///     Applies <see cref="ApplyMembershipDetails(SysmlNode, SysMLv2Parser.MemberPrefixContext?, Antlr4.Runtime.ParserRuleContext, bool)"/>
    ///     to every node built from one body item of a specialized (type, state, action, or
    ///     requirement) body, using the item's own member prefix. Leading modifiers apply only to
    ///     the first definition or feature node, so transitions synthesized from the same item
    ///     (a <see cref="MultiNodeCapture"/>) never receive them.
    /// </summary>
    private static void ApplyMembershipDetails(IEnumerable<SysmlNode> nodes, Antlr4.Runtime.Tree.IParseTree item)
    {
        if (item is not Antlr4.Runtime.ParserRuleContext context)
        {
            return;
        }

        var memberPrefix = FindMemberPrefix(context);
        var modifiersApplied = false;
        foreach (var node in nodes)
        {
            var applyModifiers = !modifiersApplied && node is SysmlDefinitionNode or SysmlFeatureNode;
            modifiersApplied |= applyModifiers;
            ApplyMembershipDetails(node, memberPrefix, context, applyModifiers);
        }
    }

    /// <summary>
    ///     Applies the leading prefix keywords of a body element (<c>abstract</c>, <c>variation</c>,
    ///     <c>individual</c>, <c>variant</c>, <c>ref</c>, and the <c>in</c>/<c>out</c>/<c>inout</c>
    ///     direction) to the definition or feature node built from it. Walks the element's terminal
    ///     tokens in order, skips the member's visibility keyword and a leading <c>then</c>,
    ///     consumes tokens while they are prefix keywords (also tolerating <c>derived</c> and
    ///     <c>constant</c>, which are not captured), and stops at the first other token (the kind
    ///     keyword, an identifier, <c>#</c>, ...). Done centrally so that every definition and usage
    ///     visitor is covered without per-visitor edits. Nodes other than definitions and features
    ///     are left untouched.
    /// </summary>
    private static void ApplyLeadingModifiers(SysmlNode node, Antlr4.Runtime.ParserRuleContext element)
    {
        if (node is not (SysmlDefinitionNode or SysmlFeatureNode))
        {
            return;
        }

        var isAbstract = false;
        var isVariation = false;
        var isIndividual = false;
        var isVariant = false;
        var isReference = false;
        SysmlFeatureDirection? direction = null;
        var leading = true;
        foreach (var tokenType in EnumerateTokens(element).Select(token => token.Type))
        {
            if (leading && tokenType is SysMLv2Parser.PUBLIC or SysMLv2Parser.PRIVATE
                or SysMLv2Parser.PROTECTED or SysMLv2Parser.THEN)
            {
                continue;
            }

            leading = false;
            switch (tokenType)
            {
                case SysMLv2Parser.ABSTRACT:
                    isAbstract = true;
                    continue;
                case SysMLv2Parser.VARIATION:
                    isVariation = true;
                    continue;
                case SysMLv2Parser.INDIVIDUAL:
                    isIndividual = true;
                    continue;
                case SysMLv2Parser.VARIANT:
                    isVariant = true;
                    continue;
                case SysMLv2Parser.REF:
                    isReference = true;
                    continue;
                case SysMLv2Parser.IN:
                    direction = SysmlFeatureDirection.In;
                    continue;
                case SysMLv2Parser.OUT:
                    direction = SysmlFeatureDirection.Out;
                    continue;
                case SysMLv2Parser.INOUT:
                    direction = SysmlFeatureDirection.InOut;
                    continue;
                case SysMLv2Parser.DERIVED:
                case SysMLv2Parser.CONSTANT:
                    continue;
            }

            break;
        }

        switch (node)
        {
            case SysmlDefinitionNode definition:
                definition.IsAbstract = isAbstract;
                definition.IsVariation = isVariation;
                definition.IsIndividual = isIndividual;
                break;
            case SysmlFeatureNode feature:
                feature.IsAbstract = isAbstract;
                feature.IsVariation = isVariation;
                feature.IsIndividual = isIndividual;
                feature.IsVariant = isVariant;
                feature.IsReference = isReference;
                feature.Direction = direction;
                break;
        }
    }

    /// <summary>
    ///     Lazily enumerates the terminal-node tokens of a parse tree in source (pre-)order, so a
    ///     caller that needs only the first few tokens does not walk the whole subtree.
    /// </summary>
    private static IEnumerable<Antlr4.Runtime.IToken> EnumerateTokens(Antlr4.Runtime.Tree.IParseTree tree)
    {
        if (tree is Antlr4.Runtime.Tree.ITerminalNode terminal)
        {
            yield return terminal.Symbol;
            yield break;
        }

        for (var i = 0; i < tree.ChildCount; i++)
        {
            foreach (var token in EnumerateTokens(tree.GetChild(i)))
            {
                yield return token;
            }
        }
    }

    /// <summary>
    ///     Returns a body item's own <c>memberPrefix</c>: the first one in a pre-order walk that
    ///     precedes the item's first <c>identification</c>, <c>{</c>, or <c>;</c> (a member prefix
    ///     always precedes the member's declaration, so prefixes of nested body members — even
    ///     of an anonymous member — are never reached), or null.
    /// </summary>
    private static SysMLv2Parser.MemberPrefixContext? FindMemberPrefix(Antlr4.Runtime.Tree.IParseTree tree)
    {
        var stop = false;
        return Find(tree);

        SysMLv2Parser.MemberPrefixContext? Find(Antlr4.Runtime.Tree.IParseTree node)
        {
            if (stop)
            {
                return null;
            }

            switch (node)
            {
                case SysMLv2Parser.MemberPrefixContext prefix:
                    return prefix;
                case SysMLv2Parser.IdentificationContext:
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol.Text: "{" or ";" }:
                    stop = true;
                    return null;
            }

            for (var i = 0; i < node.ChildCount && !stop; i++)
            {
                if (Find(node.GetChild(i)) is { } found)
                {
                    return found;
                }
            }

            return null;
        }
    }

    /// <summary>
    ///     Returns the first <c>identification</c> in a pre-order walk of <paramref name="tree"/> —
    ///     an element declaration's own identification, which precedes its body — or null.
    /// </summary>
    private static SysMLv2Parser.IdentificationContext? FindFirstIdentification(Antlr4.Runtime.Tree.IParseTree tree)
    {
        if (tree is SysMLv2Parser.IdentificationContext identification)
        {
            return identification;
        }

        for (var i = 0; i < tree.ChildCount; i++)
        {
            if (FindFirstIdentification(tree.GetChild(i)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///     Extracts the qualified/dotted name text and wildcard flag from either an import's
    ///     namespace form (<c>qualifiedName::*</c>, a wildcard by definition) or membership form
    ///     (<c>qualifiedName</c>, optionally <c>::**</c> for a recursive wildcard). Shared by
    ///     <see cref="VisitImportRule"/>'s <c>import</c> handling and <see cref="ExtractExposedNames"/>'s
    ///     <c>expose</c> handling, since both grammar constructs wrap the identical
    ///     <c>namespaceImport</c>/<c>membershipImport</c> shapes — extracted here per the
    ///     Copy-Paste Programming anti-pattern in coding-principles.md rather than duplicating the
    ///     extraction logic at both call sites.
    /// </summary>
    /// <param name="namespaceImport">The wildcard-import alternative, or null when not this form.</param>
    /// <param name="membershipImport">The membership-import alternative, or null when not this form.</param>
    /// <returns>
    ///     The extracted qualified/dotted name text (or null when neither alternative yielded
    ///     text), whether the import/expose is a wildcard, the bracket-filter expression text (if
    ///     any), whether this entry is recursive (derived from a trailing <c>::**</c>, or
    ///     always true for bracket-filtered forms by design, since the grammar only reaches that
    ///     alternative via <c>::**[filterExpr]</c>), and the reference text as written
    ///     (<c>GetText()</c> of the innermost import-shaped context: the qualified name plus any
    ///     <c>::*</c>/<c>::**</c> marker, never the bracket-filter members).
    /// </returns>
    private static (string? QualifiedName, bool IsWildcard, string? BracketFilterExpressionText, bool IsRecursive, string? ReferenceText) ExtractImportTarget(
        SysMLv2Parser.NamespaceImportContext? namespaceImport,
        SysMLv2Parser.MembershipImportContext? membershipImport)
    {
        // Namespace import: qualifiedName::* — wildcard, all members of the namespace are in scope
        if (namespaceImport is not null)
        {
            var qn = namespaceImport.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                return (qn, true, null, namespaceImport.STAR_STAR() is not null, namespaceImport.GetText());
            }

            // Bracketed-filter form: qualifiedName::**[filterExpr] — the dominant expose form in
            // the real OMG corpus. The grammar nests the qualified name two levels deeper here:
            // namespaceImport -> filterPackage -> filterPackageImportDeclaration -> (membershipImport
            // | namespaceImportDirect). Descend through that chain rather than only checking the
            // direct qualifiedName() child (which is null for this alternative). The bracket
            // expression text itself is captured raw here from the filterPackage's first
            // filterPackageMember (multiple bracket filters chained on one path are extremely
            // rare; the first is representative); it is paired with its ExposeMember by
            // ExtractExposedNames and evaluated per-entry by ExposeScopeResolver (Phase 2a).
            // Bracket-filtered forms are always treated as recursive (IsRecursive: true)
            // regardless of the nested optional STAR_STAR token — the grammar's filterPackage
            // form is treated as always recursive by design (the grammar only reaches this
            // alternative via ::**[filterExpr]).
            var filterPackage = namespaceImport.filterPackage();
            var bracketFilterText = filterPackage?.filterPackageMember()?.FirstOrDefault()?.ownedExpression() is { } bracketExpr
                ? GetOriginalText(bracketExpr)
                : null;
            var filterDecl = filterPackage?.filterPackageImportDeclaration();
            if (filterDecl is not null)
            {
                var filterMembershipImport = filterDecl.membershipImport();
                if (filterMembershipImport is not null)
                {
                    var filterQn = filterMembershipImport.qualifiedName()?.GetText();
                    if (filterQn is { Length: > 0 })
                    {
                        return (filterQn, filterMembershipImport.STAR_STAR() is not null, bracketFilterText, true, filterMembershipImport.GetText());
                    }
                }

                var namespaceImportDirect = filterDecl.namespaceImportDirect();
                if (namespaceImportDirect is not null)
                {
                    var directQn = namespaceImportDirect.qualifiedName()?.GetText();
                    if (directQn is { Length: > 0 })
                    {
                        return (directQn, true, bracketFilterText, true, namespaceImportDirect.GetText());
                    }
                }
            }
        }

        // Membership import: qualifiedName (optional ::**)
        // The ** form is a recursive wildcard; either way it enables lookup under the namespace
        if (membershipImport is not null)
        {
            var qn = membershipImport.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                var isRecursive = membershipImport.STAR_STAR() is not null;
                return (qn, isRecursive, null, isRecursive, membershipImport.GetText());
            }
        }

        return (null, false, null, false, null);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitDataType(SysMLv2Parser.DataTypeContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "datatype");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitClass(SysMLv2Parser.ClassContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "class");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitStructure(SysMLv2Parser.StructureContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "struct");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAssociation(SysMLv2Parser.AssociationContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "assoc");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAssociationStructure(SysMLv2Parser.AssociationStructureContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "assoc struct");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitFunction(SysMLv2Parser.FunctionContext context)
    {
        return BuildClassifierNode(
            context.classifierDeclaration(), () => CollectFunctionBodyItems(context.functionBody()), "function");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitPredicate(SysMLv2Parser.PredicateContext context)
    {
        return BuildClassifierNode(
            context.classifierDeclaration(), () => CollectFunctionBodyItems(context.functionBody()), "predicate");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitClassifier(SysMLv2Parser.ClassifierContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "classifier");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitMetaclass(SysMLv2Parser.MetaclassContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "metaclass");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitBehavior(SysMLv2Parser.BehaviorContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "behavior");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitInteraction(SysMLv2Parser.InteractionContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), context.typeBody(), "interaction");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitType(SysMLv2Parser.TypeContext context)
    {
        var declaration = context.typeDeclaration();
        var supertypeNames = declaration?.specializationPart()
            .SelectMany(part => part.ownedSpecialization())
            .Select(specialization => specialization.GetText())
            .Where(text => text.Length > 0)
            .ToList() ?? [];
        return BuildClassifierNode(
            GetDeclaredName(declaration?.identification()),
            supertypeNames,
            () => CollectTypeBodyItems(context.typeBody()?.typeBodyElement() ?? []),
            "type");
    }

    /// <summary>
    ///     Builds a definition AST node from a KerML classifier declaration (datatype, class, struct,
    ///     assoc, classifier, metaclass, behavior, interaction) with a type body.
    /// </summary>
    private SysmlDefinitionNode? BuildClassifierNode(
        SysMLv2Parser.ClassifierDeclarationContext? decl,
        SysMLv2Parser.TypeBodyContext? body,
        string keyword)
    {
        return BuildClassifierNode(decl, () => CollectTypeBodyItems(body?.typeBodyElement() ?? []), keyword);
    }

    /// <summary>
    ///     Builds a definition AST node from a KerML classifier declaration whose body members are
    ///     collected by <paramref name="collectBody"/> (a type body, or a function body for
    ///     <c>function</c> and <c>predicate</c>).
    /// </summary>
    private SysmlDefinitionNode? BuildClassifierNode(
        SysMLv2Parser.ClassifierDeclarationContext? decl,
        Func<(IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)> collectBody,
        string keyword)
    {
        return BuildClassifierNode(
            GetDeclaredName(decl?.identification()),
            GetSuperclassingSupertypes(decl?.superclassingPart()),
            collectBody,
            keyword);
    }

    /// <summary>
    ///     Builds a definition AST node from an already-extracted declared name and supertype list,
    ///     collecting the members that the body callback yields underneath the new node's name.
    ///     Returns null for an unnamed declaration.
    /// </summary>
    private SysmlDefinitionNode? BuildClassifierNode(
        string? name,
        IReadOnlyList<string> supertypeNames,
        Func<(IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)> collectBody,
        string keyword)
    {
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);

        _namespaceStack.Add(name);
        var (children, annotations) = collectBody();
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = keyword,
            SupertypeNames = supertypeNames,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <summary>
    ///     Collects the member nodes of a KerML function or predicate body — its
    ///     <c>kermlFunctionBodyPart</c> items, which are KerML type body elements (parameters such
    ///     as <c>in x : T;</c>) and <c>return</c> feature members — in source order. The trailing
    ///     result expression is not a declaration and is ignored.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations) CollectFunctionBodyItems(
        SysMLv2Parser.FunctionBodyContext? body)
    {
        var children = new List<SysmlNode>();
        var annotations = new List<SysmlAnnotation>();
        foreach (var item in body?.kermlFunctionBodyPart()?.children?.OfType<Antlr4.Runtime.ParserRuleContext>() ?? [])
        {
            if (item is not (SysMLv2Parser.TypeBodyElementContext or SysMLv2Parser.ReturnFeatureMemberContext))
            {
                continue;
            }

            var node = Visit(item);
            if (node is AnnotationCapture capture)
            {
                annotations.Add(capture.Annotation);
            }
            else if (node is not null)
            {
                ApplyMembershipDetails(node, FindMemberPrefix(item), item);
                children.Add(node);
            }
        }

        return (children, annotations);
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitReturnFeatureMember(SysMLv2Parser.ReturnFeatureMemberContext context)
    {
        return Visit(context.featureElement());
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitFeature(SysMLv2Parser.FeatureContext context)
    {
        return BuildKermlFeatureNode(context.featureDeclaration(), "feature", TypeBodyCollector(context.typeBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitStep(SysMLv2Parser.StepContext context)
    {
        return BuildKermlFeatureNode(context.featureDeclaration(), "step", TypeBodyCollector(context.typeBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitExpression(SysMLv2Parser.ExpressionContext context)
    {
        return BuildKermlFeatureNode(context.featureDeclaration(), "expr", FunctionBodyCollector(context.functionBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitBooleanExpression(SysMLv2Parser.BooleanExpressionContext context)
    {
        return BuildKermlFeatureNode(context.featureDeclaration(), "bool", FunctionBodyCollector(context.functionBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitInvariant(SysMLv2Parser.InvariantContext context)
    {
        return BuildKermlFeatureNode(context.featureDeclaration(), "inv", FunctionBodyCollector(context.functionBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConnector(SysMLv2Parser.ConnectorContext context)
    {
        var connectorDeclaration = context.connectorDeclaration();
        var declaration = context.featureDeclaration()
            ?? connectorDeclaration?.binaryConnectorDeclaration()?.featureDeclaration()
            ?? connectorDeclaration?.naryConnectorDeclaration()?.featureDeclaration();
        return BuildKermlFeatureNode(declaration, "connector", TypeBodyCollector(context.typeBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitBindingConnector(SysMLv2Parser.BindingConnectorContext context)
    {
        return BuildKermlFeatureNode(
            context.bindingConnectorDeclaration()?.featureDeclaration(), "binding", TypeBodyCollector(context.typeBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSuccession(SysMLv2Parser.SuccessionContext context)
    {
        return BuildKermlFeatureNode(
            context.successionDeclaration()?.featureDeclaration(), "succession", TypeBodyCollector(context.typeBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitFlow(SysMLv2Parser.FlowContext context)
    {
        return BuildKermlFeatureNode(
            context.flowDeclaration()?.featureDeclaration(), "flow", TypeBodyCollector(context.typeBody()));
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSuccessionFlow(SysMLv2Parser.SuccessionFlowContext context)
    {
        return BuildKermlFeatureNode(
            context.flowDeclaration()?.featureDeclaration(), "succession flow", TypeBodyCollector(context.typeBody()));
    }

    /// <summary>
    ///     Returns a callback collecting the members of a KerML type body.
    /// </summary>
    private Func<(IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)> TypeBodyCollector(
        SysMLv2Parser.TypeBodyContext? body) =>
        () => CollectTypeBodyItems(body?.typeBodyElement() ?? []);

    /// <summary>
    ///     Returns a callback collecting the members of a KerML function body.
    /// </summary>
    private Func<(IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)> FunctionBodyCollector(
        SysMLv2Parser.FunctionBodyContext? body) =>
        () => CollectFunctionBodyItems(body);

    /// <summary>
    ///     Builds a feature AST node from a KerML feature declaration (<c>feature</c>, <c>step</c>,
    ///     <c>expr</c>, <c>bool</c>, <c>inv</c>, <c>connector</c>, <c>binding</c>, <c>succession</c>,
    ///     <c>flow</c>, <c>succession flow</c>), capturing the declared name and short name, the first
    ///     typing, subsetting targets, the first redefinition, the multiplicity, and the nested
    ///     members. A member declared without a name (an anonymous invariant or step, or a feature
    ///     that only redefines another) keeps its typing, subsetting, and redefinition references
    ///     for reference checking but has no name, so it registers no symbol.
    /// </summary>
    /// <param name="declaration">The feature declaration, or null when the member has none.</param>
    /// <param name="keyword">The KerML feature keyword recorded as the node's feature keyword.</param>
    /// <param name="collectBody">Callback collecting the members of the feature's body.</param>
    private SysmlFeatureNode BuildKermlFeatureNode(
        SysMLv2Parser.FeatureDeclarationContext? declaration,
        string keyword,
        Func<(IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)> collectBody)
    {
        var identification = declaration?.featureIdentification();
        var name = identification is null ? null : GetDeclaredName(identification);
        var shortName = name is not null && identification?.LT() is not null
            ? identification.name()[0].GetText()
            : null;
        var specialization = declaration?.featureSpecializationPart();

        if (name is not null)
        {
            _namespaceStack.Add(name);
        }

        var (children, annotations) = collectBody();

        if (name is not null)
        {
            _namespaceStack.RemoveAt(_namespaceStack.Count - 1);
        }

        return new SysmlFeatureNode
        {
            Name = name,
            ShortName = shortName,
            QualifiedName = name is not null ? QualifyName(name) : null,
            FeatureKeyword = keyword,
            FeatureTyping = ExtractFeatureTyping(specialization),
            RedefinedFeatureName = ExtractRedefinedFeature(specialization),
            SupertypeNames = ExtractSubsettingTargetNames(specialization),
            Multiplicity = ExtractMultiplicity(specialization),
            Children = children,
            Annotations = annotations,
        };
    }

    /// <summary>
    ///     Extracts supertype qualified names from a <see cref="SysMLv2Parser.SuperclassingPartContext"/>.
    /// </summary>
    private static IReadOnlyList<string> GetSuperclassingSupertypes(
        SysMLv2Parser.SuperclassingPartContext? part)
    {
        if (part is null)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var owned in part.ownedSubclassification())
        {
            var qn = owned.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                names.Add(qn);
            }
        }

        return names;
    }

    /// <summary>
    ///     Collects child nodes from an array of <see cref="SysMLv2Parser.TypeBodyElementContext"/>.
    ///     Comment/documentation annotations nested among the items are separated out into
    ///     <see cref="SysmlAnnotation"/> entries rather than becoming children.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations) CollectTypeBodyItems(
        IEnumerable<SysMLv2Parser.TypeBodyElementContext> items)
    {
        var children = new List<SysmlNode>();
        var annotations = new List<SysmlAnnotation>();
        foreach (var item in items)
        {
            var node = Visit(item);
            if (node is AnnotationCapture capture)
            {
                annotations.Add(capture.Annotation);
            }
            else if (node is not null)
            {
                ApplyMembershipDetails(node, FindMemberPrefix(item), item);
                children.Add(node);
            }
        }

        return (children, annotations);
    }


    /// <summary>
    ///     Builds a definition AST node from a bare <see cref="SysMLv2Parser.DefinitionDeclarationContext"/>
    ///     for definition kinds whose grammar rule uses a specialized body (e.g. action, state,
    ///     requirement, enum) rather than the generic <c>definition</c> rule.
    /// </summary>
    /// <param name="decl">The definition declaration (name/supertypes) to build from.</param>
    /// <param name="keyword">The definition keyword (e.g. "requirement def").</param>
    /// <param name="specializedBody">
    ///     Optional specialized body (e.g. a <see cref="SysMLv2Parser.CaseBodyContext"/>) to scan
    ///     for nested <c>verify</c> members via <see cref="FindVerificationMembers"/>. Defaults to
    ///     <see langword="null"/>, which is behavior-neutral for callers that don't have (or don't
    ///     need to scan) a specialized body.
    /// </param>
    /// <remarks>
    ///     Only the declared name, supertype names, and (when <paramref name="specializedBody"/> is
    ///     given) nested verified-requirement names are captured. The specialized body's other
    ///     contents (nested usages and compartment members) are not yet collected; that is handled
    ///     in a later phase that adds usage and compartment rendering.
    /// </remarks>
    private SysmlDefinitionNode? BuildDefinitionFromDeclaration(
        SysMLv2Parser.DefinitionDeclarationContext? decl,
        string keyword,
        Antlr4.Runtime.Tree.IParseTree? specializedBody = null)
    {
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());
        var verifiedRequirementNames = specializedBody is not null
            ? FindVerificationMembers(specializedBody)
            : Array.Empty<string>();

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = keyword,
            SupertypeNames = supertypeNames,
            VerifiedRequirementNames = verifiedRequirementNames,
        };
    }

    /// <summary>
    ///     Builds a definition AST node from the given <see cref="SysMLv2Parser.DefinitionContext"/>.
    /// </summary>
    private SysmlDefinitionNode? BuildDefinitionNode(
        SysMLv2Parser.DefinitionContext? definition,
        string keyword)
    {
        if (definition is null)
        {
            return null;
        }

        var decl = definition.definitionDeclaration();
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);

        // Collect supertype names from subclassificationPart
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());

        // Collect body children
        _namespaceStack.Add(name);
        var (children, annotations) = CollectDefinitionBodyItems(definition.definitionBody()?.definitionBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = keyword,
            SupertypeNames = supertypeNames,
            Children = children,
            Annotations = annotations,
        };
    }

    /// <summary>
    ///     Extracts supertype qualified names from a <see cref="SysMLv2Parser.SubclassificationPartContext"/>.
    /// </summary>
    private static IReadOnlyList<string> GetSubclassificationSupertypes(
        SysMLv2Parser.SubclassificationPartContext? part)
    {
        if (part is null)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var owned in part.ownedSubclassification())
        {
            var qn = owned.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                names.Add(qn);
            }
        }

        return names;
    }

    /// <summary>
    ///     Extracts the declared name from a <see cref="SysMLv2Parser.FeatureIdentificationContext"/>
    ///     (<c>&lt;shortName&gt; declaredName?</c> or <c>declaredName</c>), or null when only a short
    ///     name is present.
    /// </summary>
    private static string? GetDeclaredName(SysMLv2Parser.FeatureIdentificationContext identification)
    {
        var names = identification.name();
        if (identification.LT() is not null)
        {
            return names.Length >= 2 ? names[1].GetText() : null;
        }

        return names.Length >= 1 ? names[0].GetText() : null;
    }

    /// <summary>
    ///     Extracts the declared name from an <see cref="SysMLv2Parser.IdentificationContext"/>.
    /// </summary>
    /// <remarks>
    ///     The grammar has three alternatives:
    ///     <list type="bullet">
    ///         <item>Alt 1: <c>&lt;shortName&gt; declaredName</c> → 2 name() children; declared name is name(1).</item>
    ///         <item>Alt 2: <c>&lt;shortName&gt;</c> → 1 name() child with LT present; no declared name.</item>
    ///         <item>Alt 3: <c>declaredName</c> → 1 name() child without LT; declared name is name(0).</item>
    ///     </list>
    /// </remarks>
    private static string? GetDeclaredName(SysMLv2Parser.IdentificationContext? identification)
    {
        if (identification is null)
        {
            return null;
        }

        var names = identification.name();
        if (names.Length == 0)
        {
            return null;
        }

        // Alt 1 or Alt 2: there is a '<' token
        if (identification.LT() != null)
        {
            // Alt 1: < shortName > declaredName → 2 names; declared name is names[1]
            // Alt 2: < shortName > → 1 name; no declared name
            return names.Length >= 2 ? names[1].GetText() : null;
        }

        // Alt 3: just the declared name
        return names[0].GetText();
    }

    /// <summary>
    ///     Collects child nodes by visiting an arbitrary sequence of parse-tree contexts, keeping
    ///     each non-null result. Used for specialized bodies (e.g. state bodies) whose item type
    ///     differs from the generic definition body item. Comment/documentation annotations
    ///     nested among the items are separated out into <see cref="SysmlAnnotation"/> entries
    ///     rather than becoming children.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations) CollectChildren(
        IEnumerable<Antlr4.Runtime.Tree.IParseTree> items)
    {
        var children = new List<SysmlNode>();
        var annotations = new List<SysmlAnnotation>();
        foreach (var item in items)
        {
            var node = Visit(item);
            if (node is AnnotationCapture capture)
            {
                annotations.Add(capture.Annotation);
            }
            else if (node is MultiNodeCapture multi)
            {
                ApplyMembershipDetails(multi.Nodes, item);
                children.AddRange(multi.Nodes);
            }
            else if (node is not null)
            {
                ApplyMembershipDetails([node], item);
                children.Add(node);
            }
        }

        return (children, annotations);
    }

    /// <summary>
    ///     Collects an action body's child nodes by visiting its <c>actionBodyItem</c>s in source
    ///     order, tracking <see cref="_actionBodyPreviousNodeName"/> as it goes so
    ///     <see cref="VisitActionBodyItem"/> can resolve the implicit incoming <c>Source</c> of a
    ///     leading bare <c>then</c> (<c>sourceSuccessionMember</c>) on any sibling. This is
    ///     deliberately kept separate from the generic <see cref="CollectChildren"/> (which
    ///     continues to serve state bodies and other item kinds unmodified) because only action
    ///     bodies currently need this order-sensitive "current flow position" bookkeeping — the
    ///     grammar's leading marker carries no name of its own, so its identity must come from
    ///     traversal state maintained here rather than from anything inside the visited node.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations)
        CollectActionBodyChildren(IEnumerable<SysMLv2Parser.ActionBodyItemContext> items)
    {
        var children = new List<SysmlNode>();
        var annotations = new List<SysmlAnnotation>();
        var savedPreviousName = _actionBodyPreviousNodeName;
        _actionBodyPreviousNodeName = null;
        try
        {
            foreach (var item in items)
            {
                var node = Visit(item);
                if (node is AnnotationCapture capture)
                {
                    // A comment/doc annotation does not change the current flow position.
                    annotations.Add(capture.Annotation);
                    continue;
                }

                if (node is MultiNodeCapture multi)
                {
                    ApplyMembershipDetails(multi.Nodes, item);
                    children.AddRange(multi.Nodes);
                }
                else if (node is not null)
                {
                    ApplyMembershipDetails([node], item);
                    children.Add(node);
                }

                _actionBodyPreviousNodeName = DetermineFlowPositionName(item, node);
            }
        }
        finally
        {
            _actionBodyPreviousNodeName = savedPreviousName;
        }

        return (children, annotations);
    }

    /// <summary>
    ///     Determines the name that represents the "current flow position" after visiting one
    ///     <c>actionBodyItem</c>, for use as the implicit <c>Source</c> of a subsequent sibling's
    ///     leading bare <c>then</c>. When the item produced a <see cref="MultiNodeCapture"/> ending
    ///     in a synthesized <see cref="SysmlTransitionNode"/> (one or more trailing
    ///     <c>actionTargetSuccessionMember</c>s), the position moves to that succession's
    ///     <c>Target</c> (e.g. after <c>first start then off;</c>, the position is <c>"off"</c>,
    ///     not <c>"start"</c>). Otherwise it is the visited node's own <c>Name</c>. A bare
    ///     <c>first start;</c> (no attached succession) still establishes <c>"start"</c> as the
    ///     position even though it synthesizes no AST node of its own.
    /// </summary>
    private static string? DetermineFlowPositionName(SysMLv2Parser.ActionBodyItemContext item, SysmlNode? node)
    {
        if (node is MultiNodeCapture { Nodes.Count: > 0 } multi)
        {
            return multi.Nodes[^1] is SysmlTransitionNode lastTransition
                ? lastTransition.Target
                : multi.Nodes[^1].Name;
        }

        if (node is not null)
        {
            return node.Name;
        }

        return item.initialNodeMember()?.qualifiedName()?.GetText();
    }

    /// <summary>
    ///     Collects child nodes from an array of <see cref="SysMLv2Parser.PackageBodyElementContext"/>.
    ///     Comment/documentation annotations nested among the elements are separated out into
    ///     <see cref="SysmlAnnotation"/> entries rather than becoming children.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations) CollectBodyElements(
        IEnumerable<SysMLv2Parser.PackageBodyElementContext> elements)
    {
        var children = new List<SysmlNode>();
        var annotations = new List<SysmlAnnotation>();
        foreach (var element in elements)
        {
            var node = Visit(element);
            if (node is AnnotationCapture capture)
            {
                annotations.Add(capture.Annotation);
            }
            else if (node is not null)
            {
                ApplyMembershipDetails(
                    node,
                    element.packageMember()?.memberPrefix()
                        ?? element.kermlPackageMember()?.memberPrefix()
                        ?? element.aliasMember()?.memberPrefix(),
                    element);
                children.Add(node);
            }
        }

        return (children, annotations);
    }

    /// <summary>
    ///     Collects child nodes from an array of <see cref="SysMLv2Parser.DefinitionBodyItemContext"/>.
    ///     Comment/documentation annotations nested among the items are separated out into
    ///     <see cref="SysmlAnnotation"/> entries rather than becoming children.
    /// </summary>
    private (IReadOnlyList<SysmlNode> Children, IReadOnlyList<SysmlAnnotation> Annotations) CollectDefinitionBodyItems(
        IEnumerable<SysMLv2Parser.DefinitionBodyItemContext> items)
    {
        var children = new List<SysmlNode>();
        var annotations = new List<SysmlAnnotation>();
        foreach (var item in items)
        {
            var node = Visit(item);
            if (node is AnnotationCapture capture)
            {
                annotations.Add(capture.Annotation);
            }
            else if (node is not null)
            {
                ApplyMembershipDetails(node, item.memberPrefix(), item);
                children.Add(node);
            }
        }

        return (children, annotations);
    }
}
