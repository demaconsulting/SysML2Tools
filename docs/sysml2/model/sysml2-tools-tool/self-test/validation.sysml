package SysML2ToolsTool_Validation {
    private import SysML2ToolsTool_Context::*;
    private import SysML2ToolsTool_PathHelpers::*;
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
        // Qualified (not imported): Program imports this package, and an import cycle triggers a lint warning.
        dependency Validation to SysML2ToolsTool_Program::Program;
        dependency Validation to PathHelpers;
    }
}
