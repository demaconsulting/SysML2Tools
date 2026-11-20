package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def RenderCommand {
        doc /* Resolves glob patterns, loads the workspace with the standard-library seed, selects the SVG or PNG renderer, and writes the diagram output files (documented in the render subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Render/RenderCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Render/RenderCommandOptions.cs", "src/DemaConsulting.SysML2Tools.Tool/Render/RenderArgumentParser.cs", "src/DemaConsulting.SysML2Tools.Tool/Render/RenderStrings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Render/RenderSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/render.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/render.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/render.yaml");
        }

        // Rendering loads the workspace, renders each view through the diagram renderer, and writes the output with
        // the off-the-shelf SVG or PNG renderer.
        dependency RenderCommand to Context;
        dependency RenderCommand to DiagnosticFormatter;
        dependency RenderCommand to SysML2ToolsCore::DiagramRenderer;
        dependency RenderCommand to OtsDependencies::DemaRendering;
        dependency RenderCommand to SysML2ToolsCore::GlobFileCollector;
        dependency RenderCommand to SysML2ToolsStdlib::StdlibProvider;
        dependency RenderCommand to SysML2ToolsLanguage::WorkspaceLoader;
    }
}
