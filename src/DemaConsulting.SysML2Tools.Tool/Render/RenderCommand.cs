// <copyright file="RenderCommand.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.SysML2Tools.Cli;
using DemaConsulting.SysML2Tools.Parser;
using DemaConsulting.SysML2Tools.Png;
using DemaConsulting.SysML2Tools.Rendering;
using DemaConsulting.SysML2Tools.Semantic;
using DemaConsulting.SysML2Tools.Svg;

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
        // Validate that at least one file pattern was supplied
        if (context.Files.Count == 0)
        {
            context.WriteError("render: no input files specified. Provide file glob patterns.");
            return;
        }

        // Load the workspace from the supplied file patterns
        context.WriteLine($"Loading {context.Files.Count} file pattern(s)...");
        var loadResult = await WorkspaceLoader.LoadAsync(context.Files).ConfigureAwait(false);

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

        // Select the renderer based on the format option (default: svg)
        var format = context.RendererFormat ?? "svg";
        IRenderer renderer = format.Equals("png", StringComparison.OrdinalIgnoreCase)
            ? new PngRenderer()
            : new SvgRenderer();

        // Render all views in the workspace
        var diagramRenderer = new DiagramRenderer();
        var options = new RenderOptions(Themes.Light);
        var outputs = diagramRenderer.RenderWorkspace(loadResult.Workspace, renderer, options);

        if (outputs.Count == 0)
        {
            context.WriteLine("No view declarations found in the workspace; no output files written.");
            return;
        }

        // Determine the output directory (default: current directory)
        var outputDir = context.OutputDirectory ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDir);

        // Write each render output to disk
        foreach (var output in outputs)
        {
            var filePath = Path.Combine(outputDir, output.SuggestedFileName);
            context.WriteLine($"  Writing {filePath}");
            await using var fileStream = File.Create(filePath);
            await output.Data.CopyToAsync(fileStream).ConfigureAwait(false);
        }

        context.WriteLine($"Rendered {outputs.Count} view(s).");
    }
}
