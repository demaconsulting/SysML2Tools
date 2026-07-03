#### SemanticIndex

##### Overview

`SemanticIndex` is a reverse-lookup index over the resolved `SysmlEdge` collection produced
by `ReferenceResolver.ResolveAll`. It answers "what does X reference" (outgoing) and "what
references X" (incoming) queries, forming the foundation for the `query` command's
`uses`/`used-by`/`impact`/`hierarchy` verbs added in later units.

##### Algorithm

The constructor takes an `IEnumerable<SysmlEdge>`, materializes it into a `List<SysmlEdge>`
(`AllEdges`), and builds two `Dictionary<string, List<SysmlEdge>>` lookups (`Ordinal`
comparer):

- `_outgoing` — keyed by `SourceQualifiedName`, populated only for edges with a non-null,
  non-empty source (anonymous-source edges, e.g. unnamed import statements, are omitted from
  outgoing lookups but still appear in `AllEdges` and in incoming lookups).
- `_incoming` — keyed by `TargetQualifiedName`, populated for every edge.

##### Lookup

`GetOutgoingEdges(string qualifiedName)` and `GetIncomingEdges(string qualifiedName)` each
return the matching list, or `Array.Empty<SysmlEdge>()` when no edges are recorded for the
given qualified name — callers never receive `null`.

##### Error Handling

The constructor throws `ArgumentNullException` if `edges` is `null`. Lookup methods never
throw; an unknown qualified name simply returns an empty list.

##### Dependencies

- `SysmlEdge` — the edge records being indexed.

##### Callers

- `ReferenceResolver.ResolveAll` — constructs the index once per resolution pass over the
  accumulated edge list and returns it to the caller.
- `WorkspaceLoader.LoadAsync` — assigns the returned index to `SysmlWorkspace.Index`.
- `SysmlWorkspace` — exposes the index as a public property (default empty when no
  resolution has run, e.g., for a workspace built only from a stdlib seed).
