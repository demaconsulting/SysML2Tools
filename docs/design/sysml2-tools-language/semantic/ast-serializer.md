### AstSerializer

#### Purpose

`AstSerializer` converts a populated `SymbolTable` and its diagnostics into a portable UTF-8 JSON
byte array. It exists so the SysML/KerML standard library can be pre-compiled once at build time
(by the `StdlibGen` tool) and embedded as a binary resource, avoiding repeated source parsing at
run time.

#### Data Model

`AstSerializer` is a static class with no instance state. It relies on two grouped internal types
declared in the `Semantic/Internal` namespace:

- **`SerializedStdlib`** — an internal DTO record pairing the symbol dictionary
  (`Dictionary<string, SysmlNode>`) with the diagnostics list. It is the on-the-wire shape written
  to and read from the binary and is documented here rather than in its own chapter because it has
  no behavior of its own.
- **`AstSerializerContext`** — a source-generated `JsonSerializerContext` that provides AOT-safe,
  reflection-free serialization metadata for `SerializedStdlib` and the node dictionary. It is
  likewise grouped into this chapter as a serialization support type with no independent behavior.

#### Key Methods

##### `Serialize(SymbolTable table, IReadOnlyList<SysmlDiagnostic> diagnostics)`

1. Validates that neither argument is null.
2. Copies the table's `Symbols` into a `SerializedStdlib` DTO alongside the diagnostics.
3. Calls `JsonSerializer.SerializeToUtf8Bytes` using `AstSerializerContext.Default.SerializedStdlib`
   and returns the resulting `byte[]`.

JSON polymorphism attributes on `SysmlNode` write a `$type` discriminator for every concrete node
subtype so that `AstDeserializer` can reconstruct the correct types.

#### Dependencies

- **SymbolTable** — source of the `Symbols` dictionary being serialized.
- **SysmlNode** — the polymorphic node hierarchy serialized within each symbol entry.
- **System.Text.Json** — performs the UTF-8 JSON serialization via the source-generated context.

#### Callers

The `StdlibGen` build-time tool calls `Serialize` to produce the embedded `stdlib.bin` resource.
