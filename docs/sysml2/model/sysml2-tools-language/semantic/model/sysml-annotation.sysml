package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def SysmlAnnotation {
        doc /* Public captured comment and documentation record (documented in the model subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SysmlAnnotation.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/sysml-annotation.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/sysml-annotation.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model.yaml");
        }

        private import ScalarValues::String;

        attribute kind : SysmlAnnotationKind;
        attribute text : String;
    }

    enum def SysmlAnnotationKind {
        enum Comment;
        enum Documentation;
    }
}
