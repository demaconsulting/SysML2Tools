package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def GlobFileCollector {
        doc /* Resolves ordered glob patterns (with exclusions, recursive matching, and bare-star extension filtering) to a sorted, deduplicated list of absolute file paths. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Io/GlobFileCollector.cs", "src/DemaConsulting.SysML2Tools.Core/Io/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Io/GlobFileCollectorTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/io/glob-file-collector.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/io/glob-file-collector.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/io/glob-file-collector.yaml");
        }
    }
}
