package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def DiagramRenderer {
        doc /* High-level rendering orchestrator: for each view it builds a LayoutTree through a layout strategy and renders it with a renderer. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Rendering/DiagramRenderer.cs", "src/DemaConsulting.SysML2Tools.Core/Rendering/ILayoutStrategy.cs", "src/DemaConsulting.SysML2Tools.Core/Rendering/SysmlThemes.cs", "src/DemaConsulting.SysML2Tools.Core/Rendering/Internal/StdlibFilter.cs", "src/DemaConsulting.SysML2Tools.Core/Rendering/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Rendering/RenderIntegrationTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Rendering/RenderingTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/rendering/diagram-renderer.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/rendering/diagram-renderer.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/rendering/diagram-renderer.yaml");
        }

        // Rendering routes each view to a strategy, synthesizes dynamic views, reports empty-view warnings, and hands
        // the layout tree to the off-the-shelf renderers.
        dependency DiagramRenderer to LayoutWarnings;
        dependency DiagramRenderer to DiagramTypeRouter;
        dependency DiagramRenderer to DynamicViewSynthesizer;
        dependency DiagramRenderer to OtsDependencies::DemaRendering;
    }
}
