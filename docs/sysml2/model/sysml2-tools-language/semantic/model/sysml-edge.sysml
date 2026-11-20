package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def SysmlEdge {
        doc /* Public resolved-reference record (supertype, typing, metadata type, import, expose, redefinition, satisfy, verify, allocate, connect, transition, dependency, and binding) carrying the raw reference text of single-reference edges (documented in the model subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SysmlEdge.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/sysml-edge.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/sysml-edge.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model.yaml");
        }

        private import ScalarValues::String;

        attribute sourceQualifiedName : String [0..1];
        attribute targetQualifiedName : String;
        attribute kind : SysmlEdgeKind;
        attribute rawReference : String [0..1];
    }

    enum def SysmlEdgeKind {
        enum Supertype;
        enum Typing;
        enum Import;
        enum Satisfy;
        enum Verify;
        enum Allocate;
        enum Connect;
        enum Transition;
        enum Expose;
        enum Redefinition;
        enum MetadataType;
        enum Dependency;
        enum Binding;
    }
}
