package SysML2ToolsLanguage_ParserInternalSubsystem {
    private import SysML2ToolsLanguage_SysmlDiagnosticListener::*;
    private import SoftwareItemMetadata::*;

    part def ParserInternalSubsystem {
        doc /* Internal implementation details of the Parser subsystem (documented in the parser subsystem docs). */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-language/parser.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/parser.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/parser.yaml");
        }

        part sysmlDiagnosticListener : SysmlDiagnosticListener;
    }
}
