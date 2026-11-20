package SysML2ToolsTool {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Self-validation test runner: exercises the tool and records the results as TRX or JUnit files. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Tool/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/sysml2-tools-tool/self-test/validation.md");
            verificationFiles = ("docs/verification/sysml2-tools-tool/self-test/validation.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-tool/self-test/validation.yaml");
        }

        // Validation runs the tool through the program entry point, writes its output through the context, and builds
        // log paths safely.
        dependency Validation to Context;
        dependency Validation to Program;
        dependency Validation to PathHelpers;
    }
}
