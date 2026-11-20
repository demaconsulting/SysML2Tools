package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def DiagramTypeRouter {
        doc /* Selects a layout strategy from a view's resolved kind. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Rendering/Internal/DiagramTypeRouter.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Rendering/DiagramTypeRouterTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/rendering/internal/diagram-type-router.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/rendering/internal/diagram-type-router.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/rendering/internal/diagram-type-router.yaml");
        }

        // The router returns one of the per-view layout strategies.
        dependency DiagramTypeRouter to GeneralViewLayoutStrategy;
        dependency DiagramTypeRouter to InterconnectionViewLayoutStrategy;
        dependency DiagramTypeRouter to StateTransitionViewLayoutStrategy;
        dependency DiagramTypeRouter to ActionFlowViewLayoutStrategy;
        dependency DiagramTypeRouter to SequenceViewLayoutStrategy;
        dependency DiagramTypeRouter to GridViewLayoutStrategy;
        dependency DiagramTypeRouter to BrowserViewLayoutStrategy;
    }
}
