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
        expose SysML2ToolsLanguage::ParserSubsystem;
        expose SysML2ToolsLanguage::ParserInternalSubsystem;
        expose SysML2ToolsLanguage::SemanticSubsystem;
        expose SysML2ToolsLanguage::ModelSubsystem;
        expose SysML2ToolsLanguage::FilteringSubsystem;
        expose SysML2ToolsStdlib::SysML2ToolsStdlibSystem;
        expose SysML2ToolsCore::SysML2ToolsCoreSystem;
        expose SysML2ToolsCore::LayoutSubsystem;
        expose SysML2ToolsCore::LayoutInternalSubsystem;
        expose SysML2ToolsCore::RenderingSubsystem;
        expose SysML2ToolsCore::RenderingInternalSubsystem;
        expose SysML2ToolsCore::IoSubsystem;
        expose SysML2ToolsCore::QuerySubsystem;
        expose SysML2ToolsTool::SysML2ToolsToolSystem;
        expose SysML2ToolsTool::CliSubsystem;
        expose SysML2ToolsTool::LintSubsystem;
        expose SysML2ToolsTool::RenderSubsystem;
        expose SysML2ToolsTool::HelpSubsystem;
        expose SysML2ToolsTool::QuerySubsystem;
        expose SysML2ToolsTool::ExportSubsystem;
        expose SysML2ToolsTool::SelfTestSubsystem;
        expose SysML2ToolsTool::UtilitiesSubsystem;
        expose OtsDependencies::**;
        filter not @SysML::Documentation;
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
        expose SysML2ToolsLanguage::ParserSubsystem;
    }

    view LanguageParserInternalView {
        expose SysML2ToolsLanguage::ParserInternalSubsystem;
    }

    view LanguageSemanticView {
        expose SysML2ToolsLanguage::SemanticSubsystem;
    }

    view LanguageSemanticModelView {
        expose SysML2ToolsLanguage::ModelSubsystem;
    }

    view LanguageFilteringView {
        expose SysML2ToolsLanguage::FilteringSubsystem;
    }

    view CoreLayoutView {
        expose SysML2ToolsCore::LayoutSubsystem;
    }

    view CoreLayoutInternalView {
        expose SysML2ToolsCore::LayoutInternalSubsystem;
    }

    view CoreRenderingView {
        expose SysML2ToolsCore::RenderingSubsystem;
    }

    view CoreRenderingInternalView {
        expose SysML2ToolsCore::RenderingInternalSubsystem;
    }

    view CoreIoView {
        expose SysML2ToolsCore::IoSubsystem;
    }

    view CoreQueryView {
        expose SysML2ToolsCore::QuerySubsystem;
    }

    view ToolCliView {
        expose SysML2ToolsTool::CliSubsystem;
    }

    view ToolLintView {
        expose SysML2ToolsTool::LintSubsystem;
    }

    view ToolRenderView {
        expose SysML2ToolsTool::RenderSubsystem;
    }

    view ToolHelpView {
        expose SysML2ToolsTool::HelpSubsystem;
    }

    view ToolQueryView {
        expose SysML2ToolsTool::QuerySubsystem;
    }

    view ToolExportView {
        expose SysML2ToolsTool::ExportSubsystem;
    }

    view ToolSelfTestView {
        expose SysML2ToolsTool::SelfTestSubsystem;
    }

    view ToolUtilitiesView {
        expose SysML2ToolsTool::UtilitiesSubsystem;
    }
}
