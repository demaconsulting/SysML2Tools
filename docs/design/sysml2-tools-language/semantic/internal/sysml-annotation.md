#### SysmlAnnotation

##### Overview

`SysmlAnnotation` and `SysmlAnnotationKind` model a single captured `comment` or `doc`
annotating-element body attached to the AST node it lexically annotates. Annotations are
produced by `AstBuilder` while visiting `annotatingElement` contexts nested in a package,
definition, feature, action, or state body, and are attached to the owning node's
`SysmlNode.Annotations` list in source order.

##### Types

`SysmlAnnotationKind` is an enum with two members:

- `Comment` — free text from a `comment` annotating element.
- `Documentation` — free text from a `doc` annotating element.

`SysmlAnnotation` is a sealed positional record with two properties:

- `Kind` (`SysmlAnnotationKind`) — the kind of annotation this text was captured from.
- `Text` (`string`) — the raw annotation text with the surrounding `/*`/`*/` (or `//*`/`*/`)
  comment delimiters removed, preserved verbatim otherwise (no re-indentation or trimming of
  interior whitespace, newlines, or `*` bullet characters).

##### Error Handling

N/A — `SysmlAnnotation` is a pure data record with no logic or validation.

##### Dependencies

- No external dependencies. Public types within the `Semantic.Internal` namespace.

##### Callers

- `AstBuilder` — constructs `SysmlAnnotation` instances in `VisitAnnotatingElement` and
  attaches them (via the `AnnotationCapture` sentinel and the body-collection helpers) to
  `SysmlNode.Annotations` on the owning node.

##### Known Limitations

- An annotating element with an explicit `about X` target is still attached to its lexically
  enclosing node rather than to the referenced element `X`; resolving explicit `about` targets
  is deferred to a future unit.
- Comments/docs nested inside a relationship body (e.g.
  `alias Car for Automobile { /* ... */ }`) are not captured, since no `AstBuilder` visitor
  currently collects relationship bodies (`relationshipBody`).
- `textualRepresentation` and `metadataFeature` (the other two `annotatingElement`
  alternatives) remain unhandled, unchanged from prior behavior.
