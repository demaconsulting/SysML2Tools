// Views rendered into docs/design/generated/ for the Design document.
//
// Every view is designed to stay readable at roughly 900-1200 pixels of document width. A general
// view draws an exposed part def together with its owned part usages as diamond-linked part nodes
// (a part tree), so exposing every system, subsystem, and unit def at once would draw one
// composition forest (the previous overview measured about 11577x2380 pixels). Each view therefore
// exposes one level of the hierarchy:
//   - SoftwareStructureView: an interconnection view of the four system defs; each system frame
//     nests its direct subsystem part boxes (one level, no unit trees).
//   - OtsView: the shared OTS package as a package frame of part defs.
//   - {System}View: the system def plus its direct subsystems as part nodes. The Tool system owns
//     nine parts, which as one tree row is about 2200 pixels wide, so it is an interconnection view
//     whose part boxes wrap into rows.
//   - {Subsystem}View: the subsystem def with its unit part usages as part nodes, the unit defs, and
//     the suppliers of the units' 'dependency' statements, so the dependency edges (drawn dashed
//     with a depend label) appear. A nested subsystem is drawn as one part node only (its def is not
//     exposed, so its own unit tree is left to its own view). A subsystem owning more than five
//     parts (the Core Query, Core Layout Internal, and Language Semantic Model subsystems) would
//     draw a row of nine to twelve wide part nodes, so those views filter to part definitions: the
//     subsystem def lists its parts in a compartment and the unit defs show their dependencies.
//     The Language Semantic and Core Rendering Internal views do the same: their seven or more
//     supplier defs form a row that, with the part nodes added, is about 1900 to 2400 pixels wide.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a 'view def'
// definition), and it - not the view definition - is what scopes the rendered diagram content.
// View names are a flat namespace (render writes {ViewName}.svg), so subsystem views are
// prefixed with their system's short name to keep the three Internal and two Query
// subsystems distinct.
package SysML2ToolsDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : InterconnectionView {
        expose SysML2ToolsLanguage::SysML2ToolsLanguageSystem;
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
    }

    view OtsView : GeneralView {
        expose OtsDependencies::*;
        filter not (@SysML::Documentation);
    }

    view LanguageView : GeneralView {
        expose SysML2ToolsLanguage::SysML2ToolsLanguageSystem;
        filter not (@SysML::Documentation);
    }

    view StdlibView : GeneralView {
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
        filter not (@SysML::Documentation);
    }

    view CoreView : GeneralView {
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
        filter not (@SysML::Documentation);
    }

    view ToolView : InterconnectionView {
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
    }
    view LanguageParserView : GeneralView {
        expose SysML2ToolsLanguage_ParserSubsystem::ParserSubsystem;
        expose SysML2ToolsLanguage_WorkspaceParser::WorkspaceParser;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::Antlr4;
        expose SysML2ToolsLanguage_SysmlDiagnosticListener::SysmlDiagnosticListener;
        filter not (@SysML::Documentation);
    }

    view LanguageParserInternalView : GeneralView {
        expose SysML2ToolsLanguage_ParserInternalSubsystem::ParserInternalSubsystem;
        expose SysML2ToolsLanguage_SysmlDiagnosticListener::SysmlDiagnosticListener;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::Antlr4;
        filter not (@SysML::Documentation);
    }

    view LanguageSemanticView : GeneralView {
        expose SysML2ToolsLanguage_SemanticSubsystem::SemanticSubsystem;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsLanguage_AstSerializer::AstSerializer;
        expose SysML2ToolsLanguage_AstDeserializer::AstDeserializer;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsLanguage_AstBuilder::AstBuilder;
        expose SysML2ToolsLanguage_AstSerializerContext::AstSerializerContext;
        expose SysML2ToolsLanguage_ReferenceResolver::ReferenceResolver;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        expose SysML2ToolsLanguage_SerializedStdlib::SerializedStdlib;
        expose SysML2ToolsLanguage_SupertypeWalker::SupertypeWalker;
        expose SysML2ToolsLanguage_SymbolTable::SymbolTable;
        expose SysML2ToolsLanguage_SysmlNode::SysmlNode;
        expose SysML2ToolsLanguage_WorkspaceParser::WorkspaceParser;
        filter @SysML::PartDefinition;
    }

    view LanguageSemanticModelView : GeneralView {
        expose SysML2ToolsLanguage_ModelSubsystem::ModelSubsystem;
        expose SysML2ToolsLanguage_SysmlNode::SysmlNode;
        expose SysML2ToolsLanguage_SysmlMetadataNode::SysmlMetadataNode;
        expose SysML2ToolsLanguage_AstBuilder::AstBuilder;
        expose SysML2ToolsLanguage_SymbolTable::SymbolTable;
        expose SysML2ToolsLanguage_ReferenceResolver::ReferenceResolver;
        expose SysML2ToolsLanguage_NameResolver::NameResolver;
        expose SysML2ToolsLanguage_SupertypeWalker::SupertypeWalker;
        expose SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        expose SysML2ToolsLanguage_SysmlAnnotation::SysmlAnnotation;
        expose SysML2ToolsLanguage_SerializedStdlib::SerializedStdlib;
        expose SysML2ToolsLanguage_AstSerializerContext::AstSerializerContext;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::Antlr4;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        filter @SysML::PartDefinition;
    }

    view LanguageFilteringView : GeneralView {
        expose SysML2ToolsLanguage_FilteringSubsystem::FilteringSubsystem;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::Antlr4;
        expose SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        expose SysML2ToolsLanguage_SysmlMetadataNode::SysmlMetadataNode;
        expose SysML2ToolsLanguage_SysmlNode::SysmlNode;
        filter not (@SysML::Documentation);
    }

    view CoreLayoutView : GeneralView {
        expose SysML2ToolsCore_LayoutSubsystem::LayoutSubsystem;
        filter not (@SysML::Documentation);
    }

    view CoreLayoutInternalView : GeneralView {
        expose SysML2ToolsCore_LayoutInternalSubsystem::LayoutInternalSubsystem;
        expose SysML2ToolsCore_GeneralViewLayoutStrategy::GeneralViewLayoutStrategy;
        expose SysML2ToolsCore_InterconnectionViewLayoutStrategy::InterconnectionViewLayoutStrategy;
        expose SysML2ToolsCore_StateTransitionViewLayoutStrategy::StateTransitionViewLayoutStrategy;
        expose SysML2ToolsCore_ActionFlowViewLayoutStrategy::ActionFlowViewLayoutStrategy;
        expose SysML2ToolsCore_SequenceViewLayoutStrategy::SequenceViewLayoutStrategy;
        expose SysML2ToolsCore_GridViewLayoutStrategy::GridViewLayoutStrategy;
        expose SysML2ToolsCore_BrowserViewLayoutStrategy::BrowserViewLayoutStrategy;
        expose SysML2ToolsCore_LayoutWarnings::LayoutWarnings;
        expose SysML2ToolsCore_ExposeScopeResolver::ExposeScopeResolver;
        expose SysML2ToolsCore_LayeredPlacement::LayeredPlacement;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::DemaRendering;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        expose SysML2ToolsLanguage_NameResolver::NameResolver;
        expose SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        filter @SysML::PartDefinition;
    }

    view CoreRenderingView : GeneralView {
        expose SysML2ToolsCore_RenderingSubsystem::RenderingSubsystem;
        expose SysML2ToolsCore_DiagramRenderer::DiagramRenderer;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::DemaRendering;
        expose SysML2ToolsCore_DiagramTypeRouter::DiagramTypeRouter;
        expose SysML2ToolsCore_DynamicViewSynthesizer::DynamicViewSynthesizer;
        expose SysML2ToolsCore_LayoutWarnings::LayoutWarnings;
        filter not (@SysML::Documentation);
    }

    view CoreRenderingInternalView : GeneralView {
        expose SysML2ToolsCore_RenderingInternalSubsystem::RenderingInternalSubsystem;
        expose SysML2ToolsCore_DiagramTypeRouter::DiagramTypeRouter;
        expose SysML2ToolsCore_DynamicViewSynthesizer::DynamicViewSynthesizer;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsCore_ActionFlowViewLayoutStrategy::ActionFlowViewLayoutStrategy;
        expose SysML2ToolsCore_BrowserViewLayoutStrategy::BrowserViewLayoutStrategy;
        expose SysML2ToolsCore_GeneralViewLayoutStrategy::GeneralViewLayoutStrategy;
        expose SysML2ToolsCore_GridViewLayoutStrategy::GridViewLayoutStrategy;
        expose SysML2ToolsCore_InterconnectionViewLayoutStrategy::InterconnectionViewLayoutStrategy;
        expose SysML2ToolsCore_SequenceViewLayoutStrategy::SequenceViewLayoutStrategy;
        expose SysML2ToolsCore_StateTransitionViewLayoutStrategy::StateTransitionViewLayoutStrategy;
        filter @SysML::PartDefinition;
    }

    view CoreIoView : GeneralView {
        expose SysML2ToolsCore_IoSubsystem::IoSubsystem;
        expose SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        filter not (@SysML::Documentation);
    }

    view CoreQueryView : GeneralView {
        expose SysML2ToolsCore_QuerySubsystem::QuerySubsystem;
        expose SysML2ToolsCore_QueryVerb::QueryVerb;
        expose SysML2ToolsCore_QueryOptions::QueryOptions;
        expose SysML2ToolsCore_QueryArgumentParser::QueryArgumentParser;
        expose SysML2ToolsCore_QueryEngine::QueryEngine;
        expose SysML2ToolsCore_QueryResult::QueryResult;
        expose SysML2ToolsCore_QueryResultRenderer::QueryResultRenderer;
        expose SysML2ToolsCore_QueryResultSerializerContext::QueryResultSerializerContext;
        expose SysML2ToolsCore_QueryResultExporter::QueryResultExporter;
        expose SysML2ToolsCore_QualifiedNameShortener::QualifiedNameShortener;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        expose SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        expose SysML2ToolsLanguage_SysmlNode::SysmlNode;
        filter @SysML::PartDefinition;
    }

    view ToolCliView : GeneralView {
        expose SysML2ToolsTool_CliSubsystem::CliSubsystem;
        expose SysML2ToolsTool_Context::Context;
        filter not (@SysML::Documentation);
    }

    view ToolLintView : GeneralView {
        expose SysML2ToolsTool_LintSubsystem::LintSubsystem;
        expose SysML2ToolsTool_LintCommand::LintCommand;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsStdlib_StdlibProvider::StdlibProvider;
        expose SysML2ToolsTool_Context::Context;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view ToolRenderView : GeneralView {
        expose SysML2ToolsTool_RenderSubsystem::RenderSubsystem;
        expose SysML2ToolsTool_RenderCommand::RenderCommand;
        // Suppliers of the units' dependency statements.
        expose OtsDependencies::DemaRendering;
        expose SysML2ToolsCore_DiagramRenderer::DiagramRenderer;
        expose SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsStdlib_StdlibProvider::StdlibProvider;
        expose SysML2ToolsTool_Context::Context;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view ToolHelpView : GeneralView {
        expose SysML2ToolsTool_HelpSubsystem::HelpSubsystem;
        expose SysML2ToolsTool_HelpCommand::HelpCommand;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsTool_Context::Context;
        expose SysML2ToolsTool_LintCommand::LintCommand;
        expose SysML2ToolsTool_Program::Program;
        expose SysML2ToolsTool_QueryCommand::QueryCommand;
        expose SysML2ToolsTool_RenderCommand::RenderCommand;
        filter not (@SysML::Documentation);
    }

    view ToolQueryView : GeneralView {
        expose SysML2ToolsTool_QuerySubsystem::QuerySubsystem;
        expose SysML2ToolsTool_QueryCommand::QueryCommand;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        expose SysML2ToolsCore_QueryEngine::QueryEngine;
        expose SysML2ToolsCore_QueryResultExporter::QueryResultExporter;
        expose SysML2ToolsCore_QueryResultRenderer::QueryResultRenderer;
        expose SysML2ToolsCore_QueryVerb::QueryVerb;
        expose SysML2ToolsLanguage_SysmlNode::SysmlNode;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsStdlib_StdlibProvider::StdlibProvider;
        expose SysML2ToolsTool_Context::Context;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }

    view ToolExportView : GeneralView {
        expose SysML2ToolsTool_ExportSubsystem::ExportSubsystem;
        expose SysML2ToolsTool_ExportCommand::ExportCommand;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsCore_GlobFileCollector::GlobFileCollector;
        expose SysML2ToolsLanguage_FilterExpressionEvaluator::FilterExpressionEvaluator;
        expose SysML2ToolsLanguage_SemanticIndex::SemanticIndex;
        expose SysML2ToolsLanguage_SysmlEdge::SysmlEdge;
        expose SysML2ToolsLanguage_SysmlNode::SysmlNode;
        expose SysML2ToolsLanguage_WorkspaceLoader::WorkspaceLoader;
        expose SysML2ToolsStdlib_StdlibProvider::StdlibProvider;
        expose SysML2ToolsTool_Context::Context;
        filter not (@SysML::Documentation);
    }

    view ToolSelfTestView : GeneralView {
        expose SysML2ToolsTool_SelfTestSubsystem::SelfTestSubsystem;
        expose SysML2ToolsTool_Validation::Validation;
        // Suppliers of the units' dependency statements.
        expose SysML2ToolsTool_Context::Context;
        expose SysML2ToolsTool_PathHelpers::PathHelpers;
        expose SysML2ToolsTool_Program::Program;
        filter not (@SysML::Documentation);
    }

    view ToolUtilitiesView : GeneralView {
        expose SysML2ToolsTool_UtilitiesSubsystem::UtilitiesSubsystem;
        expose SysML2ToolsTool_PathHelpers::PathHelpers;
        expose SysML2ToolsTool_DiagnosticFormatter::DiagnosticFormatter;
        filter not (@SysML::Documentation);
    }
}
