package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def QueryOptions {
        doc /* Immutable option record shared by every query verb (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryOptions.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        private import ScalarValues::Boolean;
        private import ScalarValues::Integer;
        private import ScalarValues::String;

        attribute verb : QueryVerbKind;
        attribute element : String [0..1];
        attribute format : String [0..1];
        attribute walkDepth : Integer [0..1];
        attribute heading : String [0..1];
        attribute direction : String [0..1];
        attribute kind : String [0..1];
        attribute nameFilter : String [0..1];
        attribute includeStdlib : Boolean;
        attribute includeConnections : Boolean;

        // The options record names the verb it applies to.
        dependency QueryOptions to QueryVerb;
    }
}
