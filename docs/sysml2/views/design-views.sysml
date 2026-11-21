// Views rendered into docs/design/generated/ for the Design document.
//
// Every view is designed to stay readable at roughly 900-1200 pixels of document width. A general
// view draws an exposed part def together with its owned part usages as diamond-linked part nodes
// (a part tree), so exposing every system, subsystem, and unit def at once would draw one
// composition forest (the previous overview measured about 11577x2380 pixels). Each view therefore
// exposes one level of the hierarchy and a bounded number of dependency edges:
//   - SoftwareStructureView: an interconnection view of the four system defs; each system frame
//     nests its direct subsystem part boxes (one level, no unit trees).
//   - OtsView: the shared OTS package as a package frame of part defs.
//   - {System}View: the system def plus its direct subsystems as part nodes. The Tool system owns
//     nine parts, which as one tree row is too wide, so it is an interconnection view whose part
//     boxes wrap into rows.
//   - {Subsystem}View: for a subsystem with a few units, the subsystem def as the tree root with
//     its unit part usages as part nodes (diamond edges), the unit defs, and the suppliers of the
//     units' 'dependency' statements, so the dependency edges (dashed, labeled depend) appear. A
//     nested subsystem is one part node (its def is not exposed). A large subsystem (Core Query,
//     Core Layout Internal, Language Semantic Model) is an interconnection view of its units, and
//     its dependencies are split by theme into focused views that leave the subsystem def out.
//     The Language Semantic subsystem is split the same way.
// Hub dependencies are omitted from the unit-level views: a supplier that many units depend on
// (DemaRendering, SysmlNode, SysmlEdge, LayoutWarnings, ExposeScopeResolver, StdlibProvider,
// Context, GlobFileCollector, Antlr4, and the Program entry point) turns every view into a tangle of edges to one box. Each
// affected view says so in its doc comment. The users of a hub are listed by 'sysml2tools query
// used-by --element {Hub}', and CoreLayoutInternalSharedView shows the layout hubs once.
// Each view stays at roughly 15 dependency edges or fewer so that every edge can be followed.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a 'view def'
// definition), and it - not the view definition - is what scopes the rendered diagram content.
// View names are a flat namespace (render writes {ViewName}.svg), so subsystem views are
// prefixed with their system's short name to keep the three Internal and two Query
// subsystems distinct.
package SysML2ToolsDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : InterconnectionView {
        doc /* The four systems, each framed around its direct subsystem part boxes (one level; units appear in the subsystem views). */
        expose SysML2ToolsLanguage::SysML2ToolsLanguageSystem;
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
        filter not (@SysML::Documentation);
    }

    view OtsView : GeneralView {
        doc /* The shared off-the-shelf (OTS) items. */
        expose OtsDependencies::*;
        filter not (@SysML::Documentation);
    }

    view LanguageView : GeneralView {
        doc /* The Language system with its direct subsystems as part nodes. */
        expose SysML2ToolsLanguage::SysML2ToolsLanguageSystem;
        filter not (@SysML::Documentation);
    }

    view StdlibView : GeneralView {
        doc /* The Stdlib system with its provider unit as a part node. */
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
        filter not (@SysML::Documentation);
    }

    view CoreView : GeneralView {
        doc /* The Core system with its direct subsystems as part nodes. */
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
        filter not (@SysML::Documentation);
    }

    view ToolView : InterconnectionView {
        doc /* The Tool system with its nine direct parts as part boxes (an interconnection view, because nine part nodes in one tree row are too wide). */
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
    }

    view LanguageParserView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. A nested subsystem is a single part node. Hub dependencies (Antlr4) are omitted; query used-by lists their users. */
        expose SysML2ToolsLanguage_ParserSubsystem::ParserSubsystem;
        expose SysML2ToolsLanguage_WorkspaceParser::WorkspaceParser;
        expose SysML2ToolsLanguage_SysmlDiagnosticListener::SysmlDiagnosticListener;
        filter not (@SysML::Documentation);
    }

    view LanguageParserInternalView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Antlr4) are omitted; query used-by lists their users. */
        expose SysML2ToolsLanguage_ParserInternalSubsystem::ParserInternalSubsystem;
        expose SysML2ToolsLanguage_SysmlDiagnosticListener::SysmlDiagnosticListener;
        filter not (@SysML::Documentation);
    }

    view LanguageSemanticView : GeneralView {
        doc /* The semantic subsystem with its units and the model subsystem as part nodes. Dependencies are in LanguageSemanticLoadingView and LanguageSemanticSerializationView. */
        expose SysML2ToolsLanguage_SemanticSubsystem::SemanticSubsystem;
        filter not (@SysML::Documentation);
    }

    view LanguageSemanticModelView : InterconnectionView {
        doc /* The semantic model subsystem with its twelve units as part boxes. Dependencies are in LanguageSemanticModelResolutionView and LanguageSemanticModelConstructionView. */
        expose SysML2ToolsLanguage_ModelSubsystem::ModelSubsystem;
    }

    view LanguageFilteringView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Antlr4, SysmlEdge, SysmlNode) are omitted; query used-by lists their users. */
        expose SysML2ToolsLanguage_FilteringSubsystem::FilteringSubsystem;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        expose SysML2ToolsLanguage_SysmlMetadataNode::SysmlMetadataNode;
        filter not (@SysML::Documentation);
    }

    view CoreLayoutView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. A nested subsystem is a single part node. */
        expose SysML2ToolsCore_LayoutSubsystem::LayoutSubsystem;
        filter not (@SysML::Documentation);
    }

    view CoreLayoutInternalView : InterconnectionView {
        doc /* The layout internal subsystem with its ten units as part boxes. Dependencies are in CoreLayoutInternalStrategyView and CoreLayoutInternalSharedView. */
        expose SysML2ToolsCore_LayoutInternalSubsystem::LayoutInternalSubsystem;
    }

    view CoreRenderingView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. A nested subsystem is a single part node. Hub dependencies (DemaRendering, LayoutWarnings) are omitted; query used-by lists their users. */
        expose SysML2ToolsCore_RenderingSubsystem::RenderingSubsystem;
        expose SysML2ToolsCore_DiagramRenderer::DiagramRenderer;
        expose SysML2ToolsCore_DiagramTypeRouter::DiagramTypeRouter;
        expose SysML2ToolsCore_DynamicViewSynthesizer::DynamicViewSynthesizer;
        filter not (@SysML::Documentation);
    }

    view CoreRenderingInternalView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. The router depends on all seven layout strategies (one per view kind); those edges are omitted, see CoreLayoutInternalStrategyView. */
        expose SysML2ToolsCore_RenderingInternalSubsystem::RenderingInternalSubsystem;
        expose SysML2ToolsCore_DiagramTypeRouter::DiagramTypeRouter;
        expose SysML2ToolsCore_DynamicViewSynthesizer::DynamicViewSynthesizer;
        filter not (@SysML::Documentation);
    }

    view CoreIoView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. */
        expose SysML2ToolsCore_IoSubsystem::IoSubsystem;
        expose SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        filter not (@SysML::Documentation);
    }

    view CoreQueryView : InterconnectionView {
        doc /* The query subsystem with its nine units as part boxes. Dependencies are in CoreQueryEngineView and CoreQueryOutputView. */
        expose SysML2ToolsCore_QuerySubsystem::QuerySubsystem;
    }

    view ToolCliView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. */
        expose SysML2ToolsTool_CliSubsystem::CliSubsystem;
        expose SysML2ToolsTool_Context::Context;
        filter not (@SysML::Documentation);
    }

    view ToolLintView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Context, GlobFileCollector, StdlibProvider) are omitted; query used-by lists their users. */
        expose SysML2ToolsTool_LintSubsystem::LintSubsystem;
        expose SysML2ToolsTool_LintCommand::LintCommand;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view ToolRenderView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Context, DemaRendering, GlobFileCollector, StdlibProvider) are omitted; query used-by lists their users. */
        expose SysML2ToolsTool_RenderSubsystem::RenderSubsystem;
        expose SysML2ToolsTool_RenderCommand::RenderCommand;
        expose SysML2ToolsCore_DiagramRenderer::DiagramRenderer;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view ToolHelpView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Context, Program) are omitted; query used-by lists their users. */
        expose SysML2ToolsTool_HelpSubsystem::HelpSubsystem;
        expose SysML2ToolsTool_HelpCommand::HelpCommand;
        expose SysML2ToolsTool_LintCommand::LintCommand;
        expose SysML2ToolsTool_QueryCommand::QueryCommand;
        expose SysML2ToolsTool_RenderCommand::RenderCommand;
        filter not (@SysML::Documentation);
    }

    view ToolQueryView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Context, GlobFileCollector, StdlibProvider, SysmlNode) are omitted; query used-by lists their users. */
        expose SysML2ToolsTool_QuerySubsystem::QuerySubsystem;
        expose SysML2ToolsTool_QueryCommand::QueryCommand;
        expose SysML2ToolsCore_QueryEngine::QueryEngine;
        expose SysML2ToolsCore_QueryResultExporter::QueryResultExporter;
        expose SysML2ToolsCore_QueryResultRenderer::QueryResultRenderer;
        expose SysML2ToolsCore_QueryVerb::QueryVerb;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view ToolExportView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Context, GlobFileCollector, StdlibProvider, SysmlEdge, SysmlNode) are omitted; query used-by lists their users. */
        expose SysML2ToolsTool_ExportSubsystem::ExportSubsystem;
        expose SysML2ToolsTool_ExportCommand::ExportCommand;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        filter not (@SysML::Documentation);
    }

    view ToolSelfTestView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. Hub dependencies (Context, Program) are omitted; query used-by lists their users. */
        expose SysML2ToolsTool_SelfTestSubsystem::SelfTestSubsystem;
        expose SysML2ToolsTool_Validation::Validation;
        expose SysML2ToolsTool_PathHelpers::PathHelpers;
        filter not (@SysML::Documentation);
    }

    view ToolUtilitiesView : GeneralView {
        doc /* The subsystem def with its unit part nodes, the unit defs, and the suppliers of their dependency statements. */
        expose SysML2ToolsTool_UtilitiesSubsystem::UtilitiesSubsystem;
        expose SysML2ToolsTool_PathHelpers::PathHelpers;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view CoreLayoutInternalStrategyView : GeneralView {
        doc /* Dependencies among the seven layout strategies: five are built on the general view strategy, three use the layered placement helper, and the grid strategy has only hub dependencies, so it stands alone. The subsystem def is left out (see CoreLayoutInternalView). Hub dependencies (DemaRendering, LayoutWarnings, ExposeScopeResolver) are omitted; see CoreLayoutInternalSharedView. */
        expose SysML2ToolsCore_GeneralViewLayoutStrategy::GeneralViewLayoutStrategy;
        expose SysML2ToolsCore_InterconnectionViewLayoutStrategy::InterconnectionViewLayoutStrategy;
        expose SysML2ToolsCore_StateTransitionViewLayoutStrategy::StateTransitionViewLayoutStrategy;
        expose SysML2ToolsCore_ActionFlowViewLayoutStrategy::ActionFlowViewLayoutStrategy;
        expose SysML2ToolsCore_SequenceViewLayoutStrategy::SequenceViewLayoutStrategy;
        expose SysML2ToolsCore_BrowserViewLayoutStrategy::BrowserViewLayoutStrategy;
        expose SysML2ToolsCore_GridViewLayoutStrategy::GridViewLayoutStrategy;
        expose SysML2ToolsCore_LayeredPlacement::LayeredPlacement;
        filter @SysML::PartDefinition;
    }

    view CoreLayoutInternalSharedView : GeneralView {
        doc /* The shared layout infrastructure and its suppliers, shown through the general view strategy. The other strategies use ExposeScopeResolver, LayoutWarnings, and DemaRendering the same way (query used-by lists them), so those hub dependencies are omitted from the strategy and unit views. */
        expose SysML2ToolsCore_GeneralViewLayoutStrategy::GeneralViewLayoutStrategy;
        expose SysML2ToolsCore_ExposeScopeResolver::ExposeScopeResolver;
        expose SysML2ToolsCore_LayoutWarnings::LayoutWarnings;
        expose SysML2ToolsCore_LayeredPlacement::LayeredPlacement;
        expose SysML2ToolsLanguage_NameResolver::NameResolver;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        expose SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        expose OtsDependencies::DemaRendering;
        filter @SysML::PartDefinition;
    }

    view CoreQueryEngineView : GeneralView {
        doc /* The query input side: the argument parser, the options, and the engine with its data types and the semantic index. Hub dependencies (SysmlNode, SysmlEdge) are omitted. */
        expose SysML2ToolsCore_QueryArgumentParser::QueryArgumentParser;
        expose SysML2ToolsCore_QueryOptions::QueryOptions;
        expose SysML2ToolsCore_QueryVerb::QueryVerb;
        expose SysML2ToolsCore_QueryEngine::QueryEngine;
        expose SysML2ToolsCore_QueryResult::QueryResult;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        filter @SysML::PartDefinition;
    }

    view CoreQueryOutputView : GeneralView {
        doc /* The query output side: the renderer, the exporter, and the helpers that turn a QueryResult into Markdown, JSON, or a file. */
        expose SysML2ToolsCore_QueryResult::QueryResult;
        expose SysML2ToolsCore_QueryResultRenderer::QueryResultRenderer;
        expose SysML2ToolsCore_QueryResultSerializerContext::QueryResultSerializerContext;
        expose SysML2ToolsCore_QualifiedNameShortener::QualifiedNameShortener;
        expose SysML2ToolsCore_QueryResultExporter::QueryResultExporter;
        filter @SysML::PartDefinition;
    }

    view LanguageSemanticModelResolutionView : GeneralView {
        doc /* Reference resolution inside the semantic model: the resolver, the symbol table, the name resolver, the supertype walker, and the semantic index. Hub dependencies (SysmlNode, SysmlEdge) are omitted. */
        expose SysML2ToolsLanguage_ReferenceResolver::ReferenceResolver;
        expose SysML2ToolsLanguage_SymbolTable::SymbolTable;
        expose SysML2ToolsLanguage_NameResolver::NameResolver;
        expose SysML2ToolsLanguage_SupertypeWalker::SupertypeWalker;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        filter @SysML::PartDefinition;
    }

    view LanguageSemanticModelConstructionView : GeneralView {
        doc /* Construction of the semantic model: what the AST builder produces and what the serializer context holds. Hub dependencies (SysmlNode, Antlr4) are omitted. */
        expose SysML2ToolsLanguage_AstBuilder::AstBuilder;
        expose SysML2ToolsLanguage_SysmlMetadataNode::SysmlMetadataNode;
        expose SysML2ToolsLanguage_SysmlAnnotation::SysmlAnnotation;
        expose SysML2ToolsLanguage_AstSerializerContext::AstSerializerContext;
        expose SysML2ToolsLanguage_SerializedStdlib::SerializedStdlib;
        filter @SysML::PartDefinition;
    }

    view LanguageSemanticLoadingView : GeneralView {
        doc /* Workspace loading: the loader and the parser, builder, resolver, walker, symbol table, and index it drives. It has no hub dependencies to omit. */
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsLanguage_WorkspaceParser::WorkspaceParser;
        expose SysML2ToolsLanguage_AstBuilder::AstBuilder;
        expose SysML2ToolsLanguage_SymbolTable::SymbolTable;
        expose SysML2ToolsLanguage_ReferenceResolver::ReferenceResolver;
        expose SysML2ToolsLanguage_SupertypeWalker::SupertypeWalker;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        filter @SysML::PartDefinition;
    }

    view LanguageSemanticSerializationView : GeneralView {
        doc /* Serialization: the serializer and the deserializer with the shared serializer context, serialized stdlib, and symbol table. Hub dependencies (SysmlNode) are omitted. */
        expose SysML2ToolsLanguage_AstSerializer::AstSerializer;
        expose SysML2ToolsLanguage_AstDeserializer::AstDeserializer;
        expose SysML2ToolsLanguage_AstSerializerContext::AstSerializerContext;
        expose SysML2ToolsLanguage_SerializedStdlib::SerializedStdlib;
        expose SysML2ToolsLanguage_SymbolTable::SymbolTable;
        filter @SysML::PartDefinition;
    }
}
