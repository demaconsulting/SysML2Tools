# Grammar

This folder contains the ANTLR4 grammar files for SysML v2 textual notation.
The generated C# parser lives in `../Parser/Generated/` and is committed to the
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

## Regenerating the Parser

To regenerate `../Parser/Generated/` after a grammar update:

1. Download the updated `.g4` files from [daltskin/sysml-v2-grammar][grammar-repo]
   and replace the files in this folder.
2. Download the ANTLR4 complete jar from <https://www.antlr.org/download.html>
   (use the same version as `Antlr4.Runtime.Standard` in the `.csproj`).
3. Run from the repository root:

   ```pwsh
   $java  = 'path\to\java.exe'
   $antlr = 'path\to\antlr-4.13.1-complete.jar'
   $src   = 'src\DemaConsulting.SysML2Tools\Grammar'
   $out   = 'src\DemaConsulting.SysML2Tools\Parser\Generated'
   Remove-Item $out\* -Include *.cs,*.interp,*.tokens -Force
   & $java -jar $antlr -Dlanguage=CSharp `
       -package DemaConsulting.SysML2Tools.Parser.Generated `
       -listener -visitor -o $out `
       $src\SysMLv2Lexer.g4 $src\SysMLv2Parser.g4
   ```

4. Update the version table above.
5. Commit the regenerated files.

> **Note:** The `.interp` and `.tokens` files in `Generated/` are not needed
> at runtime — the ANTLR4 ATN is serialized inline in the generated C# — but
> they are committed alongside the `.cs` files to keep regeneration reproducible.
