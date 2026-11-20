package SysML2ToolsLanguage_SupertypeWalker {
    private import SysML2ToolsLanguage_SymbolTable::*;
    private import SysML2ToolsLanguage_SysmlNode::*;
    private import SoftwareItemMetadata::*;

    part def SupertypeWalker {
        doc /* Walks specialization chains and detects cyclic specialization. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SupertypeWalker.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/supertype-walker.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/supertype-walker.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/supertype-walker.yaml");
        }

        // The walk iterates the registered symbols and follows each node's supertype names through the symbol table.
        dependency SupertypeWalker to SymbolTable;
        dependency SupertypeWalker to SysmlNode;
    }
}
