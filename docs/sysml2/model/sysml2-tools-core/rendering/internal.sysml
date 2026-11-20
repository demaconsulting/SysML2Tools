package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def RenderingInternalSubsystem {
        doc /* Internal rendering implementation: layout-strategy routing and dynamic view synthesis. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core/rendering/internal.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/rendering/internal.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/rendering/internal.yaml");
        }

        part diagramTypeRouter : DiagramTypeRouter;
        part dynamicViewSynthesizer : DynamicViewSynthesizer;
    }
}
