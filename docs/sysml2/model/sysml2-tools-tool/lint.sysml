package SysML2ToolsTool_LintSubsystem {
    public import SysML2ToolsTool_LintCommand::*;
    private import SoftwareItemMetadata::*;

    part def LintSubsystem {
        doc /* Lint command implementation: loads the workspace and reports its diagnostics. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Lint/LintSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/lint.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/lint.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/lint.yaml");
        }

        part lintCommand : LintCommand;
    }
}
