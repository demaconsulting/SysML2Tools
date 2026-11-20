package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def DynamicViewSynthesizer {
        doc /* Synthesizes an ad-hoc view node for the command-line view-type, view-target, and filter options. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Rendering/Internal/DynamicViewSynthesizer.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Rendering/DynamicViewSynthesizerTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Layout/ExcludedMembersLayoutTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/rendering/internal/dynamic-view-synthesizer.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/rendering/internal/dynamic-view-synthesizer.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/rendering/internal/dynamic-view-synthesizer.yaml");
        }

        // The synthesized view shares the router's render-target token vocabulary.
        dependency DynamicViewSynthesizer to DiagramTypeRouter;
    }
}
