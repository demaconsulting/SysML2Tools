package SysML2ToolsLanguage {
    private import SysML2ToolsLanguage_FilteringSubsystem::*;
    private import SysML2ToolsLanguage_ParserSubsystem::*;
    private import SysML2ToolsLanguage_SemanticSubsystem::*;
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    doc /* DemaConsulting.SysML2Tools.Language is the language library: the SysML v2 parsing engine, AST node types, semantic analysis, and AST serialization and deserialization. */

    part def SysML2ToolsLanguageSystem {
        doc /* The Language system as a whole: parser, semantic model, and filtering subsystems. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-language.md");
            verificationFiles = ("docs/verification/sysml2-tools-language.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language.yaml", "docs/reqstream/sysml2-tools-language/platform-requirements.yaml");
        }

        part parser : ParserSubsystem;
        part semantic : SemanticSubsystem;
        part filtering : FilteringSubsystem;
        part antlr4 : Antlr4;
    }
}
