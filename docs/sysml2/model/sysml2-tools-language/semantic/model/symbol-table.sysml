package SysML2ToolsLanguage_SymbolTable {
    private import SysML2ToolsLanguage_SysmlNode::*;
    private import SoftwareItemMetadata::*;

    part def SymbolTable {
        doc /* Registry mapping qualified names to declaration nodes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SymbolTable.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/symbol-table.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/symbol-table.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/symbol-table.yaml");
        }

        // The registry stores the declaration nodes it maps by qualified name.
        dependency SymbolTable to SysmlNode;
    }
}
