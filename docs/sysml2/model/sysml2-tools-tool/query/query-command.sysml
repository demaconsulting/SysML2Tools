package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def QueryCommand {
        doc /* Resolves glob patterns, loads the workspace with the standard-library seed, and dispatches to the query engine, renderer, and exporter (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Query/QueryCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Query/QueryCliArgumentParser.cs", "src/DemaConsulting.SysML2Tools.Tool/Query/QueryStrings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QuerySubsystemTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QueryVerbsTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QueryErrorPathTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/query.yaml");
        }

        // Querying loads the workspace, executes the verb through the core query API, and renders or exports the
        // uniform result.
        dependency QueryCommand to Context;
        dependency QueryCommand to DiagnosticFormatter;
        dependency QueryCommand to SysML2ToolsCore::QueryEngine;
        dependency QueryCommand to SysML2ToolsCore::QueryResultRenderer;
        dependency QueryCommand to SysML2ToolsCore::QueryResultExporter;
        dependency QueryCommand to SysML2ToolsCore::GlobFileCollector;
        dependency QueryCommand to SysML2ToolsStdlib::StdlibProvider;
        dependency QueryCommand to SysML2ToolsLanguage::WorkspaceLoader;
    }
}
