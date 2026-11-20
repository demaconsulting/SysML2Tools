package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def QueryResultSerializerContext {
        doc /* Source-generated System.Text.Json context used to render query results as JSON (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryResultSerializerContext.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        // The context declares the serializable shape of the result model.
        dependency QueryResultSerializerContext to QueryResult;
    }
}
