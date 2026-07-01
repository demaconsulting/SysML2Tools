### AstSerializer Verification

#### Verification Approach

`AstSerializer` is verified by round-trip unit tests in `AstSerializerTests` in the
`DemaConsulting.SysML2Tools.Tests` project. Each test builds a `SymbolTable` (and, where relevant,
diagnostics), serializes it with `AstSerializer.Serialize`, deserializes the bytes with
`AstDeserializer.Deserialize`, and asserts that the restored symbols and diagnostics match the
originals. Round-trip testing verifies serialization and deserialization together.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Symbol tables are constructed
in-memory; no files on disk, network access, or additional configuration are required beyond a
standard .NET SDK installation.

#### Acceptance Criteria

- An empty symbol table round-trips to an empty symbol table.
- Package and definition nodes round-trip preserving their qualified names.
- All six concrete node types round-trip as the same concrete types.
- Supertype names and imported names are preserved through the round trip.
- Diagnostics are preserved through the round trip.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Serialize_EmptyTable_RoundTrips` | Empty table restores to an empty table |
| `Serialize_PackageNode_RoundTrips` | A package node restores with its qualified name |
| `Serialize_DefinitionNode_RoundTrips` | A definition node restores with its qualified name |
| `Serialize_AllNodeTypes_RoundTrip` | Every concrete node type restores as the same type |
| `Serialize_SupertypeAndImportedNames_Preserved` | Supertype and imported names are preserved |
| `Serialize_Diagnostics_RoundTrip` | Diagnostics are preserved through the round trip |
