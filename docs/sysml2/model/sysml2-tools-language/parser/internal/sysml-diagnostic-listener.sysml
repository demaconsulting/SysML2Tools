package SysML2ToolsLanguage_SysmlDiagnosticListener {
    private import SoftwareItemMetadata::*;

    part def SysmlDiagnosticListener {
        doc /* Collects ANTLR4 lexer and parser syntax errors as SysmlDiagnostic records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Parser/Internal/SysmlDiagnosticListener.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Parser/WorkspaceParserTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/parser/internal/sysml-diagnostic-listener.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/parser/internal/sysml-diagnostic-listener.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/parser/internal/sysml-diagnostic-listener.yaml");
        }

        // The listener implements the ANTLR4 error-listener interfaces.
        dependency SysmlDiagnosticListener to OtsDependencies::Antlr4;
    }
}
