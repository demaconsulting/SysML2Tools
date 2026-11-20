package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def AstSerializer {
        doc /* Serializes a SymbolTable and its diagnostics to UTF-8 JSON bytes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/AstSerializer.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/AstSerializerTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/ast-serializer.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/ast-serializer.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/ast-serializer.yaml");
        }

        // Serialization reads the symbol table and writes the node hierarchy through the source-generated JSON
        // context.
        dependency AstSerializer to SymbolTable;
        dependency AstSerializer to SysmlNode;
        dependency AstSerializer to SerializedStdlib;
        dependency AstSerializer to AstSerializerContext;
    }
}
