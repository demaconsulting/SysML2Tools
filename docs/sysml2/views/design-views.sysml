// Views rendered into docs/design/generated/ for the Design document.
// The repository contains four systems (one per NuGet package), so the overview is a
// general view, with documentation filtered out to keep the diagram readable. It exposes
// each system and subsystem part def by name (not the packages' unit-level members) plus the
// shared OTS package: exposing every unit as well draws all ~90 units with their dependency
// edges, which is a 5900x5000 pixel diagram that is not readable in a design document. Every
// unit appears in the per-system and per-subsystem views below, and its dependencies are
// consumed with 'sysml2tools query dependencies'. Per SysML2Tools syntax, 'expose' is only
// valid inside a named 'view' usage (not a 'view def' definition), and it - not the view
// definition - is what scopes the rendered diagram content.
// View names are a flat namespace (render writes {ViewName}.svg), so subsystem views are
// prefixed with their system's short name to keep the three Internal and two Query
// subsystems distinct.
package SysML2ToolsDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose SysML2ToolsLanguage::SysML2ToolsLanguageSystem;
        expose SysML2ToolsLanguage_ParserSubsystem::ParserSubsystem;
        expose SysML2ToolsLanguage_ParserInternalSubsystem::ParserInternalSubsystem;
        expose SysML2ToolsLanguage_SemanticSubsystem::SemanticSubsystem;
        expose SysML2ToolsLanguage_ModelSubsystem::ModelSubsystem;
        expose SysML2ToolsLanguage_FilteringSubsystem::FilteringSubsystem;
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
        expose SysML2ToolsCore_LayoutSubsystem::LayoutSubsystem;
        expose SysML2ToolsCore_LayoutInternalSubsystem::LayoutInternalSubsystem;
        expose SysML2ToolsCore_RenderingSubsystem::RenderingSubsystem;
        expose SysML2ToolsCore_RenderingInternalSubsystem::RenderingInternalSubsystem;
        expose SysML2ToolsCore_IoSubsystem::IoSubsystem;
        expose SysML2ToolsCore_QuerySubsystem::QuerySubsystem;
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
        expose SysML2ToolsTool_CliSubsystem::CliSubsystem;
        expose SysML2ToolsTool_LintSubsystem::LintSubsystem;
        expose SysML2ToolsTool_RenderSubsystem::RenderSubsystem;
        expose SysML2ToolsTool_HelpSubsystem::HelpSubsystem;
        expose SysML2ToolsTool_QuerySubsystem::QuerySubsystem;
        expose SysML2ToolsTool_ExportSubsystem::ExportSubsystem;
        expose SysML2ToolsTool_SelfTestSubsystem::SelfTestSubsystem;
        expose SysML2ToolsTool_UtilitiesSubsystem::UtilitiesSubsystem;
        expose OtsDependencies::**;
        filter not (@SysML::Documentation);
    }

    view LanguageView {
        expose SysML2ToolsLanguage::SysML2ToolsLanguageSystem;
    }

    view StdlibView {
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
    }

    view CoreView {
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
    }

    view ToolView {
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
    }

    view LanguageParserView {
        expose SysML2ToolsLanguage_ParserSubsystem::ParserSubsystem;
    }

    view LanguageParserInternalView {
        expose SysML2ToolsLanguage_ParserInternalSubsystem::ParserInternalSubsystem;
    }

    view LanguageSemanticView {
        expose SysML2ToolsLanguage_SemanticSubsystem::SemanticSubsystem;
    }

    view LanguageSemanticModelView {
        expose SysML2ToolsLanguage_ModelSubsystem::ModelSubsystem;
    }

    view LanguageFilteringView {
        expose SysML2ToolsLanguage_FilteringSubsystem::FilteringSubsystem;
    }

    view CoreLayoutView {
        expose SysML2ToolsCore_LayoutSubsystem::LayoutSubsystem;
    }

    view CoreLayoutInternalView {
        expose SysML2ToolsCore_LayoutInternalSubsystem::LayoutInternalSubsystem;
    }

    view CoreRenderingView {
        expose SysML2ToolsCore_RenderingSubsystem::RenderingSubsystem;
    }

    view CoreRenderingInternalView {
        expose SysML2ToolsCore_RenderingInternalSubsystem::RenderingInternalSubsystem;
    }

    view CoreIoView {
        expose SysML2ToolsCore_IoSubsystem::IoSubsystem;
    }

    view CoreQueryView {
        expose SysML2ToolsCore_QuerySubsystem::QuerySubsystem;
    }

    view ToolCliView {
        expose SysML2ToolsTool_CliSubsystem::CliSubsystem;
    }

    view ToolLintView {
        expose SysML2ToolsTool_LintSubsystem::LintSubsystem;
    }

    view ToolRenderView {
        expose SysML2ToolsTool_RenderSubsystem::RenderSubsystem;
    }

    view ToolHelpView {
        expose SysML2ToolsTool_HelpSubsystem::HelpSubsystem;
    }

    view ToolQueryView {
        expose SysML2ToolsTool_QuerySubsystem::QuerySubsystem;
    }

    view ToolExportView {
        expose SysML2ToolsTool_ExportSubsystem::ExportSubsystem;
    }

    view ToolSelfTestView {
        expose SysML2ToolsTool_SelfTestSubsystem::SelfTestSubsystem;
    }

    view ToolUtilitiesView {
        expose SysML2ToolsTool_UtilitiesSubsystem::UtilitiesSubsystem;
    }
}
