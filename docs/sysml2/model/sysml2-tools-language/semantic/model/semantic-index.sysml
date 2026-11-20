package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def SemanticIndex {
        doc /* Public reverse-lookup index over resolved SysmlEdge instances (documented in the model subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SemanticIndex.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/semantic-index.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/semantic-index.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model.yaml");
        }

        // The index holds and looks up the resolved edge records.
        dependency SemanticIndex to SysmlEdge;
    }
}
