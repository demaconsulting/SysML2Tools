### StdlibProvider

#### Purpose

`StdlibProvider` is the single unit of the `DemaConsulting.SysML2Tools.Stdlib` system. It exposes the
pre-compiled SysML v2 standard library as a `SymbolTable` that callers seed into a workspace, avoiding
a cold-start parse of the standard library source files.

#### Data Model

`StdlibProvider` is a static class. The deserialized standard library (`SymbolTable` plus diagnostics)
is held in a lazily-initialised, cached field so the embedded binary is deserialized at most once per
application domain and shared by every caller.

#### Key Methods

##### `GetSymbolTable()`

On first call, reads the `stdlib.bin` resource embedded in the assembly, deserializes it with
`AstDeserializer` into a `SymbolTable` and a diagnostics list, and caches the result. Subsequent calls
return the cached instance directly. Returns the standard library `SymbolTable` together with the
diagnostics collected during pre-compilation.

#### Dependencies

- **`AstDeserializer` (Language)** — deserializes the embedded binary into a `SymbolTable`.
- **Embedded `stdlib.bin`** — the pre-compiled standard library produced at build time by StdlibGen.

#### Callers

- `WorkspaceLoader` (Language) seeds a workspace with the returned `SymbolTable`.
- The Tool's lint and render commands obtain the standard library through the loader.
- Tests call `GetSymbolTable` directly and assert on the returned table and diagnostics.
