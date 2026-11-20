package SysML2ToolsLanguage_SemanticSubsystem {
    private import SysML2ToolsLanguage_AstDeserializer::*;
    private import SysML2ToolsLanguage_AstSerializer::*;
    private import SysML2ToolsLanguage_ModelSubsystem::*;
    private import SysML2ToolsLanguage_WorkspaceLoader::*;
    private import SoftwareItemMetadata::*;

    part def SemanticSubsystem {
        doc /* SysML/KerML semantic model: symbol table, reference resolution, supertype walking, and AST serialization. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-language/semantic.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic.yaml");
        }

        part workspaceLoader : WorkspaceLoader;
        part astSerializer : AstSerializer;
        part astDeserializer : AstDeserializer;
        part model : ModelSubsystem;
    }
}
