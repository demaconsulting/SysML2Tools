## ANTLR4 Verification

This document provides the verification evidence for the ANTLR4 OTS software item
(`Antlr4.Runtime.Standard` 4.13.1). Requirements for this OTS item are defined in the ANTLR4
OTS Software Requirements document.

### Required Functionality

`Antlr4.Runtime.Standard` is the ANTLR4 C# runtime used to execute the pre-generated SysML v2
lexer and parser. It must correctly drive the generated lexer/parser classes against SysML v2
source text and invoke registered `IAntlrErrorListener` implementations when syntax errors occur.

### Verification Approach

ANTLR4 is verified by self-validation evidence from the CI pipeline. The `WorkspaceParser` unit
tests exercise the ANTLR4 runtime end-to-end on every test run. A passing pipeline run for all
scenarios constitutes evidence that both requirements are satisfied.

`Parse_StdlibOnly_NoErrors` is the primary integration evidence: it parses all 58 embedded
`.sysml` stdlib files through the ANTLR4 runtime and asserts that no error-severity diagnostics
are produced, proving that the runtime correctly processes valid SysML v2 source.

### Test Scenarios

#### ParseSource_EmptyFile_NoErrors

**Scenario**: ANTLR4 runtime drives `SysMLv2Lexer` and `SysMLv2Parser` over an empty string;
`rootNamespace()` is invoked.

**Expected**: ANTLR4 completes without invoking the error listener; the returned diagnostic list
is empty.

**Requirement coverage**: `SysML2Tools-OTS-ANTLR4-Parse`.

#### ParseSource_MinimalPackage_NoErrors

**Scenario**: ANTLR4 runtime processes `"package MyPackage {}"` through the full lexer/parser
pipeline.

**Expected**: ANTLR4 tokenizes and parses the input without errors; the returned diagnostic list
is empty.

**Requirement coverage**: `SysML2Tools-OTS-ANTLR4-Parse`.

#### ParseSource_PartDef_NoErrors

**Scenario**: ANTLR4 runtime processes a multi-line SysML source containing a package with a
`part def` block and an attribute declaration.

**Expected**: ANTLR4 parses all constructs without errors; the returned diagnostic list is
empty.

**Requirement coverage**: `SysML2Tools-OTS-ANTLR4-Parse`.

#### ParseSource_InvalidSyntax_ReportsError

**Scenario**: ANTLR4 runtime encounters `"@@@ NOT VALID SYSML @@@"` which cannot be tokenized
or parsed as valid SysML v2.

**Expected**: ANTLR4 invokes the registered `SysmlDiagnosticListener` error handler at least
once; the returned diagnostic list is non-empty and contains at least one `Error`-severity entry.

**Requirement coverage**: `SysML2Tools-OTS-ANTLR4-Errors`.

#### ParseSource_ErrorPath_MatchesSuppliedPath

**Scenario**: ANTLR4 runtime encounters `"@@@"` while the listener is bound to path
`"my-model.sysml"`.

**Expected**: ANTLR4 invokes the registered listener with the error; all diagnostics have
`FilePath == "my-model.sysml"`, confirming the listener receives the correct context.

**Requirement coverage**: `SysML2Tools-OTS-ANTLR4-Errors`.

#### Parse_StdlibOnly_NoErrors

**Scenario**: ANTLR4 runtime processes all 58 embedded `.sysml` stdlib files sequentially
through the full lexer/parser pipeline.

**Expected**: ANTLR4 parses every stdlib file without invoking the error listener; `HasErrors`
is false. This is the primary integration evidence for the ANTLR4 runtime.

**Requirement coverage**: `SysML2Tools-OTS-ANTLR4-Parse`, `SysML2Tools-OTS-ANTLR4-Errors`.
