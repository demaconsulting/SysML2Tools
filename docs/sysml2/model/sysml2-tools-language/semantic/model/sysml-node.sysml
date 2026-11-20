package SysML2ToolsLanguage_SysmlNode {
    private import SoftwareItemMetadata::*;

    part def SysmlNode {
        doc /* Public AST node hierarchy: named-element, view, and import node types. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SysmlNode.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/sysml-node.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/sysml-node.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/sysml-node.yaml");
        }
    }
}
