// SysML2Tools software structure model
// Describes the SysML2Tools software components using SysML v2 part definitions.

package SysML2Tools {

    // Core library subsystems
    package Core {

        // Parser subsystem: parses SysML/KerML source files
        part def WorkspaceParser {
        }

        // Semantic subsystem: builds symbol table and resolves references
        part def WorkspaceLoader {
        }

        // Internal: builds AST from CST
        part def AstBuilder {
        }

        // Internal: registers qualified names
        part def SymbolTable {
        }

        // Internal: resolves references and imports
        part def ReferenceResolver {
        }

        // Internal: walks specialization chains
        part def SupertypeWalker {
        }
    }

    // Tool subsystem: CLI commands
    package Tool {

        // Lint command: validates SysML/KerML files
        part def LintCommand {
        }
    }
}
