package SysML2ToolsCore_LayoutSubsystem {
    public import SysML2ToolsCore_LayoutInternalSubsystem::*;
    private import SoftwareItemMetadata::*;

    part def LayoutSubsystem {
        doc /* Maps the SysML semantic model onto the off-the-shelf LayoutTree intermediate representation and delegates geometric placement and routing to the DemaConsulting.Rendering.Layout layered algorithm. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core/layout.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout.yaml");
        }

        part internal : LayoutInternalSubsystem;
    }
}
