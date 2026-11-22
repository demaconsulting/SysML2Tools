package SysML2ToolsCore_QuerySubsystem {
    public import SysML2ToolsCore_QualifiedNameShortener::*;
    public import SysML2ToolsCore_QueryArgumentParser::*;
    public import SysML2ToolsCore_QueryEngine::*;
    public import SysML2ToolsCore_QueryOptions::*;
    public import SysML2ToolsCore_QueryResult::*;
    public import SysML2ToolsCore_QueryResultExporter::*;
    public import SysML2ToolsCore_QueryResultRenderer::*;
    public import SysML2ToolsCore_QueryResultSerializerContext::*;
    public import SysML2ToolsCore_QueryVerb::*;
    private import SoftwareItemMetadata::*;

    part def QuerySubsystem {
        doc /* Public, reusable model-analysis API answering 12 fixed questions over an already-loaded semantic workspace and returning a uniform QueryResult that callers render as Markdown or JSON or write to a file. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        part queryVerb : QueryVerb;
        part queryOptions : QueryOptions;
        part queryArgumentParser : QueryArgumentParser;
        part queryEngine : QueryEngine;
        part queryResult : QueryResult;
        part queryResultRenderer : QueryResultRenderer;
        part queryResultSerializerContext : QueryResultSerializerContext;
        part queryResultExporter : QueryResultExporter;
        part qualifiedNameShortener : QualifiedNameShortener;
    }
}
