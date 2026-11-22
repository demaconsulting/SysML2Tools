package SysML2ToolsTool_RenderSubsystem {
    public import SysML2ToolsTool_RenderCommand::*;
    private import SoftwareItemMetadata::*;

    part def RenderSubsystem {
        doc /* Render command implementation: loads the workspace and writes one diagram file per view. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Render/RenderSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/render.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/render.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/render.yaml");
        }

        part renderCommand : RenderCommand;
    }
}
