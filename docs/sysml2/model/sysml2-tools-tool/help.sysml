package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def HelpSubsystem {
        doc /* Help command implementation: pure dispatch to the single source of truth for each command's help text. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Help/HelpSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/help.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/help.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/help.yaml");
        }

        part helpCommand : HelpCommand;
    }
}
