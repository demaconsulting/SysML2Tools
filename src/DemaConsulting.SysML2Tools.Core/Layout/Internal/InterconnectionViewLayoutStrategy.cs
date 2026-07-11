// <copyright file="InterconnectionViewLayoutStrategy.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Rendering.Internal;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Layout.Internal;

/// <summary>
/// Layout strategy for Interconnection View diagrams.
/// </summary>
/// <remarks>
/// <para>
/// Shows the internal structure of a single part definition: its nested part usages as
/// boxes placed by the bundled layered algorithm, ports on the box boundaries, and connection
/// usages routed as orthogonal connector polylines between the ports, all enclosed by a container
/// box for the host definition.
/// </para>
/// <para>
/// Each nested part's ports are modeled as named <c>LayoutGraphPort</c>s on the layered
/// algorithm's input graph (via <see cref="LayeredPlacement.PlaceWithPorts"/>), so the engine
/// itself resolves port sides, spacing, and any resulting box-height growth needed to keep
/// connectors visually distinct regardless of connection count — the strategy no longer computes
/// box heights or port positions by hand. Every part node is also flagged as carrying a title
/// (<c>HasLabel</c>/<c>HasKeyword</c>), so the engine's automatic title-vs-side-port reservation
/// keeps ports clear of each box's own title band instead of only growing the box height.
/// </para>
/// <para>
/// When a nested part is itself typed by a <c>part def</c> that has its own internal parts, the
/// strategy lays out that inner structure recursively (bottom-up): the inner definition is laid out
/// first with the same flat algorithm, the container part is then treated as an atomic fixed-size node
/// by the parent, and the inner content is nested as the container box's
/// <see cref="LayoutBox.Children"/>. A single-level model (no part typed by a definition with internal
/// parts) is a strict no-op: the recursion never fires and the output is identical to the
/// non-recursive layout. Recursion is driven here, at the strategy level, because container detection
/// is a semantic-model concern the model-independent algorithm cannot see.
/// </para>
/// </remarks>
internal sealed class InterconnectionViewLayoutStrategy : ILayoutStrategy
{
    /// <summary>Minimum width of a nested part box.</summary>
    private const double MinPartWidth = 110.0;

    /// <summary>Approximate width-per-character factor relative to the title font size.</summary>
    private const double CharWidthFactor = 0.62;

    /// <summary>
    /// Uniform padding the layered algorithm adds around placed content (mirrors its internal
    /// content padding), used to give routed connectors the same trailing inset as the boxes.
    /// </summary>
    private const double LayeredContentPadding = 20.0;

    /// <summary>
    /// A nested part usage with its computed intrinsic box size. When the part is a container (its
    /// type is a <c>part def</c> with its own internal parts), <see cref="InnerContent"/> holds the
    /// pre-laid-out interior content positioned relative to the part box's own top-left
    /// <c>(0, 0)</c>; for a leaf part it is <see langword="null"/>.
    /// </summary>
    private sealed record PartItem(
        string Name,
        string Keyword,
        string? Typing,
        double Width,
        double Height,
        IReadOnlyList<LayoutNode>? InnerContent);

    /// <summary>
    /// A resolved binary connection between two nested-part indices, together with the port-name
    /// label for each end (the dotted-reference remainder after the resolved part, e.g.
    /// <c>"encoder"</c> for <c>StepperMotorX.encoder</c>), or <see langword="null"/> when the
    /// endpoint reference names the part directly with no port segment.
    /// </summary>
    private sealed record ConnPair(int A, int B, string? LabelA, string? LabelB);

    /// <summary>The laid-out interior of one definition: its full container size and content.</summary>
    /// <param name="Width">Full container width including title area and insets.</param>
    /// <param name="Height">Full container height including title area and insets.</param>
    /// <param name="Content">Part boxes, ports, and connector lines positioned with origin <c>(0, 0)</c>.</param>
    private sealed record InteriorLayout(double Width, double Height, IReadOnlyList<LayoutNode> Content);

    /// <inheritdoc/>
    public LayoutTree BuildLayout(ViewContext context, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var theme = options.Theme;

        var scope = ExposeScopeResolver.ResolveExposedScope(context.Workspace, context.ViewNode);

        // Choose the part definition whose internals to show.
        var root = FindRoot(context.Workspace, scope);
        if (root is null)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // No nested part usages means there is nothing to draw.
        var hasParts = root.Children.OfType<SysmlFeatureNode>().Any(f => f.FeatureKeyword == "part");
        if (!hasParts)
        {
            return new LayoutTree(200.0, 100.0, []);
        }

        // Index of candidate container definitions (non-stdlib part defs with at least one part child).
        var defsByName = BuildDefinitionIndex(context.Workspace);

        // Lay out the root's interior, recursing into any container parts.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (root.QualifiedName is { Length: > 0 })
        {
            visited.Add(root.QualifiedName);
        }

        var interior = LayOutInterior(root, theme, depth: 0, defsByName, visited, scope);

        // Container box for the root part definition. The root sits at the same origin (0, 0)
        // that interior.Content is already positioned relative to, so the interior content is
        // nested directly as this box's Children (mirroring MakePartBox's own nesting pattern)
        // with no translation needed.
        var rootBox = new LayoutBox(
            X: 0,
            Y: 0,
            Width: interior.Width,
            Height: interior.Height,
            Label: root.Name ?? "Interconnection",
            Depth: 0,
            Shape: BoxShape.Rectangle,
            Compartments: [],
            Children: interior.Content,
            Keyword: string.IsNullOrEmpty(root.DefinitionKeyword) ? "part def" : root.DefinitionKeyword);

        return new LayoutTree(interior.Width, interior.Height, [rootBox]);
    }

    /// <summary>
    /// Lays out the interior of one definition: collects its parts (recursing into container
    /// parts), places them with the bundled layered algorithm, and emits one rounded
    /// box per part plus a port pair and connector line per connection — all positioned relative to
    /// the container's own top-left origin <c>(0, 0)</c>.
    /// </summary>
    /// <param name="def">The definition whose interior to lay out.</param>
    /// <param name="theme">The active rendering theme.</param>
    /// <param name="depth">Nesting depth of this definition's container box (0 for the root).</param>
    /// <param name="defsByName">Container-definition index keyed by qualified and simple name.</param>
    /// <param name="visited">Qualified names already on the recursion path, guarding against cycles.</param>
    /// <param name="scope">
    /// The view's resolved <c>expose</c> containment-subtree scope, or null when no scoping applies.
    /// </param>
    /// <returns>The laid-out interior size and content.</returns>
    private static InteriorLayout LayOutInterior(
        SysmlDefinitionNode def,
        Theme theme,
        int depth,
        IReadOnlyDictionary<string, SysmlDefinitionNode> defsByName,
        ISet<string> visited,
        ExposedScope? scope)
    {
        var parts = CollectParts(def, theme, depth, defsByName, visited, scope);
        var partIndex = BuildPartIndex(parts);
        var pairs = ResolveConnections(def, partIndex);

        var nodeSizes = parts.Select(p => (p.Width, p.Height, HasLabel: true, HasKeyword: true)).ToList();

        var portEdges = pairs
            .Select(p => new PortEdge(
                p.A,
                p.B,
                new EdgePortRef(p.LabelA),
                new EdgePortRef(p.LabelB)))
            .ToList();

        // Delegate all placement and routing to the layered algorithm. Ports are modeled as named
        // LayoutGraphPorts on each connection's endpoints, so the engine resolves port sides,
        // spacing, and any resulting box-height growth needed to keep connectors visually distinct
        // regardless of connection count. Every part is flagged HasLabel/HasKeyword (matching the
        // hasLabel/hasKeyword: true convention already used for BoxMetrics.TitleAreaHeight below and
        // in ComputePartSize), which activates the engine's automatic title-vs-side-port reservation
        // so ports never land within the box's own title band. Parallel-edge merging is disabled so
        // distinct SysML connections between the same two parts (e.g. a "power" and an "encoder"
        // connection both wired between the same two nested parts) are each preserved as their own
        // independently-routed connector instead of collapsing onto one shared route.
        var placed = LayeredPlacement.PlaceWithPorts(nodeSizes, portEdges, LayoutFlowDirection.Right);

        // Shift placed content down/right to sit inside the container box.
        var titleArea = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var offsetX = theme.LabelPadding * 2.0;
        var offsetY = titleArea + (theme.LabelPadding * 2.0);

        var containerWidth = placed.Width + (offsetX * 2.0);
        var containerHeight = placed.Height + offsetY + (theme.LabelPadding * 2.0);

        // The layered algorithm derives its size from box extents only, but a connector can route
        // beyond the boxes (e.g. wrapping below them). Extend the container so every waypoint is
        // enclosed with the same trailing inset the boxes already receive, so no connector scrapes the
        // container edge.
        var trailingInset = LayeredContentPadding + (theme.LabelPadding * 2.0);
        foreach (var wp in placed.EdgePolylines)
        {
            foreach (var p in wp)
            {
                containerWidth = Math.Max(containerWidth, p.X + offsetX + trailingInset);
                containerHeight = Math.Max(containerHeight, p.Y + offsetY + trailingInset);
            }
        }

        var content = new List<LayoutNode>();

        // One rounded box per nested part usage; container parts carry their nested children.
        for (var i = 0; i < parts.Count; i++)
        {
            var r = placed.Rects[i];
            content.Add(MakePartBox(parts[i], new Rect(r.X + offsetX, r.Y + offsetY, r.Width, r.Height), depth + 1));
        }

        // One port pair and one connector line per connection. The algorithm returns exactly one
        // routed polyline per input connection, in input order and oriented source -> target, so
        // connection k uses EdgePolylines[k] directly.
        for (var k = 0; k < pairs.Count; k++)
        {
            var wp = placed.EdgePolylines[k];
            if (wp.Count < 2)
            {
                continue;
            }

            // Shift all waypoints by the container offset.
            var shifted = wp.Select(p => new Point2D(p.X + offsetX, p.Y + offsetY)).ToList();

            var edgePorts = placed.EdgePorts[k];

            // Source/target ports: engine-placed ports on the source/target boxes' resolved faces,
            // labeled with the SysML port-name segment from the connection's endpoint reference, if
            // any, and translated by the same container offset as the boxes and waypoints.
            if (edgePorts.Source is { } sourcePort)
            {
                content.Add(sourcePort with
                {
                    CentreX = sourcePort.CentreX + offsetX,
                    CentreY = sourcePort.CentreY + offsetY,
                });
            }

            if (edgePorts.Target is { } targetPort)
            {
                content.Add(targetPort with
                {
                    CentreX = targetPort.CentreX + offsetX,
                    CentreY = targetPort.CentreY + offsetY,
                });
            }

            content.Add(new LayoutLine(
                Waypoints: shifted,
                SourceEnd: EndMarkerStyle.None,
                TargetEnd: EndMarkerStyle.None,
                LineStyle: LineStyle.Solid,
                MidpointLabel: null));
        }

        return new InteriorLayout(containerWidth, containerHeight, content);
    }

    /// <summary>
    /// Finds the part definition whose interior to render: the non-stdlib <c>part def</c>
    /// with the most connections, falling back to the one with the most part usages. When
    /// <paramref name="scope"/> is non-null (the view's resolved <c>expose</c> containment-subtree
    /// scope), candidates are first restricted to those relevant to the scope via
    /// <see cref="ExposeScopeResolver.IsRootRelevantToScope"/> — the candidate itself is an exposed
    /// subject, lies within an exposed subject's subtree, or an exposed subject lies within the
    /// candidate's own subtree; because a nested definition and its ancestor can both be
    /// scope-relevant, ties among relevant candidates are then broken by specificity (deepest/longest
    /// qualified name wins) via <see cref="ExposeScopeResolver.IsMoreSpecificCandidate"/>, with the
    /// connections/parts heuristic used only to break ties between equally specific candidates. When
    /// no candidate is scope-relevant, no root is chosen (an empty canvas results, matching the
    /// existing null-root path). When <paramref name="scope"/> is <see langword="null"/>, selection is
    /// the plain connections/parts heuristic, unchanged.
    /// </summary>
    private static SysmlDefinitionNode? FindRoot(SysmlWorkspace workspace, ExposedScope? scope)
    {
        SysmlDefinitionNode? best = null;
        string? bestQualifiedName = null;
        var bestConnections = -1;
        var bestParts = -1;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            if (node is not SysmlDefinitionNode def || def.DefinitionKeyword != "part def")
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            if (scope is not null && !ExposeScopeResolver.IsRootRelevantToScope(qualifiedName, scope))
            {
                continue;
            }

            var connections = def.Children.OfType<SysmlConnectionNode>().Count();
            var partCount = def.Children.OfType<SysmlFeatureNode>().Count(f => f.FeatureKeyword == "part");
            var scoreBetter = connections > bestConnections || (connections == bestConnections && partCount > bestParts);
            var isBetter = scope is not null
                ? ExposeScopeResolver.IsMoreSpecificCandidate(qualifiedName, bestQualifiedName, scoreBetter)
                : scoreBetter;

            if (isBetter)
            {
                best = def;
                bestQualifiedName = qualifiedName;
                bestConnections = connections;
                bestParts = partCount;
            }
        }

        return best;
    }

    /// <summary>
    /// Collects the nested part usages of a definition, sized for rendering. A part whose type
    /// resolves to a container definition (a non-stdlib <c>part def</c> with its own internal parts,
    /// not already on the recursion path) is laid out recursively and sized to fit its interior;
    /// every other part is sized intrinsically as a leaf. When <paramref name="scope"/> is non-null,
    /// a part feature whose own <c>QualifiedName</c> fails
    /// <see cref="ExposeScopeResolver.IsInSubjectScope"/> is skipped; the same absolute
    /// <paramref name="scope"/> list is passed unchanged into recursive container calls, since
    /// subject qualified names are absolute and need no re-resolution at deeper nesting levels.
    /// </summary>
    private static IReadOnlyList<PartItem> CollectParts(
        SysmlDefinitionNode root,
        Theme theme,
        int depth,
        IReadOnlyDictionary<string, SysmlDefinitionNode> defsByName,
        ISet<string> visited,
        ExposedScope? scope)
    {
        var result = new List<PartItem>();
        foreach (var feature in root.Children.OfType<SysmlFeatureNode>())
        {
            if (feature.FeatureKeyword != "part")
            {
                continue;
            }

            if (scope is not null && feature.QualifiedName is { Length: > 0 } fqn &&
                !ExposeScopeResolver.IsInSubjectScope(fqn, scope))
            {
                continue;
            }

            var name = feature.Name ?? feature.FeatureTyping ?? "part";

            if (TryResolveContainer(feature.FeatureTyping, defsByName, visited, out var childDef))
            {
                // Container part: lay out its interior bottom-up and treat it as an atomic node.
                var childVisited = new HashSet<string>(visited, StringComparer.Ordinal) { childDef.QualifiedName! };
                var inner = LayOutInterior(childDef, theme, depth + 1, defsByName, childVisited, scope);
                result.Add(new PartItem(name, "part", feature.FeatureTyping, inner.Width, inner.Height, inner.Content));
            }
            else
            {
                // Leaf part: intrinsic size, no nested content.
                var (width, height) = ComputePartSize(name, feature.FeatureTyping, theme);
                result.Add(new PartItem(name, "part", feature.FeatureTyping, width, height, null));
            }
        }

        return result;
    }

    /// <summary>
    /// Builds an index of candidate container definitions — non-standard-library <c>part def</c>s
    /// that have at least one nested <c>part</c> usage — keyed by both qualified and simple name
    /// (qualified preferred), mirroring the resolve-by-qualified-then-simple pattern used by the
    /// General view strategy.
    /// </summary>
    private static IReadOnlyDictionary<string, SysmlDefinitionNode> BuildDefinitionIndex(SysmlWorkspace workspace)
    {
        var index = new Dictionary<string, SysmlDefinitionNode>(StringComparer.Ordinal);
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            if (node is not SysmlDefinitionNode def || def.DefinitionKeyword != "part def")
            {
                continue;
            }

            if (StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            var hasPartChild = def.Children.OfType<SysmlFeatureNode>().Any(f => f.FeatureKeyword == "part");
            if (!hasPartChild)
            {
                continue;
            }

            index.TryAdd(qualifiedName, def);
            if (def.Name is { Length: > 0 })
            {
                index.TryAdd(def.Name, def);
            }
        }

        return index;
    }

    /// <summary>
    /// Resolves a part's type reference to a container definition by qualified then simple name,
    /// excluding any definition already on the recursion path (cycle guard). A part whose type is
    /// on the path — or does not resolve to a container — is treated as a leaf.
    /// </summary>
    private static bool TryResolveContainer(
        string? typing,
        IReadOnlyDictionary<string, SysmlDefinitionNode> defsByName,
        ISet<string> visited,
        out SysmlDefinitionNode childDef)
    {
        childDef = null!;
        if (string.IsNullOrEmpty(typing))
        {
            return false;
        }

        if (!defsByName.TryGetValue(typing, out var def))
        {
            var sep = typing.LastIndexOf("::", StringComparison.Ordinal);
            var simple = sep >= 0 ? typing[(sep + 2)..] : typing;
            if (!defsByName.TryGetValue(simple, out def))
            {
                return false;
            }
        }

        if (def.QualifiedName is null || visited.Contains(def.QualifiedName))
        {
            return false;
        }

        childDef = def;
        return true;
    }

    /// <summary>Builds a name → index lookup for the nested parts.</summary>
    private static Dictionary<string, int> BuildPartIndex(IReadOnlyList<PartItem> parts)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < parts.Count; i++)
        {
            index.TryAdd(parts[i].Name, i);
        }

        return index;
    }

    /// <summary>
    /// Resolves each binary connection's endpoints to nested-part indices by matching the
    /// first segment of the dotted endpoint reference against the part names, capturing any
    /// remaining dotted segment(s) as the port-name label for that end.
    /// </summary>
    private static IReadOnlyList<ConnPair> ResolveConnections(
        SysmlDefinitionNode root,
        Dictionary<string, int> partIndex)
    {
        var pairs = new List<ConnPair>();
        foreach (var conn in root.Children.OfType<SysmlConnectionNode>())
        {
            var (a, labelA) = ResolveEndpoint(conn.EndpointA, partIndex);
            var (b, labelB) = ResolveEndpoint(conn.EndpointB, partIndex);
            if (a >= 0 && b >= 0 && a != b)
            {
                pairs.Add(new ConnPair(a, b, labelA, labelB));
            }
        }

        return pairs;
    }

    /// <summary>
    /// Resolves a dotted endpoint reference (e.g. <c>"StepperMotorX.encoder"</c>) to a part index by
    /// its first segment, and returns the remaining dotted segment(s) — the SysML port-name portion
    /// of the reference — as the port label. A reference with no further segments (a bare part name)
    /// resolves with a <see langword="null"/> label.
    /// </summary>
    /// <remarks>
    /// This resolves the <em>full</em> dotted path for labeling purposes, so a deeper reference such
    /// as <c>"board.cpu"</c> (into a nested part inside a container) yields the label <c>"cpu"</c>
    /// rather than discarding it. The connector itself still terminates at the container box's own
    /// port — see the "Cross-boundary limitation" note in the design documentation for what one-level
    /// cross-boundary routing this strategy does and does not perform.
    /// </remarks>
    private static (int Index, string? Label) ResolveEndpoint(string? reference, Dictionary<string, int> partIndex)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return (-1, null);
        }

        var dot = reference.IndexOf('.', StringComparison.Ordinal);
        var head = dot >= 0 ? reference[..dot] : reference;
        var label = dot >= 0 ? reference[(dot + 1)..] : null;
        return partIndex.TryGetValue(head, out var i) ? (i, label) : (-1, null);
    }

    /// <summary>Computes the intrinsic size of a nested part box.</summary>
    private static (double Width, double Height) ComputePartSize(string name, string? typing, Theme theme)
    {
        var label = typing is { Length: > 0 } ? $"{name} : {typing}" : name;
        var labelWidth = (label.Length * theme.FontSizeTitle * CharWidthFactor) + (2.0 * theme.LabelPadding);
        var width = Math.Max(MinPartWidth, labelWidth);
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true) + theme.LabelPadding;
        return (width, height);
    }

    /// <summary>
    /// Creates a rounded-rectangle part usage box at the given position. A leaf part has no
    /// children; a container part nests its pre-laid-out interior content, translated from the
    /// child's local origin <c>(0, 0)</c> to the box's absolute top-left so the inner part boxes
    /// land below the container's title and inside its border.
    /// </summary>
    private static LayoutBox MakePartBox(PartItem part, Rect rect, int depth)
    {
        var label = part.Typing is { Length: > 0 } ? $"{part.Name} : {part.Typing}" : part.Name;
        var children = part.InnerContent is null
            ? (IReadOnlyList<LayoutNode>)[]
            : TranslateNodes(part.InnerContent, rect.X, rect.Y);

        return new LayoutBox(
            X: rect.X,
            Y: rect.Y,
            Width: rect.Width,
            Height: rect.Height,
            Label: label,
            Depth: depth,
            Shape: BoxShape.RoundedRectangle,
            Compartments: [],
            Children: children,
            Keyword: part.Keyword);
    }

    /// <summary>
    /// Recursively translates a list of layout nodes by <paramref name="dx"/>/<paramref name="dy"/>,
    /// shifting box positions (and their nested children), port centres, and connector waypoints.
    /// Used to re-anchor a container's interior content from its local origin to absolute coordinates.
    /// </summary>
    private static IReadOnlyList<LayoutNode> TranslateNodes(IReadOnlyList<LayoutNode> nodes, double dx, double dy)
    {
        var result = new List<LayoutNode>(nodes.Count);
        foreach (var node in nodes)
        {
            result.Add(node switch
            {
                LayoutBox box => box with
                {
                    X = box.X + dx,
                    Y = box.Y + dy,
                    Children = TranslateNodes(box.Children, dx, dy),
                },
                LayoutPort port => port with { CentreX = port.CentreX + dx, CentreY = port.CentreY + dy },
                LayoutLine line => line with
                {
                    Waypoints = line.Waypoints.Select(p => new Point2D(p.X + dx, p.Y + dy)).ToList(),
                },
                _ => node,
            });
        }

        return result;
    }
}
