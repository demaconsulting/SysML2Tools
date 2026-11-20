package SysML2ToolsStdlib_StdlibProvider {
    private import SoftwareItemMetadata::*;

    part def StdlibProvider {
        doc /* Lazy, cached provider of the pre-resolved SysML v2 standard-library symbol table, decompressed and deserialized from the embedded stdlib.json.gz resource. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Stdlib/StdlibProvider.cs", "src/DemaConsulting.SysML2Tools.Stdlib/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/StdlibProviderTests.cs");
            designFiles = ("docs/design/sysml2-tools-stdlib/stdlib-provider.md");
            verificationFiles = ("docs/verification/sysml2-tools-stdlib/stdlib-provider.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-stdlib/stdlib-provider.yaml");
        }

        // The provider deserializes the embedded standard-library bytes into a symbol table.
        dependency StdlibProvider to SysML2ToolsLanguage_AstDeserializer::AstDeserializer;
    }
}
