package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def GeneralViewLayoutStrategy {
        doc /* General view: definitions and usage nodes grouped by package and placed top-to-bottom by the automatic layered/containment selection, with notation glyphs for specialization, feature membership, typing, subsetting, redefinition, satisfy, verify, and import edges. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/GeneralViewLayoutStrategy.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/GeneralViewLayoutStrategyTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/general-view-layout-strategy.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/general-view-layout-strategy.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/general-view-layout-strategy.yaml");
        }

        // The general view resolves its scope, reports layout warnings, resolves unresolved references and view
        // conditions through the language library, and places the graph with the off-the-shelf layout engine.
        dependency GeneralViewLayoutStrategy to ExposeScopeResolver;
        dependency GeneralViewLayoutStrategy to LayoutWarnings;
        dependency GeneralViewLayoutStrategy to SysML2ToolsLanguage::NameResolver;
        dependency GeneralViewLayoutStrategy to SysML2ToolsLanguage::FilterExpressionEvaluator;
        dependency GeneralViewLayoutStrategy to OtsDependencies::DemaRendering;
    }
}
