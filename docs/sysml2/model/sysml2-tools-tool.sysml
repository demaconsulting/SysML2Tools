package SysML2ToolsTool {
    public import SysML2ToolsTool_CliSubsystem::*;
    public import SysML2ToolsTool_ExportSubsystem::*;
    public import SysML2ToolsTool_LintSubsystem::*;
    public import SysML2ToolsTool_Program::*;
    public import SysML2ToolsTool_QuerySubsystem::*;
    public import SysML2ToolsTool_RenderSubsystem::*;
    public import SysML2ToolsTool_SelfTestSubsystem::*;
    public import SysML2ToolsTool_UtilitiesSubsystem::*;
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.SysML2Tools.Tool is the dotnet tool: a thin command-line wrapper and orchestration around the Language, Stdlib, and Core libraries. */

    part def SysML2ToolsToolSystem {
        doc /* The Tool system as a whole: program entry point and the cli, lint, render, query, export, self-test, and utilities subsystems. */

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
        part query : QuerySubsystem;
        part export : ExportSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
    }
}
