package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def StateTransitionViewLayoutStrategy {
        doc /* State machine view: states and guarded transitions placed top-to-bottom by the layered algorithm with orthogonal transitions. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/StateTransitionViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/StateTransitionViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/state-transition-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/state-transition-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/state-transition-view-layout-strategy.yaml");
        }

        // The state view shares placement, scoping, warning, and name-line helpers with the other strategies.
        dependency StateTransitionViewLayoutStrategy to LayeredPlacement;
        dependency StateTransitionViewLayoutStrategy to ExposeScopeResolver;
        dependency StateTransitionViewLayoutStrategy to LayoutWarnings;
        dependency StateTransitionViewLayoutStrategy to GeneralViewLayoutStrategy;
        dependency StateTransitionViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
