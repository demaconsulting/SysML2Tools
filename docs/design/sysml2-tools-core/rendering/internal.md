### Rendering Internal Subsystem

#### Overview

The Internal sub-subsystem of Rendering holds the implementation details that the public
rendering pipeline relies on but does not expose: selecting a layout strategy for each view
and excluding standard-library elements from user-facing diagrams. It contains the
`DiagramTypeRouter` unit. The stdlib exclusion is performed with the `StdlibFilter` helper, which
is grouped with the rendering contracts and documented in the *RenderingContracts* chapter.

#### Interfaces

The components are internal and are consumed only by other Core types. `DiagramTypeRouter`
exposes a single `GetStrategy` method returning an `ILayoutStrategy`. The view strategies consume
the `StdlibFilter.IsStdlibElement` predicate (owned by the RenderingContracts unit) to decide
whether an element belongs to the standard library.

#### Design

`DiagramTypeRouter` inspects a view's name and declared supertype names for a recognized view
kind (interconnection, state transition, action flow, grid/matrix, browser/tree, sequence) and
returns the matching strategy, defaulting to the general view strategy. To keep diagrams focused
on the user's model, the view strategies omit standard-library elements by testing each qualified
name with the `StdlibFilter` helper (grouped with the rendering contracts; see the
*RenderingContracts* chapter). The `DiagramTypeRouter` unit is described in its own chapter.
