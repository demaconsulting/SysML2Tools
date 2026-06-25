# SysmlNode — AST Node Hierarchy

## Overview

`SysmlNode` is the abstract base class for all SysML/KerML AST nodes. Concrete subtypes represent
packages, definitions, features, imports, views, and viewpoints.

## Class Hierarchy

| Class | Purpose |
| --- | --- |
| `SysmlNode` (abstract) | Base: Name, QualifiedName, Children, SupertypeNames, ImportedNames |
| `SysmlPackageNode` | Package or namespace declaration |
| `SysmlDefinitionNode` | Definition element (part def, attribute def, etc.); adds DefinitionKeyword |
| `SysmlFeatureNode` | Feature/usage element |
| `SysmlImportNode` | Import declaration; adds ImportedNamespace, IsWildcard |
| `SysmlViewNode` | View definition |
| `SysmlViewpointNode` | Viewpoint definition |

## Properties

All nodes carry:

- `Name` — simple (unqualified) name, or null if anonymous.
- `QualifiedName` — fully-qualified name in containing namespace.
- `Children` — nested AST nodes.
- `SupertypeNames` — qualified names of supertypes referenced via `specializes` / `:>`.
- `ImportedNames` — qualified names of imported namespaces.
