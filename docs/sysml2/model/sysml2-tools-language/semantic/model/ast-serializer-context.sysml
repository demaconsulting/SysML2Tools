package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def AstSerializerContext {
        doc /* Source-generated JSON context that makes the AST serialization trim- and AOT-safe (documented in the model subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/AstSerializerContext.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model.yaml");
        }

        // The context declares the serializable shape of the stdlib transfer object.
        dependency AstSerializerContext to SerializedStdlib;
    }
}
