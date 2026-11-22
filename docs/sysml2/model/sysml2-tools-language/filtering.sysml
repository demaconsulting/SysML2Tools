package SysML2ToolsLanguage_FilteringSubsystem {
    public import SysML2ToolsLanguage_FilterExpressionEvaluator::*;
    private import SoftwareItemMetadata::*;

    part def FilteringSubsystem {
        doc /* Parses and evaluates the standalone view filter expressions (and import and expose bracket filters) over metadata annotations; lives in the Language library so ReferenceResolver can evaluate filtered imports. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-language/filtering.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/filtering.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/filtering.yaml");
        }

        part filterExpressionEvaluator : FilterExpressionEvaluator;
    }
}
