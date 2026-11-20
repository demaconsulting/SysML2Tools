package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def ExportSubsystem {
        doc /* Export command implementation: writes a lossless, faithful dump of the loaded model. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Export/ExportSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/export.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/export.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/export.yaml");
        }

        part exportCommand : ExportCommand;
    }
}
