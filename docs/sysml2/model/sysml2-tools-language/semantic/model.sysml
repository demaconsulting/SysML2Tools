package SysML2ToolsLanguage_ModelSubsystem {
    private import SysML2ToolsLanguage_AstBuilder::*;
    private import SysML2ToolsLanguage_AstSerializerContext::*;
    private import SysML2ToolsLanguage_NameResolver::*;
    private import SysML2ToolsLanguage_ReferenceResolver::*;
    private import SysML2ToolsLanguage_SemanticIndex::*;
    private import SysML2ToolsLanguage_SerializedStdlib::*;
    private import SysML2ToolsLanguage_SupertypeWalker::*;
    private import SysML2ToolsLanguage_SymbolTable::*;
    private import SysML2ToolsLanguage_SysmlAnnotation::*;
    private import SysML2ToolsLanguage_SysmlEdge::*;
    private import SysML2ToolsLanguage_SysmlMetadataNode::*;
    private import SysML2ToolsLanguage_SysmlNode::*;
    private import SoftwareItemMetadata::*;

    part def ModelSubsystem {
        doc /* Semantic model: the public model types plus the internal build and resolve implementation. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-language/semantic/model.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model.yaml");
        }

        part sysmlNode : SysmlNode;
        part sysmlMetadataNode : SysmlMetadataNode;
        part astBuilder : AstBuilder;
        part symbolTable : SymbolTable;
        part referenceResolver : ReferenceResolver;
        part nameResolver : NameResolver;
        part supertypeWalker : SupertypeWalker;
        part sysmlEdge : SysmlEdge;
        part semanticIndex : SemanticIndex;
        part sysmlAnnotation : SysmlAnnotation;
        part serializedStdlib : SerializedStdlib;
        part astSerializerContext : AstSerializerContext;
    }
}
