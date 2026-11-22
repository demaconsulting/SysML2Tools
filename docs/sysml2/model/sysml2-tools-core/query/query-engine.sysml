package SysML2ToolsCore_QueryEngine {
    private import SysML2ToolsCore_QueryOptions::*;
    private import SysML2ToolsCore_QueryResult::*;
    private import SysML2ToolsCore_QueryVerb::*;
    private import SoftwareItemMetadata::*;

    part def QueryEngine {
        doc /* Public execution surface: the 12 verb methods plus Execute, centralizing the verb switch used by both library callers and the command-line adapter (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryEngine.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Query/QueryEngineElementLookupTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Query/QueryEngineImpactTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Query/QueryOmgFixtureTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        // Every verb reads the semantic workspace's nodes, edges, and index and returns a uniform result.
        dependency QueryEngine to QueryOptions;
        dependency QueryEngine to QueryVerb;
        dependency QueryEngine to QueryResult;
        dependency QueryEngine to SysML2ToolsLanguage_SysmlNode::SysmlNode;
        dependency QueryEngine to SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        dependency QueryEngine to SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        // Element lookup resolves names with the same NameResolver that resolves model references.
        dependency QueryEngine to SysML2ToolsLanguage_NameResolver::NameResolver;
    }
}
