package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def LayoutWarnings {
        doc /* Builder for layout diagnostic warning messages. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/LayoutWarnings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/LayoutWarningsTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/layout-warnings.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/layout-warnings.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/layout-warnings.yaml");
        }
    }
}
