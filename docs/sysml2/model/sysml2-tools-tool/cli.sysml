package SysML2ToolsTool_CliSubsystem {
    public import SysML2ToolsTool_Context::*;
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing and I/O. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/cli.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/cli.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/cli.yaml");
        }

        part context : Context;
    }
}
