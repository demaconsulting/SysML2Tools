package SysML2ToolsCore_QueryArgumentParser {
    private import SysML2ToolsCore_QueryOptions::*;
    private import SysML2ToolsCore_QueryVerb::*;
    private import SoftwareItemMetadata::*;

    part def QueryArgumentParser {
        doc /* Parses a token list into query options and file patterns for callers that accept the same verb and option grammar as the command line (documented in the query subsystem docs). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Core/Query/QueryArgumentParser.cs");
            designFiles = ("docs/design/sysml2-tools-core/query.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/query.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/query.yaml");
        }

        // Parsing converts verb tokens and produces the option record.
        dependency QueryArgumentParser to QueryVerb;
        dependency QueryArgumentParser to QueryOptions;
    }
}
