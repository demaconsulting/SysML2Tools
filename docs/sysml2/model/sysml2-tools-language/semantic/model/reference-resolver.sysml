package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def ReferenceResolver {
        doc /* Resolves supertype, typing, metadata-type, redefinition, import, satisfy, verify, allocate, expose, dependency, connect, binding, and transition references by KerML 8.2.3.5 name resolution, delegating to NameResolver, and returns a SemanticIndex of the resolved edges. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/ReferenceResolver.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/WorkspaceLoaderTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/SemanticOmgModelsTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Semantic/KermlLibraryIndexingTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/reference-resolver.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/reference-resolver.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/reference-resolver.yaml");
        }

        // Resolution walks the node hierarchy against the symbol table through the name resolver, evaluates filtered
        // imports, and records every resolved reference as an edge in the semantic index.
        dependency ReferenceResolver to SymbolTable;
        dependency ReferenceResolver to NameResolver;
        dependency ReferenceResolver to SysmlNode;
        dependency ReferenceResolver to SysmlEdge;
        dependency ReferenceResolver to SemanticIndex;
        dependency ReferenceResolver to FilterExpressionEvaluator;
    }
}
