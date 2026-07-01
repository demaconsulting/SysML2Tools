### AstDeserializer Verification

#### Verification Approach

`AstDeserializer` is verified by the round-trip unit tests in `AstSerializerTests` in the
`DemaConsulting.SysML2Tools.Tests` project. Every round-trip test calls
`AstDeserializer.Deserialize` on the bytes produced by `AstSerializer.Serialize` and asserts that
the restored `SymbolTable` and diagnostics match the originals, exercising the deserialization path
directly.

#### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. Byte arrays are produced in-memory
by `AstSerializer`; no files on disk, network access, or additional configuration are required
beyond a standard .NET SDK installation.

#### Acceptance Criteria

- Deserializing the bytes of an empty table yields an empty symbol table.
- Deserializing package and definition nodes restores their qualified names.
- Deserializing restores every concrete node type as the same type it was serialized from.
- Supertype names, imported names, and diagnostics are restored to match the serialized input.

#### Test Scenarios

| Test | Assertion |
| --- | --- |
| `Serialize_EmptyTable_RoundTrips` | Bytes of an empty table deserialize to an empty table |
| `Serialize_PackageNode_RoundTrips` | A package node deserializes with its qualified name |
| `Serialize_AllNodeTypes_RoundTrip` | Every concrete node type deserializes as the same type |
| `Serialize_SupertypeAndImportedNames_Preserved` | Supertype and imported names are restored |
| `Serialize_Diagnostics_RoundTrip` | Diagnostics are restored through deserialization |
