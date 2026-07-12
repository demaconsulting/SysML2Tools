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
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "enum def");
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

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = QualifyName(name),
            FeatureKeyword = "action",
        };
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
    ///     grammar for this alternative — a known, documented simplification). The trailing
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
            };
        }

        if (succession?.defaultTargetSuccession() is { } defaultTargetSuccession)
        {
            return new SysmlTransitionNode
            {
                Source = sourceName,
                Target = ConnectorEndReference(
                    defaultTargetSuccession.transitionSuccessionMember()?.transitionSuccession()?.connectorEndMember()),
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
        var declaration = context.acceptNodeDeclaration()?.actionNodeUsageDeclaration();
        return BuildActionNodeFeature(declaration?.usageDeclaration(), "accept");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSendNode(SysMLv2Parser.SendNodeContext context)
    {
        var declaration = context.actionNodeUsageDeclaration()?.usageDeclaration()
            ?? context.actionUsageDeclaration()?.usageDeclaration();
        return BuildActionNodeFeature(declaration, "send");
    }

    /// <summary>
    ///     Builds a minimal, deliberately non-behavioral <see cref="SysmlFeatureNode"/> for a
    ///     <c>merge</c>/<c>decide</c>/<c>join</c>/<c>fork</c>/<c>accept</c>/<c>send</c> action-flow
    ///     control node, registering only its (possibly synthesized) name with no children. When
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
    ///     <see cref="BuildStateActionFeatureNode"/>. <c>assignmentNode</c>/<c>terminateNode</c>/
    ///     <c>ifNode</c>/<c>whileLoopNode</c>/<c>forLoopNode</c> remain an intentional, out-of-scope
    ///     gap — not handled here or anywhere else in <see cref="AstBuilder"/>.
    /// </summary>
    private SysmlFeatureNode BuildActionNodeFeature(SysMLv2Parser.UsageDeclarationContext? decl, string keyword)
    {
        var declaredName = GetDeclaredName(decl?.identification());
        var name = declaredName ?? $"${keyword}{_anonymousNodeCounter++}";

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = declaredName is not null ? QualifyName(name) : null,
            FeatureKeyword = keyword,
            Children = Array.Empty<SysmlNode>(),
            Annotations = Array.Empty<SysmlAnnotation>(),
        };
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
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "constraint def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitRequirementDefinition(SysMLv2Parser.RequirementDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "requirement def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitConcernDefinition(SysMLv2Parser.ConcernDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "concern def");
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
    public override SysmlNode? VisitConnectionUsage(SysMLv2Parser.ConnectionUsageContext context)
    {
        var name = GetDeclaredName(context.usageDeclaration()?.identification());
        var (endpointA, endpointB) = ExtractConnectorEnds(context.connectorPart());

        return new SysmlConnectionNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            ConnectionKeyword = "connection",
            EndpointA = endpointA,
            EndpointB = endpointB,
        };
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

        return new SysmlConnectionNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            ConnectionKeyword = "message",
            EndpointA = from,
            EndpointB = to,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAllocationUsage(SysMLv2Parser.AllocationUsageContext context)
    {
        // allocationUsageDeclaration.connectorPart() returns the exact same ConnectorPartContext
        // type as connectionUsage.connectorPart(), so the existing ExtractConnectorEnds helper is
        // reusable as-is for allocate's two ends.
        var decl = context.allocationUsageDeclaration();
        var name = GetDeclaredName(decl?.usageDeclaration()?.identification());
        var (endpointA, endpointB) = ExtractConnectorEnds(decl?.connectorPart());

        return new SysmlConnectionNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            ConnectionKeyword = "allocation",
            EndpointA = endpointA,
            EndpointB = endpointB,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitBindingConnectorAsUsage(SysMLv2Parser.BindingConnectorAsUsageContext context)
    {
        // Only the common "bind A = B;" (bindingConnectorAsUsage) shape is supported; the longer
        // bindingConnector/typeBody form has zero corpus evidence and is a documented limitation.
        var name = GetDeclaredName(context.usageDeclaration()?.identification());
        var ends = context.connectorEndMember();
        var endpointA = ends.Length > 0 ? ConnectorEndReference(ends[0]) : null;
        var endpointB = ends.Length > 1 ? ConnectorEndReference(ends[1]) : null;

        return new SysmlConnectionNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            ConnectionKeyword = "binding",
            EndpointA = endpointA,
            EndpointB = endpointB,
        };
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitSatisfyRequirementUsage(SysMLv2Parser.SatisfyRequirementUsageContext context)
    {
        // Prefer the ownedReferenceSubsetting form (satisfy <ref> ...); fall back to the
        // typed/declared name of the "REQUIREMENT usageDeclaration?" form.
        var requirementName = context.ownedReferenceSubsetting()?.GetText()
            ?? GetDeclaredName(context.usageDeclaration()?.identification())
            ?? context.usageDeclaration()?.GetText();

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
        // Minimal capture: name/qualified-name only, so that named requirement usages (the common
        // real-world satisfy/verify target pattern) become resolvable symbols. Subject/constraint/
        // actor compartment members remain unvisited, consistent with existing scope discipline
        // for specialized bodies (see BuildDefinitionFromDeclaration).
        var name = GetDeclaredName(context.constraintUsageDeclaration()?.usageDeclaration()?.identification());

        var verifiedRequirementNames = context.requirementBody() is { } body
            ? FindVerificationMembers(body)
            : Array.Empty<string>();

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            FeatureKeyword = "requirement",
            VerifiedRequirementNames = verifiedRequirementNames,
        };
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
        var typing = ExtractFeatureTyping(decl?.featureSpecializationPart());

        // Collect the state body (nested state usages and transitions) as children, mirroring
        // VisitStateDefinition. Anonymous ("state x;") usages have no body items to collect.
        _namespaceStack.Add(name);
        var (children, annotations) = CollectChildren(context.stateUsageBody()?.stateBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = QualifyName(name),
            FeatureKeyword = "state",
            FeatureTyping = typing,
            Children = children,
            Annotations = annotations,
        };
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

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = name is not null ? QualifyName(name) : null,
            FeatureKeyword = keyword,
            Children = Array.Empty<SysmlNode>(),
            Annotations = Array.Empty<SysmlAnnotation>(),
        };
    }

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
        };
    }

    /// <summary>
    ///     Extracts the two endpoint references of a binary connector (the features either side of
    ///     <c>connect … to …</c>), or nulls when the connector is not a simple binary connection.
    /// </summary>
    private static (string? A, string? B) ExtractConnectorEnds(SysMLv2Parser.ConnectorPartContext? connectorPart)
    {
        var binary = connectorPart?.binaryConnectorPart();
        if (binary is null)
        {
            return (null, null);
        }

        var ends = binary.connectorEndMember();
        if (ends.Length < 2)
        {
            return (null, null);
        }

        return (ConnectorEndReference(ends[0]), ConnectorEndReference(ends[1]));
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

        return new SysmlFeatureNode
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
        if (owned is not null)
        {
            return owned.GetText();
        }

        return ft.qualifiedName()?.GetText();
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
    ///     already is, which <see cref="ReferenceResolver"/>'s bare-name redefinition
    ///     ancestor-chain walk depends on to reach an inherited member through a usage's own
    ///     subsetting ancestor.
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

        var (renderTargetName, filterExpressionText) =
            ExtractViewRenderAndFilter(context.viewDefinitionBody()?.viewDefinitionBodyItem() ?? []);

        return new SysmlViewNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            SupertypeNames = supertypeNames,
            RenderTargetName = renderTargetName,
            FilterExpressionText = filterExpressionText,
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

        var (renderTargetName, filterExpressionText) = ExtractViewRenderAndFilter(bodyItems);
        var exposeMembers = ExtractExposedNames(bodyItems);

        return new SysmlViewNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            RenderTargetName = renderTargetName,
            ExposeMembers = exposeMembers,
            FilterExpressionText = filterExpressionText,
        };
    }

    /// <summary>
    ///     Scans a view's body items for a <c>render &lt;target&gt;;</c> member and a
    ///     <c>filter [&lt;expression&gt;];</c> member, returning the raw reference text and raw
    ///     expression source text respectively (or <see langword="null"/> for either when absent).
    ///     Shared by <see cref="VisitViewDefinition"/> (<c>viewDefinitionBodyItem</c>) and
    ///     <see cref="VisitViewUsage"/> (<c>viewBodyItem</c>) since both context types expose
    ///     identically-shaped <c>viewRenderingMember()</c>/<c>elementFilterMember()</c> accessors.
    ///     The first <c>render</c> member wins if more than one appears — SysML disallows more
    ///     than one render subject per view, so this is a defensive tie-break, not a validated
    ///     constraint enforced by this tool.
    /// </summary>
    private static (string? RenderTargetName, string? FilterExpressionText) ExtractViewRenderAndFilter<TItem>(
        IEnumerable<TItem> bodyItems)
        where TItem : Antlr4.Runtime.ParserRuleContext
    {
        string? renderTargetName = null;
        string? filterExpressionText = null;

        foreach (var item in bodyItems)
        {
            if (renderTargetName is null &&
                GetViewRenderingMember(item) is { } renderingMember)
            {
                renderTargetName = ExtractRenderTargetName(renderingMember.viewRenderingUsage());
            }

            if (filterExpressionText is null &&
                GetElementFilterMember(item) is { } filterMember)
            {
                filterExpressionText = filterMember.ownedExpression() is { } filterExpr
                    ? GetOriginalText(filterExpr)
                    : null;
            }
        }

        return (renderTargetName, filterExpressionText);
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

            var (qn, _, bracketFilterText) = ExtractImportTarget(
                expose.namespaceExpose()?.namespaceImport(),
                expose.membershipExpose()?.membershipImport());
            if (qn is { Length: > 0 })
            {
                members.Add(new ExposeMember(qn, bracketFilterText));
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

        var (qn, isWildcard, bracketFilterText) = ExtractImportTarget(decl.namespaceImport(), decl.membershipImport());
        if (qn is null)
        {
            return null;
        }

        return new SysmlImportNode
        {
            ImportedNamespace = qn,
            ImportedNames = [qn],
            IsWildcard = isWildcard,
            BracketFilterExpressionText = bracketFilterText,
        };
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
    ///     text) and whether the import/expose is a wildcard.
    /// </returns>
    private static (string? QualifiedName, bool IsWildcard, string? BracketFilterExpressionText) ExtractImportTarget(
        SysMLv2Parser.NamespaceImportContext? namespaceImport,
        SysMLv2Parser.MembershipImportContext? membershipImport)
    {
        // Namespace import: qualifiedName::* — wildcard, all members of the namespace are in scope
        if (namespaceImport is not null)
        {
            var qn = namespaceImport.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                return (qn, true, null);
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
                        return (filterQn, filterMembershipImport.STAR_STAR() is not null, bracketFilterText);
                    }
                }

                var namespaceImportDirect = filterDecl.namespaceImportDirect();
                if (namespaceImportDirect is not null)
                {
                    var directQn = namespaceImportDirect.qualifiedName()?.GetText();
                    if (directQn is { Length: > 0 })
                    {
                        return (directQn, true, bracketFilterText);
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
                return (qn, membershipImport.STAR_STAR() is not null, null);
            }
        }

        return (null, false, null);
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
        return BuildClassifierNode(context.classifierDeclaration(), body: null, "function");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitPredicate(SysMLv2Parser.PredicateContext context)
    {
        return BuildClassifierNode(context.classifierDeclaration(), body: null, "predicate");
    }

    /// <summary>
    ///     Builds a definition AST node from a KerML classifier declaration (datatype, class, struct, assoc).
    /// </summary>
    private SysmlDefinitionNode? BuildClassifierNode(
        SysMLv2Parser.ClassifierDeclarationContext? decl,
        SysMLv2Parser.TypeBodyContext? body,
        string keyword)
    {
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSuperclassingSupertypes(decl?.superclassingPart());

        _namespaceStack.Add(name);
        var (children, annotations) = CollectTypeBodyItems(body?.typeBodyElement() ?? []);
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
                children.AddRange(multi.Nodes);
            }
            else if (node is not null)
            {
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
                    children.AddRange(multi.Nodes);
                }
                else if (node is not null)
                {
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
                children.Add(node);
            }
        }

        return (children, annotations);
    }
}
