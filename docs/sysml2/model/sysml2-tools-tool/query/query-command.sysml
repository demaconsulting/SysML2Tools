package SysML2ToolsTool_QueryCommand {
    private import SysML2ToolsTool_Context::*;
    private import SysML2ToolsTool_DiagnosticFormatter::*;
    private import SoftwareItemMetadata::*;

    part def QueryCommand {
        doc /* Resolves glob patterns, loads the workspace with the standard-library seed, and dispatches to the query engine, renderer, and exporter (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Query/QueryCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Query/QueryCliArgumentParser.cs", "src/DemaConsulting.SysML2Tools.Tool/Query/QueryStrings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QuerySubsystemTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QueryVerbsTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QueryErrorPathTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QuerySelfModelTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/query.yaml");
        }

        // Querying loads the workspace, executes the verb through the core query API, and renders or exports the
        // uniform result.
        dependency QueryCommand to Context;
        dependency QueryCommand to DiagnosticFormatter;
        dependency QueryCommand to SysML2ToolsCore_QueryEngine::QueryEngine;
        dependency QueryCommand to SysML2ToolsCore_QueryVerb::QueryVerb;
        dependency QueryCommand to SysML2ToolsCore_QueryResultRenderer::QueryResultRenderer;
        dependency QueryCommand to SysML2ToolsCore_QueryResultExporter::QueryResultExporter;
        dependency QueryCommand to SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        dependency QueryCommand to SysML2ToolsStdlib_StdlibProvider::StdlibProvider;
        dependency QueryCommand to SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        dependency QueryCommand to SysML2ToolsLanguage_SysmlNode::SysmlNode;
    }
}
