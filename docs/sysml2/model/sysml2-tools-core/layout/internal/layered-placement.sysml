package SysML2ToolsCore_LayeredPlacement {
    private import SoftwareItemMetadata::*;

    part def LayeredPlacement {
        doc /* Thin helper that adapts the off-the-shelf layered layout algorithm, returning placed rectangles and routed polylines to the strategies. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/LayeredPlacement.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/LayeredPlacementTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/layered-placement.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/layered-placement.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/layered-placement.yaml");
        }

        // Placement and routing are performed by the off-the-shelf layout engine.
        dependency LayeredPlacement to OtsDependencies::DemaRendering;
    }
}
