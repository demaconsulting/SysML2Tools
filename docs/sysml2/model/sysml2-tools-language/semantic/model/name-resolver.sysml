package SysML2ToolsLanguage {
    private import SoftwareItemMetadata::*;

    part def NameResolver {
        doc /* Public KerML 8.2.3.5 name resolver covering owned, imported, inherited, aliased, and short-named memberships, full and visible resolution, and dotted feature chains; used by ReferenceResolver and by layout strategies for references the semantic model did not resolve. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Semantic/Model/NameResolver.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Semantic/NameResolverTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/semantic/model/name-resolver.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/semantic/model/name-resolver.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/semantic/model/name-resolver.yaml");
        }

        // Name resolution reads the node hierarchy and the resolved edges to find members, inherited members, and
        // imports.
        dependency NameResolver to SysmlNode;
        dependency NameResolver to SysmlEdge;
    }
}
