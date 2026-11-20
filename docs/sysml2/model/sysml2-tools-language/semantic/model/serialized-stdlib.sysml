package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def SerializedStdlib {
        doc /* Data transfer object for the standard-library binary serialization: the symbol dictionary and its diagnostics (documented in the model subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/SerializedStdlib.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model.yaml");
        }

        // Keyed by qualified name: the C# dictionary maps each qualified name to its node (the diagnostics list is a supporting type).
        part symbols : SysmlNode [0..*];
    }
}
