// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser.Antlr;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

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
        var children = CollectBodyElements(context.packageBodyElement());
        return new SysmlPackageNode
        {
            Children = children,
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
        var children = CollectBodyElements(context.packageBody()?.packageBodyElement() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlPackageNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            Children = children,
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
        var children = CollectBodyElements(context.packageBody()?.packageBodyElement() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlPackageNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            Children = children,
        };
    }

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
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "action def");
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
        var children = CollectChildren(context.stateDefBody()?.stateBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = "state def",
            SupertypeNames = supertypeNames,
            Children = children,
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
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "case def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitAnalysisCaseDefinition(SysMLv2Parser.AnalysisCaseDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "analysis def");
    }

    /// <inheritdoc/>
    public override SysmlNode? VisitVerificationCaseDefinition(SysMLv2Parser.VerificationCaseDefinitionContext context)
    {
        return BuildDefinitionFromDeclaration(context.definitionDeclaration(), "verification def");
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
    public override SysmlNode? VisitStateUsage(SysMLv2Parser.StateUsageContext context)
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
            FeatureKeyword = "state",
        };
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
        var multiplicity = ExtractMultiplicity(decl?.featureSpecializationPart());

        // Named usages contribute a namespace segment for any nested usages they own.
        var qualifiedName = name is not null ? QualifyName(name) : null;
        IReadOnlyList<SysmlNode> children = Array.Empty<SysmlNode>();
        var body = usage.usageCompletion()?.usageBody()?.definitionBody();
        if (body is not null)
        {
            if (name is not null)
            {
                _namespaceStack.Add(name);
            }

            children = CollectDefinitionBodyItems(body.definitionBodyItem());

            if (name is not null)
            {
                _namespaceStack.RemoveAt(_namespaceStack.Count - 1);
            }
        }

        return new SysmlFeatureNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            FeatureKeyword = keyword,
            FeatureTyping = typing,
            Multiplicity = multiplicity,
            Children = children,
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

        return new SysmlViewNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            SupertypeNames = supertypeNames,
        };
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

        // Namespace import: qualifiedName::* — wildcard, all members of the namespace are in scope
        var nsImport = decl.namespaceImport();
        if (nsImport is not null)
        {
            var qn = nsImport.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                return new SysmlImportNode
                {
                    ImportedNamespace = qn,
                    IsWildcard = true,
                };
            }
        }

        // Membership import: qualifiedName (optional ::**)
        // The ** form is a recursive wildcard; either way it enables lookup under the namespace
        var memImport = decl.membershipImport();
        if (memImport is not null)
        {
            var qn = memImport.qualifiedName()?.GetText();
            if (qn is { Length: > 0 })
            {
                return new SysmlImportNode
                {
                    ImportedNamespace = qn,
                    IsWildcard = memImport.STAR_STAR() is not null,
                };
            }
        }

        return null;
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
        var children = CollectTypeBodyItems(body?.typeBodyElement() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = keyword,
            SupertypeNames = supertypeNames,
            Children = children,
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
    /// </summary>
    private IReadOnlyList<SysmlNode> CollectTypeBodyItems(
        IEnumerable<SysMLv2Parser.TypeBodyElementContext> items)
    {
        var result = new List<SysmlNode>();
        foreach (var item in items)
        {
            var node = Visit(item);
            if (node is not null)
            {
                result.Add(node);
            }
        }

        return result;
    }


    /// <summary>
    ///     Builds a definition AST node from a bare <see cref="SysMLv2Parser.DefinitionDeclarationContext"/>
    ///     for definition kinds whose grammar rule uses a specialized body (e.g. action, state,
    ///     requirement, enum) rather than the generic <c>definition</c> rule.
    /// </summary>
    /// <remarks>
    ///     Only the declared name and supertype names are captured. The specialized body contents
    ///     (nested usages and compartment members) are not yet collected; that is handled in a later
    ///     phase that adds usage and compartment rendering.
    /// </remarks>
    private SysmlDefinitionNode? BuildDefinitionFromDeclaration(
        SysMLv2Parser.DefinitionDeclarationContext? decl,
        string keyword)
    {
        var name = GetDeclaredName(decl?.identification());
        if (name is null)
        {
            return null;
        }

        var qualifiedName = QualifyName(name);
        var supertypeNames = GetSubclassificationSupertypes(decl?.subclassificationPart());

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = keyword,
            SupertypeNames = supertypeNames,
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
        var children = CollectDefinitionBodyItems(definition.definitionBody()?.definitionBodyItem() ?? []);
        _namespaceStack.RemoveAt(_namespaceStack.Count - 1);

        return new SysmlDefinitionNode
        {
            Name = name,
            QualifiedName = qualifiedName,
            DefinitionKeyword = keyword,
            SupertypeNames = supertypeNames,
            Children = children,
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
    ///     differs from the generic definition body item.
    /// </summary>
    private IReadOnlyList<SysmlNode> CollectChildren(IEnumerable<Antlr4.Runtime.Tree.IParseTree> items)
    {
        var result = new List<SysmlNode>();
        foreach (var item in items)
        {
            var node = Visit(item);
            if (node is not null)
            {
                result.Add(node);
            }
        }

        return result;
    }

    /// <summary>
    ///     Collects child nodes from an array of <see cref="SysMLv2Parser.PackageBodyElementContext"/>.
    /// </summary>
    private IReadOnlyList<SysmlNode> CollectBodyElements(
        IEnumerable<SysMLv2Parser.PackageBodyElementContext> elements)
    {
        var result = new List<SysmlNode>();
        foreach (var element in elements)
        {
            var node = Visit(element);
            if (node is not null)
            {
                result.Add(node);
            }
        }

        return result;
    }

    /// <summary>
    ///     Collects child nodes from an array of <see cref="SysMLv2Parser.DefinitionBodyItemContext"/>.
    /// </summary>
    private IReadOnlyList<SysmlNode> CollectDefinitionBodyItems(
        IEnumerable<SysMLv2Parser.DefinitionBodyItemContext> items)
    {
        var result = new List<SysmlNode>();
        foreach (var item in items)
        {
            var node = Visit(item);
            if (node is not null)
            {
                result.Add(node);
            }
        }

        return result;
    }
}
