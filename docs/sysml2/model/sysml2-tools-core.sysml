package SysML2ToolsCore {
    public import SysML2ToolsCore_IoSubsystem::*;
    public import SysML2ToolsCore_LayoutSubsystem::*;
    public import SysML2ToolsCore_QuerySubsystem::*;
    public import SysML2ToolsCore_RenderingSubsystem::*;
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    doc /* DemaConsulting.SysML2Tools.Core is the core library: layout strategies, rendering orchestration, file collection, and the model-analysis query API. */

    part def SysML2ToolsCoreSystem {
        doc /* The Core system as a whole: layout, rendering, io, and query subsystems. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core.md");
            verificationFiles = ("docs/verification/sysml2-tools-core.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core.yaml", "docs/reqstream/sysml2-tools-core/platform-requirements.yaml");
        }

        part layout : LayoutSubsystem;
        part renderingSubsystem : RenderingSubsystem;
        part io : IoSubsystem;
        part query : QuerySubsystem;
        part demaRendering : DemaRendering;
    }
}
