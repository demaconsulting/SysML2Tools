// <!-- TEMPLATE-DIRECTIVE:
//      Read the sysml2-modeling.md standard before editing this file. This file is
//      intentionally repo-agnostic (no repo-specific imports) so it can be copied/synced
//      verbatim across repositories - do not add repo-specific content here. -->
// Shared metadata definitions for annotating software-item model elements with traceability
// links to their supporting project artifacts (source, test, design, verification, requirements).
// Intended to be reused as-is across multiple DEMA Consulting repositories - keep this package
// self-contained (no repo-specific imports) so it can be copied/synced without modification.
package SoftwareItemMetadata {
    private import ScalarValues::String;

    metadata def SoftwareInfo {
        doc /* Traceability links from a model element to its supporting project artifacts. */

        attribute sourceFiles : String [0..*];
        attribute testFiles : String [0..*];
        attribute designFiles : String [0..*];
        attribute verificationFiles : String [0..*];
        attribute requirementFiles : String [0..*];
    }
}
