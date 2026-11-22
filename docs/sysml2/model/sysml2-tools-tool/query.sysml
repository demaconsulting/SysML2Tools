package SysML2ToolsTool_QuerySubsystem {
    public import SysML2ToolsTool_QueryCommand::*;
    private import SoftwareItemMetadata::*;

    part def QuerySubsystem {
        doc /* Query command implementation: resolves a workspace, runs one of the query verbs against it, and renders the result as Markdown or JSON. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QuerySubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/query.yaml");
        }

        part queryCommand : QueryCommand;
    }
}
