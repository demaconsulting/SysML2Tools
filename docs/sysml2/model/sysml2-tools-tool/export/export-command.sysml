package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def ExportCommand {
        doc /* Resolves glob patterns, loads the workspace with the standard-library seed, and writes the loaded model, its edges, and its diagnostics as faithful JSON (documented in the export subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Export/ExportCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Export/ExportOptions.cs", "src/DemaConsulting.SysML2Tools.Tool/Export/ExportArgumentParser.cs", "src/DemaConsulting.SysML2Tools.Tool/Export/ExportResult.cs", "src/DemaConsulting.SysML2Tools.Tool/Export/ExportResultSerializerContext.cs", "src/DemaConsulting.SysML2Tools.Tool/Export/ExportLineSerializerContext.cs", "src/DemaConsulting.SysML2Tools.Tool/Export/ExportStrings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Export/ExportSubsystemTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Export/ExportRenderingTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/export.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/export.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/export.yaml");
        }

        // Exporting loads the workspace and dumps its nodes, resolved edges, and diagnostics.
        dependency ExportCommand to Context;
        dependency ExportCommand to SysML2ToolsCore::GlobFileCollector;
        dependency ExportCommand to SysML2ToolsStdlib::StdlibProvider;
        dependency ExportCommand to SysML2ToolsLanguage::WorkspaceLoader;
        dependency ExportCommand to SysML2ToolsLanguage::SemanticIndex;
        dependency ExportCommand to SysML2ToolsLanguage::SysmlNode;
        dependency ExportCommand to SysML2ToolsLanguage::SysmlEdge;
    }
}
