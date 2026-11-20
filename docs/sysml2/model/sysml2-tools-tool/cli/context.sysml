package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner: parses the global and per-command arguments, owns the output and error writers and the optional log file, and records the exit status. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Cli/Context.cs", "src/DemaConsulting.SysML2Tools.Tool/Cli/SysmlCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Cli/CliArgumentHelpers.cs", "src/DemaConsulting.SysML2Tools.Tool/Cli/GlobalArguments.cs", "src/DemaConsulting.SysML2Tools.Tool/Cli/GlobalArgumentParser.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/cli/context.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/cli/context.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/cli/context.yaml");
        }
    }
}
