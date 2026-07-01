#### SysmlNode — AST Node Hierarchy

##### Overview

`SysmlNode` is the abstract base class for all SysML/KerML AST nodes. Concrete subtypes represent
packages, definitions, features, imports, views, viewpoints, connections, and transitions.

##### Class Hierarchy

| Class | Purpose |
| --- | --- |
| `SysmlNode` (abstract) | Base: Name, QualifiedName, Children, SupertypeNames, ImportedNames |
| `SysmlPackageNode` | Package or namespace declaration |
| `SysmlDefinitionNode` | Definition element (part def, attribute def, etc.); adds DefinitionKeyword |
| `SysmlFeatureNode` | Feature/usage element |
| `SysmlImportNode` | Import declaration; adds ImportedNamespace, IsWildcard |
| `SysmlViewNode` | View definition |
| `SysmlViewpointNode` | Viewpoint definition |
| `SysmlConnectionNode` | Connection/binding usage between two endpoints; adds ConnectionKeyword, EndpointA, EndpointB |
| `SysmlTransitionNode` | State transition; adds Source, Target, Guard |

##### Properties

All nodes carry:

- `Name` — simple (unqualified) name, or null if anonymous.
- `QualifiedName` — fully-qualified name in containing namespace.
- `Children` — nested AST nodes.
- `SupertypeNames` — qualified names of supertypes referenced via `specializes` / `:>`.
- `ImportedNames` — qualified names of imported namespaces.

##### Key Methods

All node types use C# `init`-only properties and are constructed via object initializers.
There are no behavioral methods beyond the inherited `object` members. `SysmlImportNode` adds:

- `ImportedNamespace` — the target namespace string extracted by `ReferenceResolver`.
- `IsWildcard` — `true` if the import ends with `::*`.

`SysmlDefinitionNode` adds:

- `DefinitionKeyword` — the grammar keyword string (e.g., `"part def"`, `"attribute def"`).

`SysmlConnectionNode` adds:

- `ConnectionKeyword` — the connection keyword (e.g., `"connection"`, `"binding"`).
- `EndpointA` — the first endpoint reference (e.g., `"engine.fuelPort"`), or null when unresolved.
- `EndpointB` — the second endpoint reference (e.g., `"transmission.input"`), or null when unresolved.

`SysmlTransitionNode` adds:

- `Source` — the source state reference, or null when implied by the containing state.
- `Target` — the target state reference.
- `Guard` — the guard expression text (the condition after `if`), or null when unguarded.

##### Error Handling

N/A — node types are pure data containers with no logic or validation. Invalid or anonymous
elements are filtered out by `AstBuilder` before a node is constructed.

##### Dependencies

- No external dependencies. All node types are internal sealed classes or the abstract base class
  within the `Semantic.Internal` namespace.

##### Callers

- `AstBuilder` — constructs all concrete node instances during CST visitor traversal.
- `SymbolTable` — traverses the node hierarchy via `Children`; reads `QualifiedName`.
- `ReferenceResolver` — reads `SupertypeNames`, `Children`; checks for `SysmlImportNode`.
- `SupertypeWalker` — reads `SupertypeNames` on each node retrieved from `SymbolTable`.
