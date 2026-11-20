package SysML2ToolsLanguage_AstDeserializer {
    private import SysML2ToolsLanguage_AstSerializerContext::*;
    private import SysML2ToolsLanguage_SerializedStdlib::*;
    private import SysML2ToolsLanguage_SymbolTable::*;
    private import SysML2ToolsLanguage_SysmlNode::*;
    private import SoftwareItemMetadata::*;

    part def AstDeserializer {
        doc /* Deserializes UTF-8 JSON bytes back into a SymbolTable and its diagnostics. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/AstDeserializer.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/AstSerializerTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/ast-deserializer.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/ast-deserializer.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/ast-deserializer.yaml");
        }

        // Deserialization restores the node hierarchy into a symbol table through the source-generated JSON context.
        dependency AstDeserializer to SymbolTable;
        dependency AstDeserializer to SysmlNode;
        dependency AstDeserializer to SerializedStdlib;
        dependency AstDeserializer to AstSerializerContext;
    }
}
