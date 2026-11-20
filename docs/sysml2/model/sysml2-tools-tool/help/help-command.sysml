package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def HelpCommand {
        doc /* Parses the optional target command or verb and dispatches to that command's help-printing method (documented in the help subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/Help/HelpCommand.cs", "src/DemaConsulting.SysML2Tools.Tool/Help/HelpOptions.cs", "src/DemaConsulting.SysML2Tools.Tool/Help/HelpArgumentParser.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Help/HelpSubsystemTests.cs", "test/DemaConsulting.SysML2Tools.Tool.Tests/Help/HelpArgumentParserTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/help.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/help.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/help.yaml");
        }

        // Help dispatches to the help text owned by the program and by each command.
        dependency HelpCommand to Context;
        dependency HelpCommand to Program;
        dependency HelpCommand to LintCommand;
        dependency HelpCommand to RenderCommand;
        dependency HelpCommand to QueryCommand;
    }
}
