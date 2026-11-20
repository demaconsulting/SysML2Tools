// <copyright file="DiagramRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Semantic.Model;

namespace DemaConsulting.SysML2Tools.Rendering;

/// <summary>
/// High-level rendering orchestrator that iterates over all views in a
/// <see cref="SysmlWorkspace"/>, builds a <see cref="LayoutTree"/> via an
/// <see cref="ILayoutStrategy"/>, and writes each view to an output stream via
/// an <see cref="IRenderer"/>.
/// </summary>
/// <remarks>
/// <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/> is the single entry point for the rendering pipeline.
/// It delegates layout selection to <see cref="Internal.DiagramTypeRouter"/> and collects
/// one <see cref="RenderOutput"/> per view found in <see cref="SysmlWorkspace.Declarations"/>.
/// Views whose type is not supported by any available strategy are silently skipped.
/// <para>
/// The <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/> example below requires a concrete <c>IRenderer</c>
/// implementation from a renderer package — see the <c>DemaConsulting.SysML2Tools.Rendering</c>
/// namespace remarks for the exact package reference(s) needed (e.g.
/// <c>DemaConsulting.Rendering.Svg</c> for <c>SvgRenderer</c>).
/// </para>
/// </remarks>
public sealed class DiagramRenderer
{
    /// <summary>Media type of the PNG renderer, the only raster format the tool offers.</summary>
    private const string PngMediaType = "image/png";

    /// <summary>The raster backend's maximum surface width and height, in pixels.</summary>
    private const int MaxPngDimension = 8192;

    /// <summary>The number of times a rejected PNG scale is tightened and retried.</summary>
    private const int MaxFitAttempts = 3;

    /// <summary>Slack applied when tightening the scale so integer rounding cannot exceed the limit.</summary>
    private const double FitSlack = 0.999;

    /// <summary>
    /// Initializes a new <see cref="DiagramRenderer"/> instance. The renderer is stateless; a
    /// single instance may be reused across multiple <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/> calls.
    /// </summary>
    public DiagramRenderer()
    {
    }

    /// <summary>
    /// Identifies a single renderable view by both its fully qualified name and its display
    /// name (the name used to derive the output file name).
    /// </summary>
    /// <param name="QualifiedName">
    /// The view's fully qualified name (e.g. <c>"PkgA::SharedView"</c>), unique within the
    /// workspace.
    /// </param>
    /// <param name="DisplayName">
    /// The view's display name: the simple <c>Name</c> when present, otherwise the qualified
    /// name. Two views in different packages may share the same display name.
    /// </param>
    public readonly record struct ViewIdentity(string QualifiedName, string DisplayName);

    /// <summary>
    /// Returns the display names of all renderable user-defined views in the workspace,
    /// mirroring the filtering applied by <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/>.
    /// </summary>
    /// <param name="workspace">The SysML workspace whose view declarations are inspected.</param>
    /// <returns>
    /// An ordered list of view display names. Returns an empty list when the workspace
    /// contains no renderable view declarations.
    /// </returns>
    public static IReadOnlyList<string> GetViewNames(SysmlWorkspace workspace) =>
        GetViewIdentities(workspace).Select(identity => identity.DisplayName).ToList();

    /// <summary>
    /// Returns the qualified and display names of all renderable user-defined views in the
    /// workspace, in the same order that <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/> produces its outputs.
    /// </summary>
    /// <param name="workspace">The SysML workspace whose view declarations are inspected.</param>
    /// <returns>
    /// An ordered list of <see cref="ViewIdentity"/> values, one per renderable view. Returns an
    /// empty list when the workspace contains no renderable view declarations.
    /// </returns>
    /// <remarks>
    /// Callers that need to correlate each <see cref="RenderOutput"/> produced by
    /// <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/> with the originating view's qualified name (for example, to
    /// report a collision between two views that sanitize to the same output file name) may zip
    /// this list by index against the <see cref="RenderWorkspace(SysmlWorkspace, IRenderer, RenderOptions, string)"/> result, because both methods
    /// apply the identical filter/iteration over <see cref="SysmlWorkspace.Declarations"/> with no
    /// intervening mutation.
    /// </remarks>
    public static IReadOnlyList<ViewIdentity> GetViewIdentities(SysmlWorkspace workspace)
    {
        // Validate input — null workspace would produce silent failures
        ArgumentNullException.ThrowIfNull(workspace);

        var identities = new List<ViewIdentity>();

        // Iterate over all declarations and collect each renderable view's identity
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Skip non-view declarations
            if (node is not SysmlViewNode viewNode)
            {
                continue;
            }

            // Skip stdlib view declarations — only user-defined views are considered
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            // Skip views whose type is not supported by any available strategy
            var strategy = Internal.DiagramTypeRouter.GetStrategy(viewNode, workspace, out var unsupportedMsg);
            if (unsupportedMsg != null)
            {
                continue;
            }

            // Suppress unused variable warning — strategy is validated but not used here
            _ = strategy;

            // Resolve the display name: prefer the simple name, fall back to qualified name
            var displayName = viewNode.Name ?? qualifiedName;
            identities.Add(new ViewIdentity(qualifiedName, displayName));
        }

        return identities;
    }

    /// <summary>
    /// Renders every view in the workspace and returns a collection of output streams, one per
    /// view, without collecting view-level diagnostics. Kept so assemblies compiled against the
    /// original signature continue to bind.
    /// </summary>
    /// <param name="workspace">The SysML workspace whose view declarations are rendered.</param>
    /// <param name="renderer">The format-specific renderer used to write each view.</param>
    /// <param name="options">Render options supplying theme, scale, and depth-limit settings.</param>
    /// <param name="viewFilter">
    /// When non-null, only the view whose display name equals this value is rendered.
    /// When null, all renderable views are rendered.
    /// </param>
    /// <returns>
    /// An ordered list of <see cref="RenderOutput"/> instances, one per view in declaration order.
    /// </returns>
    public IReadOnlyList<RenderOutput> RenderWorkspace(
        SysmlWorkspace workspace,
        IRenderer renderer,
        RenderOptions options,
        string? viewFilter = null) =>
        RenderWorkspace(workspace, renderer, options, viewFilter, diagnostics: null);

    /// <summary>
    /// Renders every view in the workspace and returns a collection of output streams,
    /// one per view.
    /// </summary>
    /// <remarks>Equivalent to the four-argument overload, additionally collecting view-level diagnostics.
    /// When <paramref name="renderer"/> is a PNG renderer and a view's diagram is larger than the raster
    /// backend's 8192 px maximum, the image is scaled down uniformly to fit (through
    /// <see cref="RenderOptions.Scale"/>) and a warning naming the view and both sizes is added to that
    /// output's <see cref="RenderOutput.Warnings"/>; the render does not fail.</remarks>
    /// <param name="workspace">The SysML workspace whose view declarations are rendered.</param>
    /// <param name="renderer">The format-specific renderer used to write each view.</param>
    /// <param name="options">Render options supplying theme, scale, and depth-limit settings.</param>
    /// <param name="viewFilter">
    /// When non-null, only the view whose display name equals this value is rendered.
    /// When null, all renderable views are rendered.
    /// </param>
    /// <param name="diagnostics">
    /// When non-null, receives view-level diagnostics located at each view's declaration (empty
    /// file path and line 0 for synthesized views): a <see cref="DiagnosticSeverity.Warning"/> for
    /// a view whose laid-out diagram contains nothing renderable (naming the view and, for each
    /// bare <c>expose &lt;Package&gt;;</c> statement, suggesting <c>expose &lt;Package&gt;::**;</c>),
    /// or an <see cref="DiagnosticSeverity.Info"/> hint for a view that draws a bare-exposed
    /// package as an empty package node (suggesting <c>expose &lt;Package&gt;::**;</c> to show its
    /// members). Views with neither condition add nothing. Every view is still rendered and returned.
    /// </param>
    /// <returns>
    /// An ordered list of <see cref="RenderOutput"/> instances, one per view in declaration order.
    /// Returns an empty list when the workspace contains no view declarations.
    /// </returns>
    /// <example>
    /// <code>
    /// var diagramRenderer = new DiagramRenderer();
    /// var svgRenderer = new SvgRenderer();
    /// var options = new RenderOptions(Themes.Light);
    /// var outputs = diagramRenderer.RenderWorkspace(workspace, svgRenderer, options);
    ///
    /// foreach (var output in outputs)
    /// {
    ///     await using var file = File.Create(output.SuggestedFileName);
    ///     await output.Data.CopyToAsync(file);
    /// }
    /// </code>
    /// </example>
    // S2325: instance method — future phases will inject ILayoutStrategy via constructor making this non-static
#pragma warning disable S2325
    public IReadOnlyList<RenderOutput> RenderWorkspace(
        SysmlWorkspace workspace,
        IRenderer renderer,
        RenderOptions options,
        string? viewFilter,
        ICollection<SysmlDiagnostic>? diagnostics)
#pragma warning restore S2325
    {
        // Validate inputs — null workspace or renderer would produce silent failures
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);

        var results = new List<RenderOutput>();

        // Iterate over all declarations and process each view node
        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Skip non-view declarations
            if (node is not SysmlViewNode viewNode)
            {
                continue;
            }

            // Skip stdlib view declarations — only user-defined views are rendered
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            // Route the view to an appropriate layout strategy
            var strategy = Internal.DiagramTypeRouter.GetStrategy(viewNode, workspace, out var unsupportedMsg);
            if (unsupportedMsg != null)
            {
                // Strategy not available for this view type — skip silently
                continue;
            }

            // Resolve the display name: prefer the simple name, fall back to qualified name
            var viewName = viewNode.Name ?? qualifiedName;

            // Skip views that do not match the active filter
            if (viewFilter != null && !string.Equals(viewName, viewFilter, StringComparison.Ordinal))
            {
                continue;
            }

            var context = new ViewContext(viewName, workspace, viewNode);

            // Build the layout tree for this view
            var layout = ApplyNameStyleDefault(strategy.BuildLayout(context, options));

            // Report a view with nothing renderable (a warning), or a lone exposed package drawn
            // without its contents (an informational hint); the view is still rendered either way.
            if (diagnostics is not null &&
                BuildViewDiagnostic(
                    viewName,
                    viewNode,
                    workspace,
                    layout.Nodes.Count == 0,
                    strategy is Layout.Internal.GeneralViewLayoutStrategy) is { } diagnostic)
            {
                diagnostics.Add(diagnostic);
            }

            // Render the layout tree to an in-memory stream; a PNG too large for the raster
            // backend is scaled down uniformly to fit, and the scaling is reported as a warning.
            var (stream, scaleWarning) = RenderToStream(viewName, layout, renderer, options);

            // Derive a safe file name from the view name and add to results
            var fileName = SanitizeFileName(viewName) + renderer.DefaultExtension;
            IReadOnlyList<string> warnings = scaleWarning is null ? layout.Warnings : [.. layout.Warnings, scaleWarning];
            results.Add(new RenderOutput(fileName, renderer.MediaType, stream) { Warnings = warnings });
        }

        return results;
    }

    /// <summary>
    /// Renders a laid-out view to an in-memory stream, scaling a PNG that would exceed the raster
    /// backend's maximum dimension down uniformly (through <see cref="RenderOptions.Scale"/>) so it fits.
    /// </summary>
    /// <remarks>
    /// The PNG surface size is not known to the caller beyond <see cref="LayoutTree.Width"/> and
    /// <see cref="LayoutTree.Height"/> (connector labels can extend it slightly), so the scale is
    /// first derived from the layout size and, should the backend still reject the resulting
    /// surface, tightened from the size the backend reports and retried a few times. Non-PNG
    /// renderers (SVG is vector) are never scaled.
    /// </remarks>
    /// <param name="viewName">The view's display name, used in the scaling warning.</param>
    /// <param name="layout">The laid-out view.</param>
    /// <param name="renderer">The format renderer.</param>
    /// <param name="options">The caller's render options.</param>
    /// <returns>The rendered stream (positioned at its start) and the scaling warning, or null when none applies.</returns>
    private static (MemoryStream Stream, string? Warning) RenderToStream(
        string viewName, LayoutTree layout, IRenderer renderer, RenderOptions options)
    {
        var stream = new MemoryStream();
        if (!string.Equals(renderer.MediaType, PngMediaType, StringComparison.OrdinalIgnoreCase))
        {
            renderer.Render(layout, options, stream);
            stream.Position = 0;
            return (stream, null);
        }

        // Fit the layout's longest side to the backend limit before the first attempt
        var scale = options.Scale;
        var longestSide = Math.Max(layout.Width, layout.Height) * scale;
        if (longestSide > MaxPngDimension)
        {
            scale *= MaxPngDimension / longestSide;
        }

        for (var attempt = 1; attempt <= MaxFitAttempts + 1; attempt++)
        {
            try
            {
                stream.SetLength(0);
                renderer.Render(layout, options with { Scale = scale }, stream);
                break;
            }
            catch (ArgumentOutOfRangeException ex) when (
                attempt <= MaxFitAttempts &&
                ex.ParamName is "width" or "height" &&
                ex.ActualValue is int actual &&
                actual > MaxPngDimension)
            {
                // Label overhang made the surface slightly larger than the layout; tighten the scale
                scale *= (double)MaxPngDimension / actual * FitSlack;
            }
        }

        stream.Position = 0;
        if (scale >= options.Scale)
        {
            return (stream, null);
        }

        // Report the original and the actual scaled size (read from the PNG header, falling back to
        // the computed size)
        var originalWidth = (int)Math.Ceiling(layout.Width * options.Scale);
        var originalHeight = (int)Math.Ceiling(layout.Height * options.Scale);
        var (scaledWidth, scaledHeight) = ReadPngSize(stream)
            ?? ((int)Math.Ceiling(layout.Width * scale), (int)Math.Ceiling(layout.Height * scale));
        var warning =
            $"Diagram '{viewName}' ({originalWidth}×{originalHeight} px) exceeds the {MaxPngDimension} px PNG limit; " +
            $"scaled to {scaledWidth}×{scaledHeight} px. Use SVG or narrow the view.";
        return (stream, warning);
    }

    /// <summary>Reads the pixel size from the header chunk of a PNG stream.</summary>
    /// <param name="stream">The stream holding the encoded PNG.</param>
    /// <returns>The width and height, or null when the stream is too short to hold a PNG header.</returns>
    private static (int Width, int Height)? ReadPngSize(MemoryStream stream)
    {
        // Signature (8) + chunk length (4) + chunk type (4), then big-endian width and height
        const int widthOffset = 16;
        if (stream.Length < widthOffset + 8)
        {
            return null;
        }

        var bytes = stream.GetBuffer();
        return (ReadBigEndianInt32(bytes, widthOffset), ReadBigEndianInt32(bytes, widthOffset + 4));
    }

    /// <summary>Reads a big-endian 32-bit integer.</summary>
    /// <param name="bytes">The buffer.</param>
    /// <param name="offset">The offset of the first byte.</param>
    /// <returns>The value.</returns>
    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    /// <summary>
    /// Synthesizes a <see cref="SysmlViewNode"/> that targets the most representative
    /// top-level element in the workspace, for use when the user passes <c>--auto</c>
    /// and no user-defined views are present.
    /// </summary>
    /// <param name="workspace">
    /// The SysML workspace to inspect. Must not be null.
    /// </param>
    /// <returns>
    /// A synthetic <see cref="SysmlViewNode"/> whose <c>Name</c> is derived from the chosen
    /// element (e.g. <c>"VehicleA"</c> → <c>"VehicleAView"</c>), or <see langword="null"/>
    /// when the workspace contains no non-stdlib declarations to target.
    /// </returns>
    /// <remarks>
    /// Selection priority:
    /// <list type="number">
    ///   <item>The non-stdlib <c>part def</c> with the highest direct-child count.</item>
    ///   <item>The first non-stdlib definition when no <c>part def</c> exists.</item>
    /// </list>
    /// The returned node has empty <see cref="SysmlNode.Children"/> and
    /// <see cref="SysmlNode.SupertypeNames"/> because the GeneralView layout strategy
    /// derives its content from workspace declarations rather than the view's own children.
    /// </remarks>
    public static SysmlViewNode? SynthesizeAutoView(SysmlWorkspace workspace)
    {
        // Validate input — null workspace would cause a NullReferenceException
        ArgumentNullException.ThrowIfNull(workspace);

        // Collect all non-stdlib definition nodes as candidates for the auto view target
        SysmlDefinitionNode? bestPartDef = null;
        SysmlDefinitionNode? firstDefinition = null;

        foreach (var (qualifiedName, node) in workspace.Declarations)
        {
            // Skip stdlib elements — only user-defined declarations are considered
            if (Internal.StdlibFilter.IsStdlibElement(qualifiedName, workspace.StdlibNames))
            {
                continue;
            }

            // Consider only definition nodes (part def, attribute def, etc.)
            if (node is not SysmlDefinitionNode defNode || defNode.IsExcludedFromDiagrams)
            {
                continue;
            }

            // Record the first non-stdlib definition as the fallback target
            firstDefinition ??= defNode;

            // Prefer a part def, and among part defs prefer the one with the most direct children
            if (string.Equals(defNode.DefinitionKeyword, "part def", StringComparison.Ordinal) &&
                (bestPartDef is null || defNode.DiagramChildren.Count() > bestPartDef.DiagramChildren.Count()))
            {
                bestPartDef = defNode;
            }
        }

        // Choose the best candidate: part def with most children, then any definition, then none
        var target = bestPartDef ?? firstDefinition;
        if (target is null)
        {
            return null;
        }

        // Build the synthesized view name by appending "View" to the element's simple name
        var viewName = (target.Name ?? target.QualifiedName ?? "AutoView") + "View";
        var viewQualifiedName = (target.QualifiedName ?? target.Name ?? "AutoView") + "View";

        return new SysmlViewNode
        {
            Name = viewName,
            QualifiedName = viewQualifiedName
        };
    }

    /// <summary>
    /// Synthesizes a <see cref="SysmlViewNode"/> targeting any resolvable, non-stdlib element in
    /// the workspace with the requested layout kind, for use by the <c>render --view-type
    /// &lt;kind&gt; --view-target &lt;qualified-name&gt; [--filter &lt;expr&gt;]</c> CLI feature
    /// ("dynamic" or "ad-hoc" views).
    /// </summary>
    /// <param name="workspace">The loaded workspace to resolve the target against. Must not be null.</param>
    /// <param name="viewType">
    /// One of <c>"general"</c>, <c>"interconnection"</c>, <c>"state"</c>, <c>"action"</c>,
    /// <c>"sequence"</c>, <c>"grid"</c>, or <c>"browser"</c> (case-sensitive).
    /// </param>
    /// <param name="targetQualifiedName">
    /// The fully-qualified name of the element to render, looked up in <see
    /// cref="SysmlWorkspace.Declarations"/>.
    /// </param>
    /// <param name="filterExpressionText">
    /// The raw <c>--filter</c> expression text, passed through unchanged to the synthesized
    /// node's <see cref="SysmlViewNode.FilterExpressionText"/>, or <see langword="null"/> when no
    /// filter was supplied.
    /// </param>
    /// <param name="diagnostic">
    /// Set to a non-null, human-readable message when synthesis fails (unrecognized
    /// <paramref name="viewType"/>, unresolved or wrong-kind target, a per-kind structural
    /// compatibility failure, or a name collision); <see langword="null"/> on success.
    /// </param>
    /// <returns>
    /// A synthesized <see cref="SysmlViewNode"/> ready to be injected via <see
    /// cref="SysmlWorkspace.AddDeclaration"/>, or <see langword="null"/> when
    /// <paramref name="diagnostic"/> is non-null.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="SynthesizeAutoView"/> (whose node carries no resolved <c>expose</c>
    /// scoping, so the diagram renders the entire workspace), the node returned here is scoped
    /// to exactly <paramref name="targetQualifiedName"/> — see <see
    /// cref="Internal.DynamicViewSynthesizer"/> for the scoping mechanism and per-kind
    /// compatibility rules.
    /// </remarks>
    public static SysmlViewNode? SynthesizeDynamicView(
        SysmlWorkspace workspace,
        string viewType,
        string targetQualifiedName,
        string? filterExpressionText,
        out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(viewType);
        ArgumentNullException.ThrowIfNull(targetQualifiedName);

        var (viewNode, message) = Internal.DynamicViewSynthesizer.Synthesize(
            workspace, viewType, targetQualifiedName, filterExpressionText);
        diagnostic = message;
        return viewNode;
    }

    /// <summary>
    /// Builds the view-level diagnostic, located at the view's declaration: a warning when the
    /// laid-out diagram contains nothing renderable; otherwise, for a layout that draws exposed
    /// packages as package nodes (the general view), an informational hint when the view
    /// exposes a package with a bare <c>expose &lt;Package&gt;;</c> (SysML 8.3.26.3, a
    /// MembershipExpose of the package element itself) while none of that package's members are
    /// exposed, so it is drawn as a package node without contents. Returns null when neither applies.
    /// </summary>
    /// <param name="viewName">The view's display name.</param>
    /// <param name="viewNode">The view's AST node.</param>
    /// <param name="workspace">The workspace used to classify each exposed target.</param>
    /// <param name="isEmpty">Whether the laid-out diagram has no nodes.</param>
    /// <param name="drawsPackageNodes">Whether the layout strategy draws exposed packages as package nodes.</param>
    /// <returns>The diagnostic, or null.</returns>
    private static SysmlDiagnostic? BuildViewDiagnostic(
        string viewName, SysmlViewNode viewNode, SysmlWorkspace workspace, bool isEmpty, bool drawsPackageNodes)
    {
        var scope = Layout.Internal.ExposeScopeResolver.ResolveExposedScope(workspace, viewNode);
        var lonePackages = viewNode.ResolvedExposeMembers
            .Where(entry =>
                entry.Member.RecursionKind == ExposeRecursionKind.MembershipExact &&
                entry.Member.BracketFilterExpressionText is null &&
                workspace.Declarations.TryGetValue(entry.ResolvedQualifiedName, out var target) &&
                target is SysmlPackageNode &&
                (scope is null || !workspace.Declarations.Keys.Any(name =>
                    name.StartsWith(entry.ResolvedQualifiedName + "::", StringComparison.Ordinal) &&
                    Layout.Internal.ExposeScopeResolver.IsInSubjectScope(name, scope))))
            .Select(entry => entry.Member.QualifiedName)
            .ToList();

        string message;
        DiagnosticSeverity severity;
        if (isEmpty)
        {
            severity = DiagnosticSeverity.Warning;
            message = Layout.Internal.LayoutWarnings.ForEmptyView(viewName, lonePackages);
        }
        else if (lonePackages.Count > 0 && drawsPackageNodes)
        {
            severity = DiagnosticSeverity.Info;
            message = Layout.Internal.LayoutWarnings.ForLonePackageExpose(viewName, lonePackages);
        }
        else
        {
            return null;
        }

        return new SysmlDiagnostic(
            viewNode.SourceFilePath ?? string.Empty,
            viewNode.SourceLine,
            viewNode.SourceColumn,
            severity,
            message);
    }

    /// <summary>
    /// Produces a file-system-safe name by replacing any character that is invalid
    /// in a file name with an underscore.
    /// </summary>
    /// <param name="name">The raw view name to sanitize.</param>
    /// <returns>A string containing only characters that are valid in file names.</returns>
    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>
    /// Applies the SysML notation's default name weight to a laid-out tree: every box and lifeline
    /// header whose strategy did not request a name style draws its name in regular weight (the
    /// <see cref="BoxAppearance"/> default of the rendering engine is bold).
    /// </summary>
    /// <remarks>
    /// The SysML v2 graphical notation (OMG formal/2025-09-03, clause 8.2.3 graphical BNF and
    /// Tables 3, 4, 9, and 10) and the OMG Pilot Implementation draw node names in regular weight
    /// (a node's «keyword», where it has one, is a separate italic line). This is the one place the
    /// default is set for box and lifeline-header names, so every strategy that emits them inherits
    /// it; a style a strategy requests explicitly (such as the italic name of an abstract definition)
    /// is kept. Browser rows (one combined label) are boxes and get the default; accept/send nodes
    /// have no keyword line; grid header cells are not boxes and are not processed (they stay bold).
    /// </remarks>
    /// <param name="tree">The laid-out tree.</param>
    /// <returns>The tree with the default name style applied to every box and lifeline header.</returns>
    private static LayoutTree ApplyNameStyleDefault(LayoutTree tree) =>
        tree with { Nodes = [.. tree.Nodes.Select(ApplyNameStyleDefault)] };

    /// <summary>Applies the default name style to one node and, for a box, to all of its descendants.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The node with the default name style applied.</returns>
    private static LayoutNode ApplyNameStyleDefault(LayoutNode node) => node switch
    {
        LayoutBox box => box with
        {
            Appearance = WithRegularName(box.Appearance),
            Children = [.. box.Children.Select(ApplyNameStyleDefault)],
        },
        LayoutLifeline lifeline => lifeline with { HeaderAppearance = WithRegularName(lifeline.HeaderAppearance) },
        _ => node,
    };

    /// <summary>Returns the appearance with a regular, upright name when no name style was requested.</summary>
    /// <param name="appearance">The requested appearance, or <see langword="null"/>.</param>
    /// <returns>The appearance with its name style resolved.</returns>
    private static BoxAppearance WithRegularName(BoxAppearance? appearance) =>
        (appearance ?? new BoxAppearance()) with { NameStyle = appearance?.NameStyle ?? TextStyle.Regular };
}

