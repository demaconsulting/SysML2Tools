package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def BrowserViewLayoutStrategy {
        doc /* Browser view: indented membership tree. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/BrowserViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/BrowserAndGridViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/browser-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/browser-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/browser-view-layout-strategy.yaml");
        }

        // The browser view builds each row's name line with the general view's formatter.
        dependency BrowserViewLayoutStrategy to GeneralViewLayoutStrategy;
        dependency BrowserViewLayoutStrategy to ExposeScopeResolver;
        dependency BrowserViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
