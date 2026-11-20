package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def QueryVerb {
        doc /* Fixed 12-verb vocabulary of the query API, with token conversion and the rule for which verbs require an element (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryVerb.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        attribute verb : QueryVerbKind;
    }

    enum def QueryVerbKind {
        enum Uses;
        enum UsedBy;
        enum Dependencies;
        enum Impact;
        enum Describe;
        enum Hierarchy;
        enum Requirements;
        enum Interface;
        enum Connections;
        enum States;
        enum List;
        enum Find;
    }
}
