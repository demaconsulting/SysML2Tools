## Parser

### Overview

N/A — The Parser subsystem and all its units (`WorkspaceParser`, `SysmlDiagnosticListener`)
have been relocated to the `DemaConsulting.SysML2Tools.Language` library as part of the
Phase 5 architecture refactor. `StdlibLoader` has been deleted; stdlib loading
responsibilities now belong to `DemaConsulting.SysML2Tools.Stdlib` and the `StdlibGen`
build-time tool. The `DemaConsulting.SysML2Tools` core library does not contain a Parser
subsystem. See _DemaConsulting.SysML2Tools.Language Design_.

### Interfaces

N/A — All Parser interfaces (`WorkspaceParser.ParseSource`, `WorkspaceParser.ParseSourceToCst`,
`WorkspaceParseResult.HasErrors`) have been relocated to `DemaConsulting.SysML2Tools.Language`.
`WorkspaceParser.ParseAsync` has been removed; its responsibilities are now split between
`StdlibProvider.GetSymbolTable()` (in `DemaConsulting.SysML2Tools.Stdlib`) and
`WorkspaceLoader.LoadAsync(filePaths, seedSymbolTable)` (in
`DemaConsulting.SysML2Tools.Language`). See _DemaConsulting.SysML2Tools.Language Design_.

### Design

N/A — Parser design is documented in _DemaConsulting.SysML2Tools.Language Design_.
`StdlibLoader` has been deleted. `WorkspaceParser.ParseAsync` with its stdlib
`Lazy<Task<>>` caching has been removed and replaced by the `StdlibProvider`/
`AstDeserializer` pattern in `DemaConsulting.SysML2Tools.Stdlib`.
