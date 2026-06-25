# AstBuilder

## Overview

`AstBuilder` extends `SysMLv2ParserBaseVisitor<SysmlNode?>` and builds a typed AST from the
ANTLR4 CST produced by `SysMLv2Parser`.

## Namespace Stack

A `List<string> _namespaceStack` tracks the current nesting path. When entering a named package
or definition, the name is pushed; it is popped before returning. `QualifyName(name)` joins the
stack with `::` to form the fully-qualified name.

## Key Visit Methods

| Method | Input Context | Output |
| --- | --- | --- |
| `VisitRootNamespace` | `RootNamespaceContext` | `SysmlPackageNode` (root) |
| `VisitPackage` | `PackageContext` | `SysmlPackageNode` |
| `VisitLibraryPackage` | `LibraryPackageContext` | `SysmlPackageNode` |
| `VisitPartDefinition` | `PartDefinitionContext` | `SysmlDefinitionNode` |
| `VisitAttributeDefinition` | `AttributeDefinitionContext` | `SysmlDefinitionNode` |
| `VisitItemDefinition` | `ItemDefinitionContext` | `SysmlDefinitionNode` |
| `VisitViewDefinition` | `ViewDefinitionContext` | `SysmlViewNode` |
| `VisitViewpointDefinition` | `ViewpointDefinitionContext` | `SysmlViewpointNode` |

## Name Extraction

`GetDeclaredName(IdentificationContext)` handles the three grammar alternatives:

- `< shortName > declaredName` (alt 1): returns `name(1).GetText()`.
- `< shortName >` (alt 2): no declared name — returns null.
- `declaredName` (alt 3): returns `name(0).GetText()`.

Elements with no declared name are treated as anonymous and are not registered in the symbol table.

## Supertype Extraction

`GetSubclassificationSupertypes(SubclassificationPartContext)` iterates
`ownedSubclassification()` entries and calls `qualifiedName().GetText()` on each to produce
the supertype name list.
