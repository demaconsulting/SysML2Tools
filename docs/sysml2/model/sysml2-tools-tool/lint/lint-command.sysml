package SysML2ToolsTool_LintCommand {
    private import SysML2ToolsTool_Context::*;
    private import SysML2ToolsTool_DiagnosticFormatter::*;
    private import SoftwareItemMetadata::*;

    part def LintCommand {
        doc /* Resolves glob patterns, loads the workspace with the standard-library seed (honoring strict unresolved references), and reports the diagnostics (documented in the lint subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Lint/LintCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Lint/LintOptions.cs", "src/DemaConsulting.SysML2Tools.Tool/Lint/LintArgumentParser.cs", "src/DemaConsulting.SysML2Tools.Tool/Lint/LintStrings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Lint/LintSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/lint.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/lint.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/lint.yaml");
        }

        // Linting reads its options from the context, loads the workspace through the core and language libraries,
        // and formats each diagnostic.
        dependency LintCommand to Context;
        dependency LintCommand to DiagnosticFormatter;
        dependency LintCommand to SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        dependency LintCommand to SysML2ToolsStdlib_StdlibProvider::StdlibProvider;
        dependency LintCommand to SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
    }
}
