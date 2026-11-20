package SysML2ToolsTool_DiagnosticFormatter {
    private import SoftwareItemMetadata::*;

    part def DiagnosticFormatter {
        doc /* Shared path(line,col): severity: message console-line formatter used by the lint, render, and query commands. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Utilities/DiagnosticFormatter.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Utilities/DiagnosticFormatterTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/utilities/diagnostic-formatter.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/utilities/diagnostic-formatter.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/utilities/diagnostic-formatter.yaml");
        }
    }
}
