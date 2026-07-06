// <copyright file="GeneralViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for GeneralView diagrams. Renders every user-defined <c>def</c> element
/// (part, port, interface, requirement, action, …) as a keyword-labelled box, groups boxes by
/// their owning package inside folder-shaped containers, and routes specialization, membership, and
/// attribute-typing edges orthogonally between the boxes.
/// </summary>
/// <remarks>
/// Every definition and package folder is expressed as a single <see cref="LayoutGraph"/> — packages
/// as <see cref="BoxShape.Folder"/>-shaped container nodes, definitions as leaf nodes carrying their
/// <see cref="LayoutGraphNode.Keyword"/> and <see cref="LayoutGraphNode.Compartments"/> — and the
/// whole graph is placed with a single <see cref="HierarchicalLayoutAlgorithm.Apply"/> call: the root
/// scope packs package folders and top-level definitions by reading order
/// (<see cref="ContainmentLayoutAlgorithm"/>), while each folder's own contents are ordered by their
/// intra-package edges with the bundled layered algorithm (<see cref="LayeredLayoutAlgorithm"/>). All
/// box sizing (title bands, compartment rows) remains this strategy's responsibility, since the
/// layout stage is theme-agnostic. Standard-library declarations are excluded via
/// <see cref="StdlibFilter"/>. Depth-limited (truncated) package contents are never added to the
/// graph as individual boxes — the truncated folder becomes a single leaf node sized like a plain
/// ellipsis indicator, and the "+N more…" label is stamped onto its placed box once the layout is
/// known.
/// </remarks>
internal sealed class GeneralViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a definition box in logical pixels.</summary>
    private const double MinBoxWidth = 130.0;

    /// <summary>Approximate width-per-character factor relative to font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>A feature membership: the keyword and the raw typing reference of one owned feature.</summary>
    private sealed record FeatureMembership(string Keyword, string TypeName);

    /// <summary>
    /// The classification of an edge, which selects its line style so the renderer can distinguish
    /// the three relationships the General view draws between definition boxes.
    /// </summary>
    private enum EdgeKind
    {
        /// <summary>Subtype → supertype specialization: solid line, hollow triangle at the supertype.</summary>
        Specialization,

        /// <summary>Member-type → owner structural/reference membership: solid line, diamond at the owner.</summary>
        Membership,

        /// <summary>
        /// Owner → attribute-type dependency (attribute typing): dashed line, open chevron at the type.
        /// Attribute typing is a usage-type dependency, not composition, so it uses the OMG dependency
        /// notation (dashed + open arrowhead) rather than a membership diamond.
        /// </summary>
        Typing,
    }

    /// <summary>
    /// A model edge between two definitions, expressed by qualified name so it can be resolved into
    /// the correct graph scope once every definition's node (or absence, if depth-truncated) is known.
    /// </summary>
    /// <param name="SourceQualified">Qualified name of the source definition.</param>
    /// <param name="TargetQualified">Qualified name of the target (supertype, owner, or attribute-type) definition.</param>
    /// <param name="Arrowhead">Arrowhead drawn at the target end.</param>
    /// <param name="Kind">The edge classification, which selects the rendered line style.</param>
    private sealed record ModelEdge(string SourceQualified, string TargetQualified, EndMarkerStyle Arrowhead, EdgeKind Kind);

    /// <summary>Maps an edge kind to its rendered line style: typing edges are dashed, all others solid.</summary>
    private static LineStyle LineStyleForKind(EdgeKind kind) =>
        kind == EdgeKind.Typing ? LineStyle.Dashed : LineStyle.Solid;

    /// <summary>A user-defined definition together with its computed box size and supertypes.</summary>
    private sealed record DefBox(
        string QualifiedName,
        string SimpleName,
        string Keyword,
        IReadOnlyList<string> SupertypeNames,
        IReadOnlyList<FeatureMembership> Memberships,
        IReadOnlyList<LayoutCompartment> Compartments,
        double Width,
        double Height);

    /// <summary>Where a located definition's node lives: the node itself and its owning package.</summary>
    private readonly record struct Location(LayoutGraphNode Node, string Package);

    /// <summary>
    /// A package folder that was depth-truncated: replaced by a leaf node sized as an ellipsis
    /// indicator, decorated with its "+N more…" label once the layout places it.
    /// </summary>
    /// <param name="Node">The leaf graph node standing in for the folder.</param>
    /// <param name="HiddenCount">Number of hidden definitions the ellipsis label reports.</param>
    private sealed record TruncatedFolder(LayoutGraphNode Node, int HiddenCount);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        // Resolve the view's render-target subject scope (render target plus exposed names'
        // containment subtrees), or null when the view has no resolved Render edge — including a
        // null ViewNode (the --auto synthetic-view path) and an unresolved render target, both of
        // which leave no Render edge on ResolvedEdges. A null scope means "render everything",
        // byte-identical to the pre-scoping behavior.
        var scope = ResolveSubjectScope(context.ViewNode);

        // Collect all user-defined definitions, sized for rendering, restricted to the resolved
        // scope when one applies.
        var defs = CollectDefinitions(context.Workspace, theme, scope);
        if (defs.Count == 0)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Group definitions by their owning package (prefix before the last "::").
        var groups = GroupByPackage(defs);

        // Resolve the specialization/membership/attribute-typing edge set by qualified name; the
        // graph-construction pass below drops any edge touching a definition that never received a
        // node (because its folder was depth-truncated).
        var modelEdges = BuildModelEdges(defs);

        // Build the single input graph: package folders as containers, definitions as leaves.
        var (graph, truncated) = BuildGraph(groups, modelEdges, theme, options.DepthLimit);

        // Lay out the whole graph in one call: the root scope packs folders/top-level definitions by
        // reading order (containment), while each folder's own contents are ordered by their
        // intra-package edges with the layered algorithm — selected per folder node, per the
        // established per-container-algorithm convention.
        var rootOptions = LayoutOptions.ForAlgorithm(ContainmentLayoutAlgorithm.AlgorithmId);
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, rootOptions);

        // Stamp the "+N more…" ellipsis label onto each truncated folder's placed box. The leaf
        // algorithm emits one box per root node in Nodes order, so the boxes portion of the placed
        // tree aligns with graph.Nodes by index.
        var placed = truncated.Count == 0 ? tree : DecorateTruncatedFolders(tree, graph, truncated, theme);

        // A `filter [<expr>];` statement is parsed (SysmlViewNode.FilterExpressionText) but not
        // yet evaluated — full expression evaluation is deferred future work (see ROADMAP.md).
        // Surface this to the caller through the standard layout-warnings channel rather than
        // silently rendering a diagram the user may believe is filtered.
        var warnings = LayoutWarnings.ForUnevaluatedFilter(context.ViewName, context.ViewNode?.FilterExpressionText);
        return warnings.Count == 0 ? placed : placed with { Warnings = warnings };
    }

    /// <summary>
    /// Resolves the qualified-name containment-subtree scope a view's <c>render</c>/<c>expose</c>
    /// statements restrict the diagram to, or <see langword="null"/> when the view has no resolved
    /// <see cref="SysmlEdgeKind.Render"/> edge — meaning every non-stdlib definition is included,
    /// unchanged from the pre-scoping behavior. This covers three equivalent "no scoping" cases
    /// uniformly: a null <paramref name="viewNode"/> (the <c>--auto</c> synthetic view, which never
    /// carries render/expose/filter data), a view with no <c>render</c> statement, and a view whose
    /// <c>render</c> target failed to resolve (per binding decision, an unresolved target falls
    /// back to full-workspace rendering; the diagnostic that informs the user is emitted by
    /// <c>ReferenceResolver</c>, not here).
    /// </summary>
    /// <param name="viewNode">The view's AST node, or null for the synthetic <c>--auto</c> view.</param>
    /// <returns>
    /// The list of subject qualified names (the render target plus any exposed names) whose
    /// containment subtrees are in scope, or null when no scoping applies.
    /// </returns>
    private static IReadOnlyList<string>? ResolveSubjectScope(SysmlViewNode? viewNode)
    {
        var renderTarget = viewNode?.ResolvedEdges
            .FirstOrDefault(edge => edge.Kind == SysmlEdgeKind.Render)
            ?.TargetQualifiedName;
        if (renderTarget is null)
        {
            return null;
        }

        var subjects = new List<string> { renderTarget };
        subjects.AddRange(viewNode!.ResolvedEdges
            .Where(edge => edge.Kind == SysmlEdgeKind.Expose)
            .Select(edge => edge.TargetQualifiedName));
        return subjects;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="qualifiedName"/> is one of
    /// <paramref name="subjects"/> or lies within one of their containment subtrees (a
    /// <c>"{subject}::"</c> prefix match), reusing the same qualified-name-prefix idiom
    /// <see cref="StdlibFilter.IsStdlibElement(string, IReadOnlySet{string})"/> already uses for
    /// stdlib-prefix matching.
    /// </summary>
    private static bool IsInSubjectScope(string qualifiedName, IReadOnlyList<string> subjects) =>
        subjects.Any(subject =>
            qualifiedName == subject || qualifiedName.StartsWith(subject + "::", StringComparison.Ordinal));

    /// <summary>
    /// Collects every user-defined <see cref="SysmlDefinitionNode"/> from the workspace and computes
    /// each box's intrinsic size from its keyword and name, restricted to <paramref name="scope"/>
    /// when non-null (the view's resolved <c>render</c>/<c>expose</c> containment subtrees).
    /// </summary>
    private static IReadOnlyList<DefBox> CollectDefinitions(
        SysmlWorkspace workspace,
        Theme theme,
        IReadOnlyList<string>? scope)
    {
        var result = new List<DefBox>();

        foreach (var (qualifiedName, declaration) in workspace.Declarations)
        {
            if (declaration is not SysmlDefinitionNode def)
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            if (scope is not null && !IsInSubjectScope(qualifiedName, scope))
            {
                continue;
            }

            var simpleName = def.Name ?? qualifiedName;
            var keyword = string.IsNullOrEmpty(def.DefinitionKeyword) ? "def" : def.DefinitionKeyword;

            // Build compartments from the definition's owned usages (attributes, ports, parts, …).
            var compartments = BuildCompartments(def);

            var memberships = CollectMemberships(def);
            var (width, height) = ComputeBoxSize(simpleName, keyword, compartments, theme);
            result.Add(new DefBox(qualifiedName, simpleName, keyword, def.SupertypeNames, memberships, compartments, width, height));
        }

        return result;
    }

    /// <summary>
    /// Builds compartments for a definition by grouping its owned usage features by keyword and
    /// formatting each as a <c>name : Type [n]</c> row.
    /// </summary>
    private static IReadOnlyList<LayoutCompartment> BuildCompartments(SysmlDefinitionNode def)
    {
        // Preserve keyword first-seen order so compartments appear in declaration order.
        var order = new List<string>();
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var child in def.Children)
        {
            if (child is not SysmlFeatureNode feature)
            {
                continue;
            }

            var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
            if (!groups.TryGetValue(keyword, out var rows))
            {
                rows = [];
                groups[keyword] = rows;
                order.Add(keyword);
            }

            rows.Add(FormatFeatureRow(feature));
        }

        return [.. order.Select(k => new LayoutCompartment(Pluralize(k), groups[k]))];
    }

    /// <summary>Formats a usage feature as a compartment row: <c>name : Type [n]</c>.</summary>
    private static string FormatFeatureRow(SysmlFeatureNode feature)
    {
        var name = feature.Name ?? string.Empty;
        var typing = feature.FeatureTyping is { Length: > 0 } t ? $" : {t}" : string.Empty;
        var multiplicity = feature.Multiplicity is { Length: > 0 } m ? $" {m}" : string.Empty;
        var row = $"{name}{typing}{multiplicity}".Trim();
        return row.Length == 0 ? "\u2014" : row;
    }

    /// <summary>Returns a simple plural form of a usage keyword for use as a compartment title.</summary>
    private static string Pluralize(string keyword) => keyword switch
    {
        "ref" => "references",
        _ => keyword + "s",
    };

    /// <summary>
    /// Collects the feature memberships of a definition: the keyword and type reference of each
    /// owned feature that carries a type annotation.
    /// </summary>
    private static IReadOnlyList<FeatureMembership> CollectMemberships(SysmlDefinitionNode def)
    {
        var result = new List<FeatureMembership>();
        foreach (var child in def.Children)
        {
            if (child is not SysmlFeatureNode feature)
            {
                continue;
            }

            if (feature.FeatureTyping is { Length: > 0 } typing)
            {
                var keyword = string.IsNullOrEmpty(feature.FeatureKeyword) ? "feature" : feature.FeatureKeyword;
                result.Add(new FeatureMembership(keyword, typing));
            }
        }

        return result;
    }

    /// <summary>Computes the intrinsic box size needed for the title and any compartments.</summary>
    private static (double Width, double Height) ComputeBoxSize(
        string name,
        string keyword,
        IReadOnlyList<LayoutCompartment> compartments,
        Theme theme)
    {
        var nameWidth = (name.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var keywordWidth = ((keyword.Length + 2) * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinBoxWidth, Math.Max(nameWidth, keywordWidth));

        // Widen to fit the longest compartment title or row.
        foreach (var compartment in compartments)
        {
            if (compartment.Title is { } title)
            {
                width = Math.Max(width, (title.Length * theme.FontSizeBody * CharWidthFactor) + (2.0 * theme.LabelPadding));
            }

            foreach (var row in compartment.Rows)
            {
                width = Math.Max(width, (row.Length * theme.FontSizeBody * CharWidthFactor) + (3.0 * theme.LabelPadding));
            }
        }

        // Title area holds the keyword line and the name line; add a little body breathing room.
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        foreach (var compartment in compartments)
        {
            height += ComputeCompartmentHeight(compartment, theme);
        }

        return (width, height);
    }

    /// <summary>
    /// Computes the rendered height of a compartment, matching the renderer's layout: an optional
    /// title row followed by one row per entry.
    /// </summary>
    private static double ComputeCompartmentHeight(LayoutCompartment compartment, Theme theme)
    {
        var height = 0.0;
        if (compartment.Title is not null)
        {
            height += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
        }

        height += compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody);

        // Bottom gap added by the renderer after the last row.
        height += theme.LabelPadding;
        return height;
    }

    /// <summary>
    /// Groups definitions by their parent package name (the qualified-name prefix before the last
    /// <c>::</c>), preserving first-seen order. Top-level definitions use an empty package key.
    /// </summary>
    private static IReadOnlyList<(string Package, List<DefBox> Items)> GroupByPackage(IReadOnlyList<DefBox> defs)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<DefBox>>(StringComparer.Ordinal);

        foreach (var def in defs)
        {
            var sep = def.QualifiedName.LastIndexOf("::", StringComparison.Ordinal);
            var package = sep >= 0 ? def.QualifiedName[..sep] : string.Empty;

            if (!map.TryGetValue(package, out var list))
            {
                list = [];
                map[package] = list;
                order.Add(package);
            }

            list.Add(def);
        }

        return [.. order.Select(p => (p, map[p]))];
    }

    /// <summary>
    /// Resolves the specialization, membership, and attribute-typing relationships across every
    /// definition (regardless of package) into a flat list of qualified-name edges. Self-references
    /// and unresolved targets are skipped; whether an edge's endpoints actually receive a graph node
    /// (i.e., were not depth-truncated) is decided later, in <see cref="BuildGraph"/>.
    /// </summary>
    private static List<ModelEdge> BuildModelEdges(IReadOnlyList<DefBox> defs)
    {
        // Index every definition by qualified and simple name (first-seen wins for simple names,
        // mirroring how packages may reuse a simple name across different qualified locations).
        var byQualified = new HashSet<string>(defs.Select(d => d.QualifiedName), StringComparer.Ordinal);
        var bySimple = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            bySimple.TryAdd(def.SimpleName, def.QualifiedName);
        }

        var edges = new List<ModelEdge>();
        foreach (var def in defs)
        {
            // Specialization: subtype → supertype, open arrowhead at the supertype (target) end.
            foreach (var supertype in def.SupertypeNames)
            {
                if (TryResolveQualified(supertype, byQualified, bySimple, out var target) &&
                    target != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, target, EndMarkerStyle.HollowTriangle, EdgeKind.Specialization));
                }
            }

            // Membership: member-type → owner, diamond at the owner (target) end. Structural keywords
            // (part/port) use a filled diamond; reference (ref) uses a hollow diamond; others emit none.
            foreach (var membership in def.Memberships)
            {
                var arrowhead = membership.Keyword switch
                {
                    "part" or "port" => EndMarkerStyle.FilledDiamond,
                    "ref" => EndMarkerStyle.HollowDiamond,
                    _ => EndMarkerStyle.None,
                };

                if (arrowhead != EndMarkerStyle.None &&
                    TryResolveQualified(membership.TypeName, byQualified, bySimple, out var memberType) &&
                    memberType != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(memberType, def.QualifiedName, arrowhead, EdgeKind.Membership));
                }
            }

            // Attribute typing: owner → attribute-type dependency, open chevron at the type (target)
            // end. This is a usage-type dependency, not composition, so it uses the OMG dependency
            // notation rather than a membership diamond. Unresolved types and self-references are
            // skipped, exactly as above.
            foreach (var membership in def.Memberships)
            {
                if (membership.Keyword is not ("attribute" or "enum"))
                {
                    continue;
                }

                if (TryResolveQualified(membership.TypeName, byQualified, bySimple, out var attrType) &&
                    attrType != def.QualifiedName)
                {
                    edges.Add(new ModelEdge(def.QualifiedName, attrType, EndMarkerStyle.OpenChevron, EdgeKind.Typing));
                }
            }
        }

        return edges;
    }

    /// <summary>Resolves a supertype/type reference to a definition's qualified name, by qualified then simple name.</summary>
    private static bool TryResolveQualified(
        string reference,
        HashSet<string> byQualified,
        Dictionary<string, string> bySimple,
        out string qualified)
    {
        if (byQualified.Contains(reference))
        {
            qualified = reference;
            return true;
        }

        var sep = reference.LastIndexOf("::", StringComparison.Ordinal);
        var simple = sep >= 0 ? reference[(sep + 2)..] : reference;
        return bySimple.TryGetValue(simple, out qualified!);
    }

    /// <summary>
    /// Builds the single input <see cref="LayoutGraph"/>: each package becomes a folder container
    /// node holding its definitions as leaves (or, when depth-truncated, a single leaf ellipsis
    /// placeholder); top-level (unpackaged) definitions become leaves directly on the root graph.
    /// Model edges are added once every definition's node placement is known: an edge whose endpoints
    /// share a non-empty package is added to that folder's own scope (an intra-package edge the
    /// layered algorithm can use to order the folder's contents); every other edge — including any
    /// crossing packages — is added at the root, referencing the descendant nodes directly, per the
    /// lowest-common-ancestor edge convention. An edge touching a depth-truncated (unrendered)
    /// definition has no node to reference and is dropped, exactly as before.
    /// </summary>
    private static (LayoutGraph Graph, List<TruncatedFolder> Truncated) BuildGraph(
        IReadOnlyList<(string Package, List<DefBox> Items)> groups,
        IReadOnlyList<ModelEdge> modelEdges,
        Theme theme,
        int depthLimit)
    {
        var graph = new LayoutGraph();
        var truncated = new List<TruncatedFolder>();

        // Reserve the full title area (package keyword + name) above a folder's contents so the
        // label never overlaps the first child box; the renderer draws the smaller tab notch within.
        var folderTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var margin = 2.0 * theme.LabelPadding;

        // Folder contents sit at depth 1; truncate them when the depth limit forbids that level.
        var truncateFolderContents = depthLimit > 0 && depthLimit <= 1;

        // Every located definition's node and owning package, so edges can be resolved and scoped.
        var located = new Dictionary<string, Location>(StringComparer.Ordinal);

        // Each non-truncated package folder's own child scope, keyed by package name, so intra-package
        // edges can be added there instead of at the root.
        var folderScopes = new Dictionary<string, LayoutGraph>(StringComparer.Ordinal);

        for (var g = 0; g < groups.Count; g++)
        {
            var (package, items) = groups[g];
            var isFolder = !string.IsNullOrEmpty(package);

            if (!isFolder)
            {
                // Top-level (unpackaged) definitions become plain leaves directly on the root graph.
                foreach (var def in items)
                {
                    var leaf = MakeDefNode(graph, def);
                    located[def.QualifiedName] = new Location(leaf, package);
                }

                continue;
            }

            if (truncateFolderContents)
            {
                // Replace the folder's definition boxes with a single leaf ellipsis indicator. It
                // stays a leaf (its Children graph is never touched) so the hierarchical engine keeps
                // this caller-computed size rather than auto-sizing it as a container.
                var ellipsisWidth = Math.Max(
                    MinBoxWidth,
                    (2.0 * margin) + (items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length * 8.0) + 60.0);
                var ellipsisHeight = (2.0 * margin) + theme.FontSizeTitle;
                var placeholder = graph.AddNode($"folder:{package}", ellipsisWidth, folderTitleHeight + ellipsisHeight);
                placeholder.Label = SimplePackageName(package);
                placeholder.Shape = BoxShape.Folder;
                placeholder.Keyword = "package";
                truncated.Add(new TruncatedFolder(placeholder, items.Count));

                // None of the folder's definitions receive a node: every edge touching one is dropped.
                continue;
            }

            var folder = graph.AddNode($"folder:{package}", 0, 0);
            folder.Label = SimplePackageName(package);
            folder.Shape = BoxShape.Folder;
            folder.Keyword = "package";
            folder.TitleHeight = folderTitleHeight;
            folder.Set(CoreOptions.Algorithm, LayeredLayoutAlgorithm.AlgorithmId);
            folderScopes[package] = folder.Children;

            foreach (var def in items)
            {
                var leaf = MakeDefNode(folder.Children, def);
                located[def.QualifiedName] = new Location(leaf, package);
            }
        }

        // Add every model edge whose endpoints both received a node, scoped per the
        // lowest-common-ancestor rule: same non-empty package → that folder's own scope; otherwise the
        // root graph, referencing the (possibly nested) endpoint nodes directly.
        var edgeId = 0;
        foreach (var edge in modelEdges)
        {
            if (!located.TryGetValue(edge.SourceQualified, out var source) ||
                !located.TryGetValue(edge.TargetQualified, out var target))
            {
                continue;
            }

            var scope = source.Package.Length > 0 &&
                source.Package == target.Package &&
                folderScopes.TryGetValue(source.Package, out var folderScope)
                ? folderScope
                : graph;

            var added = scope.AddEdge($"e{edgeId++}", source.Node, target.Node);
            added.TargetEnd = edge.Arrowhead;
            added.LineStyle = LineStyleForKind(edge.Kind);
        }

        return (graph, truncated);
    }

    /// <summary>Creates a definition leaf node in the given scope, carrying its keyword and compartments.</summary>
    private static LayoutGraphNode MakeDefNode(LayoutGraph scope, DefBox def)
    {
        var node = scope.AddNode(def.QualifiedName, def.Width, def.Height);
        node.Label = def.SimpleName;
        node.Shape = BoxShape.Rectangle;
        node.Keyword = def.Keyword;
        node.Compartments = def.Compartments;
        return node;
    }

    /// <summary>
    /// Replaces each truncated folder's placed box with one carrying its "+N more…" ellipsis label,
    /// positioned within the box's now-known absolute placement.
    /// </summary>
    private static LayoutTree DecorateTruncatedFolders(
        LayoutTree tree,
        LayoutGraph graph,
        IReadOnlyList<TruncatedFolder> truncated,
        Theme theme)
    {
        var hiddenByNode = truncated.ToDictionary(t => t.Node, t => t.HiddenCount);
        var folderTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        var nodes = new List<LayoutNode>(tree.Nodes);
        for (var i = 0; i < graph.Nodes.Count && i < nodes.Count; i++)
        {
            if (!hiddenByNode.TryGetValue(graph.Nodes[i], out var hiddenCount) ||
                nodes[i] is not LayoutBox box)
            {
                continue;
            }

            var indicator = new LayoutLabel(
                X: box.X + theme.LabelPadding,
                Y: box.Y + folderTitleHeight + theme.LabelPadding + (theme.FontSizeTitle / 2.0),
                MaxWidth: box.Width - (2.0 * theme.LabelPadding),
                Text: $"+{hiddenCount} more\u2026",
                Align: TextAlign.Center,
                Weight: FontWeight.Regular,
                Style: FontStyle.Normal,
                FontSize: theme.FontSizeTitle);

            nodes[i] = box with { Children = [indicator] };
        }

        return tree with { Nodes = nodes };
    }

    /// <summary>Returns the last segment of a qualified package name for use as a folder label.</summary>
    private static string SimplePackageName(string package)
    {
        var sep = package.LastIndexOf("::", StringComparison.Ordinal);
        return sep >= 0 ? package[(sep + 2)..] : package;
    }
}
