package SysML2ToolsCore_InterconnectionViewLayoutStrategy {
    private import SysML2ToolsCore_ExposeScopeResolver::*;
    private import SysML2ToolsCore_GeneralViewLayoutStrategy::*;
    private import SysML2ToolsCore_LayeredPlacement::*;
    private import SoftwareItemMetadata::*;

    part def InterconnectionViewLayoutStrategy {
        doc /* Internal structure view: nested usages, declared ports, and connectors (connection, binding, interface, flow) with end labels. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/InterconnectionViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/InterconnectionViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/interconnection-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/interconnection-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/interconnection-view-layout-strategy.yaml");
        }

        // The interconnection view reuses the general view's label and compartment helpers so an element reads the
        // same in every view.
        dependency InterconnectionViewLayoutStrategy to LayeredPlacement;
        dependency InterconnectionViewLayoutStrategy to GeneralViewLayoutStrategy;
        dependency InterconnectionViewLayoutStrategy to ExposeScopeResolver;
        dependency InterconnectionViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
