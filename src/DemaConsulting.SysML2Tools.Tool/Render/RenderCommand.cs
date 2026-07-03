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

        // Enumerate renderable views; require --view when multiple views are present
        var viewNames = DiagramRenderer.GetViewNames(loadResult.Workspace);
        if (viewNames.Count > 1 && options.ViewName is null)
        {
            var available = string.Join(", ", viewNames);
            context.WriteError(
                $"error: multiple views found; use --view to select one (available: {available})");
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
