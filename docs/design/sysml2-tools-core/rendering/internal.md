### Rendering Internal Subsystem

#### Overview

The Internal sub-subsystem of Rendering holds the implementation details that the public
rendering pipeline relies on but does not expose: selecting a layout strategy for each view
and excluding standard-library elements from user-facing diagrams. It contains the
`DiagramTypeRouter` unit. The stdlib exclusion is performed with the `StdlibFilter` helper, which
is documented in the Rendering subsystem design.

#### Interfaces

The components are internal and are consumed only by other Core types. `DiagramTypeRouter`
exposes a single `GetStrategy` method returning an `ILayoutStrategy`. The view strategies consume
the `StdlibFilter.IsStdlibElement` predicate to decide
whether an element belongs to the standard library.

#### Design

`DiagramTypeRouter` first checks a view's declared `render` target for an exact match against
`asTreeDiagram` (browser/tree strategy) or `asInterconnectionDiagram` (interconnection strategy),
taking precedence over the name/supertype heuristic; other render targets (including
`asElementTable`, `asTextualNotation`, or none) have no effect. Absent a recognized render
target, it inspects a view's name and declared supertype names for a recognized view kind
(interconnection, state transition, action flow, grid/matrix, browser/tree, sequence) and
returns the matching strategy, defaulting to the general view strategy. To keep diagrams focused
on the user's model, the view strategies omit standard-library elements by testing each qualified
name with the `StdlibFilter` helper. The `DiagramTypeRouter` unit is described in its own chapter.
