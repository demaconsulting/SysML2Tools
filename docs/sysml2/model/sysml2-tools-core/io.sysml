package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def IoSubsystem {
        doc /* Shared file glob pattern resolution used by the Tool project's lint, render, query, and export commands. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core/io.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/io.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/io.yaml");
        }

        part globFileCollector : GlobFileCollector;
    }
}
