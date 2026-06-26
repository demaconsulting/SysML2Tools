# Grammar

This folder contains the ANTLR4 grammar files for SysML v2 textual notation.
The generated C# parser lives in `../Parser/Antlr/` and is committed to the
repository — no Java is required to build this project.

## Source Versions

- **Grammar files** (`SysMLv2Lexer.g4`, `SysMLv2Parser.g4`):
  [daltskin/sysml-v2-grammar][grammar-repo], commit `bac7eb87` (OMG release 2026-04, dated 2026-05-22)
- **Stdlib** (`.sysml` / `.kerml` files):
  [Systems-Modeling/SysML-v2-Release][sysml-repo], tag `2026-04`
- **ANTLR4 generator**: `antlr-4.13.1-complete.jar`, version 4.13.1
- **JDK**: OpenJDK 25.0.3+9 (JDK 25)

[grammar-repo]: https://github.com/daltskin/sysml-v2-grammar
[sysml-repo]: https://github.com/Systems-Modeling/SysML-v2-Release

## Local Patches

The grammar files in this folder are not a verbatim copy of the upstream source.
The following patches have been applied and **must be re-applied** after any upstream grammar update:

### `SysMLv2Parser.g4` — KerML classifier types added to `definitionElement`

The upstream `daltskin/sysml-v2-grammar` grammar omits KerML classifier definition
types (`datatype`, `class`, `struct`, `assoc`) from the `definitionElement` rule.
This means the grammar cannot parse the KerML-syntax files in the OMG standard
library (e.g., `Collections.kerml`, `Objects.kerml`), even though these files are
valid SysML v2 / KerML textual notation.

The following alternatives have been appended to `definitionElement`:

```antlr
    // KerML classifier types — present in the stdlib .kerml files and valid SysML v2 textual notation
    | dataType
    | class
    | structure
    | association
    | associationStructure
```

All five rules (`dataType`, `class`, `structure`, `association`, `associationStructure`)
are already defined elsewhere in the grammar — this patch simply makes them reachable
as package-level members.

## Regenerating the Parser

To regenerate `../Parser/Antlr/` after a grammar update:

1. Download the updated `.g4` files from [daltskin/sysml-v2-grammar][grammar-repo]
   and replace the files in this folder.
2. Re-apply the patches listed in **Local Patches** above.
3. Download the ANTLR4 complete jar from <https://www.antlr.org/download.html>
   (use the same version as `Antlr4.Runtime.Standard` in the `.csproj`).
4. **Run from this Grammar folder** (not the repository root — ANTLR creates a
   nested subdirectory when invoked with relative paths from a parent directory):

   ```pwsh
   $java  = 'path\to\java.exe'   # JDK 11 or later required; JDK 25 confirmed working
   $antlr = 'path\to\antlr-4.13.1-complete.jar'
   $out   = Resolve-Path '..\Parser\Antlr'
   Remove-Item "$out\*.cs","$out\*.interp","$out\*.tokens" -Force
   Push-Location (Split-Path $MyInvocation.MyCommand.Path)  # ensure CWD = Grammar\
   & $java -jar $antlr -Dlanguage=CSharp `
       -package DemaConsulting.SysML2Tools.Parser.Antlr `
       -listener -visitor -o $out `
       SysMLv2Lexer.g4 SysMLv2Parser.g4
   Pop-Location
   ```

   > **Important:** ANTLR must be invoked with the grammar directory as the working
   > directory. If invoked from a parent directory with relative `.g4` paths, ANTLR
   > mirrors the relative path structure inside `-o`, producing a nested subdirectory
   > instead of placing files directly in `$out`.

5. Update the version table above.
6. Commit the regenerated files.

> **Note:** The `.interp` and `.tokens` files in `Antlr/` are not needed
> at runtime — the ANTLR4 ATN is serialized inline in the generated C# — but
> they are committed alongside the `.cs` files to keep regeneration reproducible.
