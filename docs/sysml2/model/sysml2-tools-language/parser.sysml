package SysML2ToolsLanguage_ParserSubsystem {
    private import SysML2ToolsLanguage_ParserInternalSubsystem::*;
    private import SysML2ToolsLanguage_WorkspaceParser::*;
    private import SoftwareItemMetadata::*;

    part def ParserSubsystem {
        doc /* SysML v2 parsing engine: parses source text into an ANTLR4 concrete syntax tree and collects syntax errors as diagnostics. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-language/parser.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/parser.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/parser.yaml");
        }

        part workspaceParser : WorkspaceParser;
        part internal : ParserInternalSubsystem;
    }
}
