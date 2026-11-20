package SysML2ToolsLanguage_SysmlMetadataNode {
    private import SoftwareItemMetadata::*;

    part def SysmlMetadataNode {
        doc /* Applied metadata annotation node with a raw type reference and captured scalar attribute values (design and verification are documented with the SysmlNode unit). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SysmlNode.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/AstBuilderMetadataTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/sysml-node.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/sysml-node.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/sysml-metadata-node.yaml");
        }

        private import ScalarValues::String;

        // The captured scalar attribute values (MetadataAttributeValue records) are supporting types and are not modeled separately.
        attribute typeReference : String;
    }
}
