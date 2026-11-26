package SysML2ToolsTool_HelpCommand {
    private import SysML2ToolsTool_Context::*;
    private import OtsDependencies::*;
    private import SoftwareItemMetadata::*;

    part def HelpCommand {
        doc /* Built-in help command: answers the help verb from the help cards embedded in the assembly, routing help <command> to the command cards. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Cli/HelpCommand.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/cli/help-command.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/cli/help-command.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/cli/help-command.yaml");
        }

        // Serves the help command from the cards embedded in the assembly.
        dependency HelpCommand to HelpCards;
        // Reads the command names from the command enumeration that Context parses.
        dependency HelpCommand to Context;
        // Qualified (not imported): Program imports this package, and an import cycle triggers a lint warning.
        dependency HelpCommand to SysML2ToolsTool_Program::Program;
    }
}
