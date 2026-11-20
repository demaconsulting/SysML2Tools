package SysML2ToolsCore_SequenceViewLayoutStrategy {
    private import SysML2ToolsCore_ExposeScopeResolver::*;
    private import SysML2ToolsCore_GeneralViewLayoutStrategy::*;
    private import SoftwareItemMetadata::*;

    part def SequenceViewLayoutStrategy {
        doc /* Sequence view: typed lifelines with rounded usage headers and event-ordered messages labeled with their payload. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/SequenceViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/SequenceViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/sequence-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/sequence-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/sequence-view-layout-strategy.yaml");
        }

        // A lifeline header reuses the general view's keyword and name-line helpers.
        dependency SequenceViewLayoutStrategy to GeneralViewLayoutStrategy;
        dependency SequenceViewLayoutStrategy to ExposeScopeResolver;
        dependency SequenceViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
