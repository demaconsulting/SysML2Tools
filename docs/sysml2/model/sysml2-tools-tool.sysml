package SysML2ToolsTool {
    private import SysML2ToolsTool_CliSubsystem::*;
    private import SysML2ToolsTool_ExportSubsystem::*;
    private import SysML2ToolsTool_HelpSubsystem::*;
    private import SysML2ToolsTool_LintSubsystem::*;
    private import SysML2ToolsTool_Program::*;
    private import SysML2ToolsTool_QuerySubsystem::*;
    private import SysML2ToolsTool_RenderSubsystem::*;
    private import SysML2ToolsTool_SelfTestSubsystem::*;
    private import SysML2ToolsTool_UtilitiesSubsystem::*;
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.SysML2Tools.Tool is the dotnet tool: a thin command-line wrapper and orchestration around the Language, Stdlib, and Core libraries. */

    part def SysML2ToolsToolSystem {
        doc /* The Tool system as a whole: program entry point and the cli, lint, render, help, query, export, self-test, and utilities subsystems. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool.yaml", "docs/reqstream/sysml2-tools-tool/platform-requirements.yaml");
        }

        part program : Program;
        part cli : CliSubsystem;
        part lint : LintSubsystem;
        part renderSubsystem : RenderSubsystem;
        part help : HelpSubsystem;
        part query : QuerySubsystem;
        part export : ExportSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
    }
}
