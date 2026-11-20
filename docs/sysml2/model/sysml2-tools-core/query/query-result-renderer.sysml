package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def QueryResultRenderer {
        doc /* Shared Markdown and JSON rendering and deterministic sorting layer for query results (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryResultRenderer.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Query/QueryRenderingTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        // Rendering formats the result model, serializes JSON through the source-generated context, and shortens
        // qualified names for the dependencies verb.
        dependency QueryResultRenderer to QueryResult;
        dependency QueryResultRenderer to QueryResultSerializerContext;
        dependency QueryResultRenderer to QualifiedNameShortener;
    }
}
