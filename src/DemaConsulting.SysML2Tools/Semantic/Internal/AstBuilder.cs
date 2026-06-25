// Copyright (c) DemaConsulting. All rights reserved.
// Licensed under the MIT License.

using DemaConsulting.SysML2Tools.Parser.Antlr;

namespace DemaConsulting.SysML2Tools.Semantic.Internal;

/// <summary>
///     Builds a SysML/KerML AST from an ANTLR4 CST produced by <see cref="SysMLv2Parser"/>.
/// </summary>
internal sealed class AstBuilder : SysMLv2ParserBaseVisitor<SysmlNode?>
{
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
