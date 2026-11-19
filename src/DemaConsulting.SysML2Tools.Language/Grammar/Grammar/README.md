# Grammar

This folder contains the ANTLR4 grammar files for SysML v2 textual notation.
The generated C# parser lives in `../../Parser/Antlr/` and is committed to the
repository — no Java is required to build this project.

## Source Versions

- **Grammar files** (`SysMLv2Lexer.g4`, `SysMLv2Parser.g4`):
  [daltskin/sysml-v2-grammar][grammar-repo], commit `bac7eb87` (OMG release 2026-04, dated 2026-05-22)
- **Stdlib** (`.sysml` / `.kerml` files):
  [Systems-Modeling/SysML-v2-Release][sysml-repo], tag `2026-04`
- **ANTLR4 generator**: `antlr-4.13.1-complete.jar`, version 4.13.1
- **JDK**: a portable OpenJDK 21.0.13+9 build was used for the current output; the generated
  code does not depend on the JDK version (JDK 11 or later is required to run the generator)

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

---

### Patch 4 — KerML package members reachable from package bodies; optional declarations for step/expr/bool/inv

**Rules affected:** `packageBodyElement` (new `kermlPackageMember` and `kermlNonFeatureElement` rules),
`step`, `expression`, `booleanExpression`, `invariant`, and the follow-ons 4c-1 to 4c-3 below.

**What was missing and why it matters.** A `package` or `library package` body (and the file root)
accepts only the SysML member forms (`packageMember` → `definitionElement | usageElement`). The KerML
members of the standard library's `.kerml` files — `abstract classifier Anything;`,
`abstract metaclass SemanticMetadata`, `feature pi : Real = ...;`, `behavior`, `interaction`, `type`,
`step`, `connector`, `inv { ... }`, the relationship statements — are defined in the grammar
(`nonFeatureElement`, `featureElement`) but reachable only from type bodies, so every such declaration
at package level was a syntax error, its name was never indexed, and references to it from SysML models
(`Metaobjects::SemanticMetadata`, `Objects::Point`, `TrigFunctions::pi`, `Occurrences::HappensBefore`)
were reported as unresolved. The standard library compile went from 1,964 warnings to 65 (0 syntax
warnings).

#### 4a — KerML members in a package body

**Fix:** add a last alternative to `packageBodyElement` and two new rules:

```antlr
packageBodyElement
    : packageMember
    | elementFilterMember
    | aliasMember
    | importRule
    | kermlPackageMember
    ;

kermlPackageMember
    : memberPrefix ( kermlNonFeatureElement | featureElement )
    ;

kermlNonFeatureElement
    : namespace | type | classifier | metaclass | interaction | behavior | multiplicity
    | specialization | conjugation | subclassification | disjoining | featureInverting
    | typingStatement | subsetting | redefinition | typeFeaturing
    ;
```

`kermlPackageMember` is the KerML `NamespaceMember` (`NonFeatureMember | NamespaceFeatureMember`) minus
the members `packageMember` already reaches (`package`, `library package`, `dependency`, comments and
documentation, and the Patch 1/2 classifier kinds). The alternative is last, so every SysML parse keeps
priority, and it is deliberately disjoint from the SysML alternatives: a first version that reused
`namespaceMember` directly overlapped them (e.g. `package` and `doc`), which forced ANTLR into
full-context prediction across whole nested package bodies and made parsing of SysML models roughly 60
times slower.

#### 4b — Optional declaration on `step`, `expr`, `bool`, `inv`

```antlr
step              : featurePrefix STEP featureDeclaration? valuePart? typeBody ;
expression        : featurePrefix EXPR featureDeclaration? valuePart? functionBody ;
booleanExpression : featurePrefix BOOL featureDeclaration? valuePart? functionBody ;
invariant         : featurePrefix INV ( TRUE | FALSE )? featureDeclaration? valuePart? functionBody ;
```

The upstream rules required the declaration, rejecting `inv { ... }` and `step;`, which the KerML grammar
allows.

#### 4c-1 — `typingStatement` split out of `featureTyping`

The upstream `featureTyping` rule has a first alternative for the KerML relationship statement
(`typing x : T;`) and bare `ownedFeatureTyping`/`conjugatedPortTyping` alternatives, and `nonFeatureElement`
used the whole rule. A bare name is then a valid member, so `feature f : T;` was parsed as the member
`feature` (the keyword accepted as a name) followed by the SysML usage `f : T;` — silently, with no
diagnostic and no `feature` node. The statement moved to its own rule, and `nonFeatureElement` uses it:

```antlr
featureTyping
    : ownedFeatureTyping
    | conjugatedPortTyping
    ;

typingStatement
    : ( SPECIALIZATION identification? )? TYPING qualifiedName ( COLON | TYPED BY ) generalType relationshipBody
    ;
```

#### 4c-2 — KerML function bodies use the KerML `FunctionBodyPart`

`functionBody` (used only by `function`, `predicate`, `expr`, `bool`, `inv`) now uses a new
`kermlFunctionBodyPart : ( typeBodyElement | returnFeatureMember )* ( resultExpressionMember )?`, the KerML
form. The upstream `functionBodyPart` also accepts SysML `definitionBodyItem`s first, so a parameter
`in x : T;` parsed as an anonymous SysML reference usage and no feature was produced. `functionBodyPart`
itself is unchanged because the SysML `bodyExpression` uses it.

#### 4c-3 — Library names that are SysML keywords

`ENTRY`, `DO`, `EXIT`, `ACCEPT`, and `FRAME` were added to `unreservedKeyword` (the existing mechanism
for keywords that are names in the standard library), because the kernel libraries name features
`entry`, `do`, `exit`, `accept`, and `frame` (`step do[1] subsets middle;`, `in frame : SpatialFrame[1]`).
To keep SysML unchanged, the `stateBodyItem` alternatives `entryActionMember`, `doActionMember`, and
`exitActionMember` now come before `nonBehaviorBodyItem`: with the keywords usable as names, an empty
`entry;` / `do;` / `exit;` would otherwise parse as a reference usage.

**Spec reference:** the KerML 1.0 textual BNF productions `NamespaceBodyElement`, `NamespaceMember`,
`NonFeatureMember`, `NamespaceFeatureMember`, `Step`, `Expression`, `BooleanExpression`, `Invariant`,
`FeatureTyping` (the `typing` relationship statement), and `FunctionBodyPart`. In KerML, `entry`, `do`,
`exit`, `accept`, and `frame` are not keywords.

**This patch is superseded when issue #89 adopts the official OMG grammar**, which defines these
productions itself; re-apply it (or drop it) when the upstream grammar is next updated.

## Regenerating the Parser

To regenerate `../../Parser/Antlr/` after a grammar update:

1. Download the updated `.g4` files from [daltskin/sysml-v2-grammar][grammar-repo]
   and replace the files in this folder.
2. Re-apply the patches listed in **Local Patches** above.
3. Obtain the ANTLR4 complete jar (the same version as `Antlr4.Runtime.Standard` in the `.csproj`,
   4.13.1) from
   <https://repo1.maven.org/maven2/org/antlr/antlr4/4.13.1/antlr4-4.13.1-complete.jar> (or
   <https://www.antlr.org/download.html>), and a JDK 11 or later. Neither needs to be installed system
   wide: a portable JDK (an unpacked zip build) and the jar can live in a temporary
   folder **outside the repository** — never commit them.
4. **Run from this Grammar folder** (not the repository root — ANTLR creates a
   nested subdirectory when invoked with relative paths from a parent directory):

   ```pwsh
   $java  = 'path\to\jdk\bin\java.exe'            # call the intended java.exe explicitly
   $antlr = 'path\to\antlr4-4.13.1-complete.jar'
   $out   = Resolve-Path '..\..\Parser\Antlr'
   Remove-Item "$out\*.cs","$out\*.interp","$out\*.tokens" -Force
   & $java -jar $antlr -Dlanguage=CSharp `
       -package DemaConsulting.SysML2Tools.Parser.Antlr `
       -listener -visitor -o $out `
       SysMLv2Lexer.g4 SysMLv2Parser.g4
   ```

   > **Important:** ANTLR must be invoked with the grammar directory as the working
   > directory. If invoked from a parent directory with relative `.g4` paths, ANTLR
   > mirrors the relative path structure inside `-o`, producing a nested subdirectory
   > instead of placing files directly in `$out`.

5. Verify that the header of the regenerated `SysMLv2Parser.cs` says `ANTLR Version: 4.13.1` (and
   `Generated from SysMLv2Parser.g4 by ANTLR 4.13.1`), and that `git status` shows only files in
   `Parser/Antlr/` (and no new subdirectory). Regenerating an unchanged grammar reproduces the
   committed files apart from line endings (the generator writes CRLF on Windows; Git normalizes).
6. Update the version table above.
7. Commit the regenerated files.

> **Note:** The `.interp` and `.tokens` files in `Antlr/` are not needed
> at runtime — the ANTLR4 ATN is serialized inline in the generated C# — but
> they are committed alongside the `.cs` files to keep regeneration reproducible.
