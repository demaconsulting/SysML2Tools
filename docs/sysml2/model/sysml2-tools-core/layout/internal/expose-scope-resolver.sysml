package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def ExposeScopeResolver {
        doc /* Shared helper that resolves a view's expose-statement qualified-name containment-subtree scope, used by every layout strategy. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Layout/Internal/ExposeScopeResolver.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Layout/ExposeScopeResolverTests.cs");
            designFiles = ("docs/design/sysml2-tools-core/layout/internal/expose-scope-resolver.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal/expose-scope-resolver.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal/expose-scope-resolver.yaml");
        }

        // Bracket-filtered expose members are parsed and evaluated with the language library's filter expressions.
        dependency ExposeScopeResolver to SysML2ToolsLanguage::FilterExpressionEvaluator;
        dependency ExposeScopeResolver to SysML2ToolsLanguage::SysmlEdge;
    }
}
