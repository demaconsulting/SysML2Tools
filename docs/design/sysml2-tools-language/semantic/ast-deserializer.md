### AstDeserializer

#### Purpose

`AstDeserializer` restores a `SymbolTable` and its diagnostics from a UTF-8 JSON byte array that
was produced by `AstSerializer`. It is the run-time counterpart of `AstSerializer`, letting the
pre-compiled standard library be reloaded from its embedded binary resource without re-parsing the
source files.

#### Data Model

`AstDeserializer` is a static class with no instance state. It reads the same grouped
`SerializedStdlib` DTO and `AstSerializerContext` source-generated context described in the
`AstSerializer` chapter; those types are shared between the two units and are documented there.

#### Key Methods

##### `Deserialize(byte[] data)`

1. Validates that `data` is not null.
2. Calls `JsonSerializer.Deserialize` with `AstSerializerContext.Default.SerializedStdlib`,
   throwing `InvalidOperationException` if deserialization yields null.
3. Reconstructs a `SymbolTable` from the DTO's `Symbols` dictionary via the copy constructor and
   returns it together with the DTO's diagnostics as a tuple
   `(SymbolTable Table, IReadOnlyList<SysmlDiagnostic> Diagnostics)`.

The `$type` discriminator written during serialization drives the reconstruction of each concrete
`SysmlNode` subtype.

#### Dependencies

- **SymbolTable** — reconstructed from the deserialized symbol dictionary.
- **SysmlNode** — the polymorphic node hierarchy restored within each symbol entry.
- **System.Text.Json** — performs the UTF-8 JSON deserialization via the source-generated context.

#### Callers

`StdlibProvider` in `DemaConsulting.SysML2Tools.Stdlib` calls `Deserialize` to load the embedded
`stdlib.bin` resource into a cached `SymbolTable`.
