package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* Self-validation test runner. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/SelfTest/SelfTestSubsystemTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/self-test.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/self-test.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/self-test.yaml");
        }

        part validation : Validation;
    }
}
