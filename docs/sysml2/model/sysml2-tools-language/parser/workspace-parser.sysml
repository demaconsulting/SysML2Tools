package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def WorkspaceParser {
        doc /* Public parsing API: parses source strings and exposes the internal ParseSourceToCst entry point used by the workspace loader. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Parser/WorkspaceParser.cs", "src/DemaConsulting.SysML2Tools.Language/Parser/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Parser/WorkspaceParserTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Parser/OmgModelsTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/parser/workspace-parser.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/parser/workspace-parser.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/parser/workspace-parser.yaml");
        }

        // Parsing runs the ANTLR4 lexer and parser and routes every syntax error through the diagnostic listener.
        dependency WorkspaceParser to SysmlDiagnosticListener;
        dependency WorkspaceParser to OtsDependencies::Antlr4;
    }
}
