package SysML2ToolsTool_Program {
    private import SysML2ToolsTool_Context::*;
    private import SysML2ToolsTool_ExportCommand::*;
    private import SysML2ToolsTool_HelpCommand::*;
    private import SysML2ToolsTool_LintCommand::*;
    private import SysML2ToolsTool_QueryCommand::*;
    private import SysML2ToolsTool_RenderCommand::*;
    private import SysML2ToolsTool_Validation::*;
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator: answers the help command first, otherwise creates the context from the command line, dispatches to the validation and command handlers, and returns the exit code. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Program.cs", "src/DemaConsulting.SysML2Tools.Tool/ProgramStrings.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/ProgramTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/program.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/program.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/program.yaml");
        }

        // The entry point reads the parsed context, runs self-validation when asked, and otherwise dispatches to the
        // selected command.
        dependency Program to Context;
        dependency Program to Validation;
        dependency Program to HelpCommand;
        dependency Program to LintCommand;
        dependency Program to RenderCommand;
        dependency Program to QueryCommand;
        dependency Program to ExportCommand;
    }
}
