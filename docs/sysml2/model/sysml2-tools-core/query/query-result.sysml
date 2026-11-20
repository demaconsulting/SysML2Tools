package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def QueryResult {
        doc /* Verb-agnostic result model: the result, its entries, and the entry direction (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryResult.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        private import ScalarValues::String;

        attribute verb : String;
        attribute element : String [0..1];
        attribute summary : String [0..*];
        attribute entries : QueryResultEntry [0..*];
    }

    attribute def QueryResultEntry {
        private import ScalarValues::Integer;
        private import ScalarValues::String;

        attribute qualifiedName : String;
        attribute kind : String [0..1];
        attribute detail : String [0..1];
        attribute notes : String [0..*];
        attribute direction : QueryEntryDirection [0..1];
        attribute depth : Integer [0..1];
        attribute relation : SysML2ToolsLanguage::SysmlEdgeKind [0..1];
        attribute viaQualifiedName : String [0..1];
    }

    enum def QueryEntryDirection {
        enum Outgoing;
        enum Incoming;
    }
}
