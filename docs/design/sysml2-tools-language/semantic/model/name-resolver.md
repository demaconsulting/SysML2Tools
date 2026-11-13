#### NameResolver

##### Overview

`NameResolver` is a public class that resolves qualified-name text against a loaded workspace's
declarations following KerML 1.1 Beta 2 clause 8.2.3.5 (Name Resolution). `ReferenceResolver`
uses it — in an internal *resolving* mode — to resolve every reference of the semantic model, so
the resolved `SysmlEdge`s are themselves KerML-compliant (member visibility, filtered imports,
import scoping). It also serves callers that hold raw reference text the semantic model did not
resolve into a `SysmlEdge`: the Core layout strategies use it as the fallback after a feature's or
definition's resolved `Typing`/`Supertype` edge, and trust that edge when it exists. It is
constructed over `SysmlWorkspace.Declarations` (or the symbol table's declarations during
resolution) with an optional `importFilter` predicate, and caches lookups per instance, so one
instance serves one workspace.

##### Data Model

- Owning namespaces are derived from qualified names: the owner of `A::B::C` is `A::B`; an
  element with no `::` is a top-level element of its root namespace.
- Owned memberships of a namespace are its `Children` (excluding imports) plus any declaration
  whose qualified name is `<namespace>::<name>`. Each has a `Name`, an optional `ShortName`, and
  a `Visibility` (`SysmlVisibility`, default `Public`). `SysmlAliasNode` children contribute
  their names for their `TargetName`, resolved relative to the owning namespace.
- Imports are the namespace's `SysmlImportNode` children, with `Visibility` (default `Private`,
  per KerML 7.2.5.4/SysML 7.5.3), `IsImportAll`, `IsMembershipImport`, `IsRecursive`, and
  `BracketFilterExpressionText`.
- A type's general types (`GeneralTypes`) — a definition, feature, view, or viewpoint — are, in
  the public mode, its resolved `Supertype`/`Typing`/`Redefinition`
  edge targets; when it has fewer of those than raw references (e.g. standard-library elements,
  which are never resolved, or a supertype the semantic pass did not resolve), its raw
  `SupertypeNames`, `FeatureTyping`, and view definition are also resolved relative to its owning
  namespace (KerML 8.2.3.5.2: a Specialization's local namespace is the owning namespace of its
  owning type) and merged with the resolved targets, without duplicates. In the internal
  *resolving* mode `GeneralTypes` ignores `ResolvedEdges` entirely and derives the general types
  only from the raw references (`SupertypeNames`, `FeatureTyping`, the view definition name, and
  the `RedefinedFeatureName`), cached per type and guarded against re-entry (a specialization
  cycle contributes nothing). That makes every answer independent of the order in which the
  semantic pass visits nodes and keeps the lookup cache valid while edges are still being
  attached.
- The global namespace is every top-level declaration of every loaded root namespace, user and
  standard library alike (KerML 8.2.3.5.2).
- *File-root imports* (resolving mode only): the imports written at the root of a source file are
  not part of any namespace's `Children` in the declarations, so `RegisterRootImports` records
  them against the file's top-level declarations; full resolution consults them after the owning
  namespaces and before the global namespace. A top-level type has no owning namespace, so its own
  general types (and a top-level feature's redefined feature) are resolved with the imports
  registered for that type — otherwise a supertype reachable only through a file-root import would
  be missing from `GeneralTypes` and the inherited members it supplies would not resolve.
- `NameResolution(QualifiedName, IsSimpleNameFallback, Candidates)` reports a fallback result.

##### Key Methods

- `Resolve(reference, localNamespace)` (KerML 8.2.3.5.1): a single segment by full resolution; a
  `$::name` in the global namespace; otherwise the qualification part by this same process (it
  must yield a namespace), then the last segment by visible resolution in that namespace. An
  internal overload additionally accepts the file-root imports for a reference whose local
  namespace is the file root itself.
- `ResolveRedefinedFeature(reference, owningType)` (KerML 8.2.3.5.1, Redefinition): `Resolve`
  with each general type of `owningType` as the local namespace, in turn. A bare name is looked up
  in the general type itself with the **Inheritable** scope (the redefining feature inherits from
  it, so the general type's private members cannot be redefined, protected ones can be); the
  enclosing namespaces outward from it, and qualified names, resolve as in `Resolve`.
- `ResolveRedefinition(reference, owningType, redefiningFeature, fileRootImports)` (internal):
  `ResolveRedefinedFeature`, then — a documented lenient deviation — ordinary `Resolve` from the
  owning type; the redefining feature itself is never an acceptable target.
- `ResolveWithFallback(reference, localNamespace)` (tool extension, warned by callers): `Resolve`;
  when that fails, the last segment is matched against every declaration's simple name (first in
  declaration order) and the result is flagged with every candidate.
- `GeneralTypes(typeName)` (KerML 8.2.3.5.3, inherited memberships): the general types described
  under Data Model.

Lookup scopes, applied per namespace (KerML 8.2.3.5.3):

- **Local** (full resolution of a simple name, each namespace outward): every owned membership
  and alias, every import, and the non-private memberships inherited from general types.
- **Inheritable** (what a specialization inherits): non-private owned memberships and imports,
  recursively inherited non-private memberships.
- **Visible** (later qualified-name segments, and what an import brings in): public owned
  memberships, public imports, public inherited memberships. `import all` brings in every
  membership regardless of visibility (KerML 8.3.2.4.2 `isImportAll`).

An import brings in (KerML 8.3.2.4.4/8.3.2.4.6): for a membership import `import A::B;` the
membership `B` (by its last segment, its element's short name, or — when `B` is an alias — the
alias's own short name); if recursive (`A::B::**`),
also the visible members of `B` and its nested namespaces, least-nested first; for a namespace
import `import A::*;` the visible members of `A`; if recursive (`A::*::**`), also those of its
nested namespaces. A filtered import (`[expr]`) admits a candidate only when the `importFilter`
predicate accepts it (SysML 7.5.4); without a predicate, filter conditions are ignored. For a
recursive filtered import the predicate is applied while enumerating nested candidates, so a
rejected match does not stop the search for a later one that satisfies the conditions.

##### Error Handling

Unresolvable names return `null` (`Resolve`, `ResolveRedefinedFeature`) or an unresolved
`NameResolution`. Imports and specializations may be circular (KerML 8.2.3.5.1); a lookup already
in progress for the same (name, namespace, scope) contributes nothing instead of recursing, so
resolution always terminates. Null arguments throw `ArgumentNullException`.

##### Dependencies

- `SysmlNode` and its subclasses, `SysmlAliasNode`, `SysmlImportNode`, `SysmlVisibility`,
  `SysmlEdge`/`SysmlEdgeKind` (Semantic Model subsystem).

##### Callers

- `ReferenceResolver` (Language Semantic Model) resolves every reference of the semantic model with
  an internal resolving-mode instance (import filter from
  `FilterExpressionEvaluator.CreateImportFilter`, file-root imports registered per file).
- `FilterExpressionEvaluator.CreateNameResolver` (Language Filtering) constructs a public-mode one
  with a filtered-import predicate.
- `GeneralViewLayoutStrategy`, `InterconnectionViewLayoutStrategy`, and `GridViewLayoutStrategy`
  (Core Layout) use it to resolve the raw type, supertype, redefinition, and subsetting references
  the semantic pass left unresolved (a reference the semantic pass resolved is used as resolved).
  A reference KerML resolution rejects then reaches only the flagged simple-name fallback, which
  always warns — the layouts' documented policy for incomplete models.
