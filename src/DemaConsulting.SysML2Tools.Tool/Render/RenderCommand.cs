// <copyright file="RenderCommand.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Skia;
using DemaConsulting.Rendering.Svg;
using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Stdlib;

namespace DemaConsulting.SysML2Tools.Render;

/// <summary>
/// Implements the <c>render</c> command: loads a SysML workspace, renders all view
/// declarations using the selected format renderer, and writes the output files to disk.
/// </summary>
/// <remarks>
/// The render command is the primary user-facing entry point for Phase 4 diagram generation.
/// It delegates workspace loading to <see cref="WorkspaceLoader"/>, format selection to
/// a simple string comparison on the <c>--format</c> option, and rendering to
/// <see cref="DiagramRenderer"/>. Output files are written to the directory specified by
/// <c>--output</c> (defaulting to the current working directory).
/// </remarks>
internal static class RenderCommand
{
    /// <summary>
    /// Runs the render command using the supplied context.
    /// </summary>
    /// <param name="context">The context providing file globs, format, and output directory.</param>
    /// <returns>A task that completes when all files have been rendered and written.</returns>
    public static async Task RunAsync(Context context)
    {
        var options = context.Render
                       ?? throw new ArgumentException("render: no render options were parsed.", nameof(context));

        // Validate that at least one file pattern was supplied
        if (options.Files.Count == 0)
        {
            context.WriteError("render: no input files specified. Provide file glob patterns.");
            return;
        }

        // Load the workspace from the supplied file patterns
        context.WriteLine($"Loading {options.Files.Count} file pattern(s)...");
        var (stdlibTable, _) = StdlibProvider.GetSymbolTable();
        var loadResult = await WorkspaceLoader.LoadAsync(options.Files, stdlibTable).ConfigureAwait(false);

        // Report any diagnostics from the load phase
        foreach (var diagnostic in loadResult.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                context.WriteError($"  {diagnostic}");
            }
            else
            {
                context.WriteLine($"  {diagnostic}");
            }
        }

        // Skip rendering when workspace loading failed entirely
        if (loadResult.Workspace is null)
        {
            context.WriteError("render: workspace loading failed; no output files written.");
            return;
        }

        // Enumerate renderable views. By default (no --view) every declared view is rendered,
        // supporting bulk "render everything" exports for CI/design-doc publishing; --view narrows
        // the run to a single named view.
        var viewNames = DiagramRenderer.GetViewNames(loadResult.Workspace);
        if (options.ViewName is not null && !viewNames.Contains(options.ViewName, StringComparer.Ordinal))
        {
            var available = string.Join(", ", viewNames);
            context.WriteError(
                $"error: view '{options.ViewName}' not found; use --view to select one (available: {available})");
            return;
        }

        // When --auto is requested and no user-defined views exist, synthesize a GeneralView
        // targeting the most representative top-level element in the workspace
        if (viewNames.Count == 0 && options.AutoView)
        {
            var autoView = DiagramRenderer.SynthesizeAutoView(loadResult.Workspace);
            if (autoView != null)
            {
                context.WriteLine($"  Auto-generating view for '{autoView.Name}'...");

                // Inject the synthetic view node into the workspace so the rendering pipeline
                // discovers it via the normal declaration-iteration path in RenderWorkspace
                loadResult.Workspace.AddDeclaration(autoView.QualifiedName!, autoView);
            }
        }

        // Select the renderer based on the format option (default: svg); reject anything else up
        // front, before doing any rendering work, mirroring the query command's --format handling.
        var format = options.Format ?? "svg";
        if (!format.Equals("svg", StringComparison.OrdinalIgnoreCase) &&
            !format.Equals("png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"render: unsupported --format value '{format}'. Valid values are: svg, png.",
                nameof(context));
        }

        IRenderer renderer = format.Equals("png", StringComparison.OrdinalIgnoreCase)
            ? new PngRenderer()
            : new SvgRenderer();

        // Render all views in the workspace (or the selected view when --view is specified)
        var diagramRenderer = new DiagramRenderer();
        var renderOptions = new RenderOptions(Themes.Light, DepthLimit: context.MaxRenderDepth ?? 0);
        var outputs = diagramRenderer.RenderWorkspace(
            loadResult.Workspace, renderer, renderOptions, viewFilter: options.ViewName);

        if (outputs.Count == 0)
        {
            context.WriteLine("No view declarations found in the workspace; no output files written.");
            return;
        }

        // When rendering every view (no --view filter) with more than one output, guard against
        // two views whose display names sanitize to the same output file name — without this
        // check, the second file written below would silently overwrite the first while the
        // final "Rendered N view(s)." message still reports both as rendered.
        if (options.ViewName is null && outputs.Count > 1 &&
            ReportFileNameCollisions(context, loadResult.Workspace, outputs))
        {
            return;
        }

        // Determine the output directory (default: current directory)
        var outputDir = options.OutputDirectory ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDir);

        // Write each render output to disk
        foreach (var output in outputs)
        {
            var filePath = Path.Combine(outputDir, output.SuggestedFileName);
            context.WriteLine($"  Writing {filePath}");
            await using var fileStream = File.Create(filePath);
            await output.Data.CopyToAsync(fileStream).ConfigureAwait(false);

            // Surface any layout-quality warnings for this view.
            foreach (var warning in output.Warnings)
            {
                context.WriteLine($"  warning: {warning}");
            }
        }

        context.WriteLine($"Rendered {outputs.Count} view(s).");
    }

    /// <summary>
    /// Detects and reports output file name collisions among the given render outputs, sourcing
    /// each output's originating view qualified name from <see cref="DiagramRenderer.GetViewIdentities"/>.
    /// </summary>
    /// <param name="context">The CLI context used to write the error message.</param>
    /// <param name="workspace">The workspace that produced <paramref name="outputs"/>.</param>
    /// <param name="outputs">
    /// The render outputs to check, in the same order that <see cref="DiagramRenderer.GetViewIdentities"/>
    /// enumerates renderable views for this workspace.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when at least one collision was found and reported (the caller
    /// must abort without writing any files); <see langword="false"/> when no collision exists.
    /// </returns>
    /// <remarks>
    /// <see cref="RenderOutput"/> (from the external <c>DemaConsulting.Rendering.Abstractions</c>
    /// package) exposes only <c>SuggestedFileName</c>, <c>MediaType</c>, <c>Data</c>, and
    /// <c>Warnings</c> — it carries no reference back to the originating view — so the qualified
    /// name for each output must be sourced separately, by index, from
    /// <see cref="DiagramRenderer.GetViewIdentities"/>. Both that method and
    /// <see cref="DiagramRenderer.RenderWorkspace"/> apply the identical filter/iteration over
    /// <see cref="SysmlWorkspace.Declarations"/> with no mutation in between (the same
    /// invariant this command already relies on when calling <c>GetViewNames</c> once for
    /// <c>--view</c> validation and <c>RenderWorkspace</c> separately), so the two lists line up
    /// by index.
    /// </remarks>
    private static bool ReportFileNameCollisions(
        Context context,
        SysmlWorkspace workspace,
        IReadOnlyList<RenderOutput> outputs)
    {
        var identities = DiagramRenderer.GetViewIdentities(workspace);
        if (identities.Count != outputs.Count)
        {
            // Defensive: the two enumerations should always line up 1:1 (see remarks); if this
            // invariant is ever broken by a future change, fail loudly rather than mis-attribute
            // qualified names to the wrong output.
            throw new InvalidOperationException(
                $"render: internal error — view identity count ({identities.Count}) does not " +
                $"match render output count ({outputs.Count}).");
        }

        // Group the colliding qualified names by shared output file name
        var groups = identities
            .Select((identity, index) => (identity.QualifiedName, outputs[index].SuggestedFileName))
            .GroupBy(entry => entry.SuggestedFileName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();

        if (groups.Count == 0)
        {
            return false;
        }

        // Report every colliding group before aborting, so users see all collisions at once
        foreach (var group in groups)
        {
            var qualifiedNames = string.Join(", ", group.Select(entry => entry.QualifiedName));
            context.WriteError(
                $"render: output file name collision '{group.Key}' between views: {qualifiedNames}. " +
                "Rename one of the views, or use --view to render a single view.");
        }

        return true;
    }

    /// <summary>
    /// Prints help for the <c>render</c> command.
    /// </summary>
    /// <param name="context">The CLI context for output.</param>
    /// <remarks>
    /// The single source of truth for both <c>render --help</c> and <c>help render</c> — see
    /// <see cref="Help.HelpCommand"/> and <c>Program.RunAsync</c>'s command-aware help dispatch.
    /// </remarks>
    public static void PrintHelp(Context context)
    {
        context.WriteLine(RenderStrings.Render_Usage);
        context.WriteLine("");
        context.WriteLine(RenderStrings.Render_Description);
        context.WriteLine("");
        context.WriteLine(RenderStrings.Render_OptionsHeader);
        context.WriteLine(RenderStrings.Render_OptionOutput);
        context.WriteLine(RenderStrings.Render_OptionFormat);
        context.WriteLine(RenderStrings.Render_OptionView);
        context.WriteLine(RenderStrings.Render_OptionAuto);
        context.WriteLine("");
        context.WriteLine(RenderStrings.Render_DepthNote1);
        context.WriteLine(RenderStrings.Render_DepthNote2);
    }
}
