package SysML2ToolsStdlib {
    public import SysML2ToolsStdlib_StdlibProvider::*;
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.SysML2Tools.Stdlib is the standard-library package: the pre-compiled SysML v2 standard library JSON embedded as a gzip-compressed managed resource. */

    part def SysML2ToolsStdlibSystem {
        doc /* The Stdlib system as a whole: the provider of the embedded standard-library symbol table. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-stdlib.md");
            verificationFiles = ("docs/verification/sysml2-tools-stdlib.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-stdlib.yaml", "docs/reqstream/sysml2-tools-stdlib/platform-requirements.yaml");
        }

        part stdlibProvider : StdlibProvider;
    }
}
