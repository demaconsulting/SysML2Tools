package SysML2ToolsCore_QualifiedNameShortener {
    private import SoftwareItemMetadata::*;

    part def QualifiedNameShortener {
        doc /* Strips the longest shared leading qualified-name segment prefix across a pool of qualified names; used only by the dependencies verb's Markdown rendering (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Utilities/QualifiedNameShortener.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Utilities/QualifiedNameShortenerTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }
    }
}
