package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def ActionFlowViewLayoutStrategy {
        doc /* Action flow view with start and done markers, placed top-to-bottom by the layered algorithm with orthogonal successions. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/ActionFlowViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/ActionFlowViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/action-flow-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/action-flow-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/action-flow-view-layout-strategy.yaml");
        }

        // The action view shares placement, scoping, warning, and name-line helpers with the other strategies.
        dependency ActionFlowViewLayoutStrategy to LayeredPlacement;
        dependency ActionFlowViewLayoutStrategy to ExposeScopeResolver;
        dependency ActionFlowViewLayoutStrategy to LayoutWarnings;
        dependency ActionFlowViewLayoutStrategy to GeneralViewLayoutStrategy;
        dependency ActionFlowViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
