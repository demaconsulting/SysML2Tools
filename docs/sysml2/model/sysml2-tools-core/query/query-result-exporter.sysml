package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def QueryResultExporter {
        doc /* Synchronous and asynchronous file-writing wrappers around the query result renderer (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryResultExporter.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Query/QueryResultExporterTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        // Exporting renders the result and writes the text to a file.
        dependency QueryResultExporter to QueryResultRenderer;
    }
}
