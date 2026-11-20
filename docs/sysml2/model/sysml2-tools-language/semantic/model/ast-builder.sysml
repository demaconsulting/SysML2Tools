package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def AstBuilder {
        doc /* Builds the AST from the ANTLR4 concrete syntax tree, with qualified names, metadata annotations, raw view filter text, and the source position of each declaration and reference. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/AstBuilder.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/AstBodyCoverageTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/AstBuilderMetadataTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/ast-builder.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/ast-builder.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/ast-builder.yaml");
        }

        // The builder is an ANTLR4 visitor over the concrete syntax tree that constructs the node hierarchy and its
        // annotations.
        dependency AstBuilder to SysmlNode;
        dependency AstBuilder to SysmlMetadataNode;
        dependency AstBuilder to SysmlAnnotation;
        dependency AstBuilder to OtsDependencies::Antlr4;
    }
}
