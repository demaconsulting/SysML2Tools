package SysML2ToolsCore_RenderingSubsystem {
    public import SysML2ToolsCore_DiagramRenderer::*;
    public import SysML2ToolsCore_RenderingInternalSubsystem::*;
    private import SoftwareItemMetadata::*;

    part def RenderingSubsystem {
        doc /* Rendering pipeline: consumes the off-the-shelf renderer, theme, and render-option contracts and retains the SysML-coupled layout-strategy contract, the diagram renderer, the standard-library filter, and the public theme presets. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core/rendering.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/rendering.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/rendering.yaml");
        }

        part diagramRenderer : DiagramRenderer;
        part internal : RenderingInternalSubsystem;
    }
}
