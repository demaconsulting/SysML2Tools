package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def GridViewLayoutStrategy {
        doc /* Grid view: specialization and relationship matrix. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/GridViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/BrowserAndGridViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/grid-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/grid-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/grid-view-layout-strategy.yaml");
        }

        // The grid view scopes its definitions through the shared expose resolver.
        dependency GridViewLayoutStrategy to ExposeScopeResolver;
        dependency GridViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
