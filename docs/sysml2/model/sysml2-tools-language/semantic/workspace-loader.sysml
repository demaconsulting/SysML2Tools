package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def WorkspaceLoader {
        doc /* Public API that loads SysML/KerML files into a semantic workspace, with an optional pre-resolved seed symbol table. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/WorkspaceLoader.cs", "src/DemaConsulting.SysML2Tools.Language/Semantic/SysmlWorkspace.cs", "src/DemaConsulting.SysML2Tools.Language/Semantic/SysmlLoadResult.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/WorkspaceLoaderTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/SemanticOmgModelsTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/NameResolverTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/KermlLibraryIndexingTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/AstBodyCoverageTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/workspace-loader.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/workspace-loader.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/workspace-loader.yaml");
        }

        // Loading orchestrates the whole pipeline: parse each file, build the AST, register symbols, resolve
        // references, and walk supertypes.
        dependency WorkspaceLoader to WorkspaceParser;
        dependency WorkspaceLoader to AstBuilder;
        dependency WorkspaceLoader to SymbolTable;
        dependency WorkspaceLoader to ReferenceResolver;
        dependency WorkspaceLoader to SupertypeWalker;
        dependency WorkspaceLoader to SemanticIndex;
    }
}
