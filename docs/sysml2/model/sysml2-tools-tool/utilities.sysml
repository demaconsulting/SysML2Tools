package SysML2ToolsTool_UtilitiesSubsystem {
    public import SysML2ToolsTool_DiagnosticFormatter::*;
    public import SysML2ToolsTool_PathHelpers::*;
    private import SoftwareItemMetadata::*;

    part def UtilitiesSubsystem {
        doc /* Shared utilities. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Utilities/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/utilities.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/utilities.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/utilities.yaml");
        }

        part pathHelpers : PathHelpers;
        part diagnosticFormatter : DiagnosticFormatter;
    }
}
