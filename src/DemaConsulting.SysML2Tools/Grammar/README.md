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

### Patch 1 — KerML classifier types added to `definitionElement`

**Rule affected:** `definitionElement`

The upstream `daltskin/sysml-v2-grammar` grammar omits KerML classifier definition
types from the `definitionElement` rule. `definitionElement` is the production that
matches any declaration appearing as a direct member of a `package` or
`library package` body. The missing types are therefore invisible to the parser
at package level, causing parse errors on any file that uses them at the top level.

This affects all `.kerml` files in the OMG standard library. For example,
`Objects.kerml` opens with:

```sysml
library package Objects {
    abstract class Object specializes Occurrence::Occurrence;
    abstract struct Structure;
    ...
}
```

The rules `dataType`, `class`, `structure`, `association`, and `associationStructure`
are all **defined** in the grammar (they exist in `nonFeatureElement`, which is
reachable from type bodies), but they are not listed as alternatives in
`definitionElement`, so they are unreachable from the package-member path.

**Fix:** append the five missing alternatives to `definitionElement`:

```antlr
    // KerML classifier types — present in the stdlib .kerml files and valid SysML v2 textual notation
    | dataType
    | class
    | structure
    | association
    | associationStructure
```

---

### Patch 2 — KerML behavioral classifier types added to `definitionElement`

**Rule affected:** `definitionElement`

The same omission as Patch 1 applies to KerML behavioral classifiers: `function`
and `predicate`. These are used throughout the KerML function libraries. For example,
`BaseFunctions.kerml` opens with:

```kerml
library package BaseFunctions {
    abstract function '==' { in x: Anything[0..1]; in y: Anything[0..1]; }
    function '!='          { in x: Anything[0..1]; in y: Anything[0..1]; }
}
```

Like the classifier types in Patch 1, the `function` and `predicate` rules exist in
`nonFeatureElement` (reachable from type bodies) but are absent from `definitionElement`
(reachable from package bodies).

**Fix:** append two further alternatives to `definitionElement`:

```antlr
    // KerML behavioral classifier types — present in the stdlib .kerml files
    | function
    | predicate
```

---

### Patch 3 — Arrow expression accepts bare function-reference argument

**Rule affected:** `ownedExpression` (the `ARROW` alternative)

The KerML standard library uses `->reduce` and similar collection operations that
pass a function reference as a bare restricted name (single-quoted identifier), without
wrapping it in parentheses or braces. For example, in `Collections.kerml`:

```kerml
feature flattenedSize : Positive[1] = dimensions->reduce '*' ?? 1;
```

The upstream grammar rule for arrow expressions is:

```antlr
| ownedExpression ARROW qualifiedName ( bodyExpression | argumentList )
```

`bodyExpression` matches `{ ... }` and `argumentList` matches `( ... )`. The bare
name `'*'` fits neither, so the parser raises an error on the `'*'` token and
enters error recovery, corrupting the parse state for subsequent declarations in
the same file.

Note that the `name` rule in this grammar already accepts `STRING` tokens (the
lexer rule `STRING : '\'' ... '\''` matches single-quoted values), so `'*'` and
`'=='` are valid `qualifiedName` tokens — they simply could not appear in this
position.

**Fix:** add `qualifiedName` as a third option and make the entire suffix optional
(a bare `->method` with no argument is also valid for pure feature-chain navigation):

```antlr
| ownedExpression ARROW qualifiedName ( bodyExpression | argumentList | qualifiedName )?
```

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
