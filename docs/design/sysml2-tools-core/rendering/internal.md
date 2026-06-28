### Rendering Internal Subsystem

#### Overview

The Internal sub-subsystem of Rendering holds the implementation details that the public
rendering pipeline relies on but does not expose: selecting a layout strategy for each view
and filtering out standard-library elements. It contains the `DiagramTypeRouter` unit and the
`StdlibFilter` helper.

#### Interfaces

The components are internal and are consumed only by other Core types. `DiagramTypeRouter`
exposes a single `GetStrategy` method returning an `ILayoutStrategy`. `StdlibFilter` exposes a
predicate used by the view strategies to decide whether an element belongs to the standard
library.

#### Design

`DiagramTypeRouter` inspects a view's name and declared supertype names for a recognized view
kind (interconnection, state transition, action flow, grid/matrix, browser/tree, sequence) and
returns the matching strategy, defaulting to the general view strategy. `StdlibFilter` answers
whether a qualified name belongs to the set of standard-library names carried by the workspace,
so the view strategies can omit those elements. `StdlibFilter` is a stateless predicate helper
with no behavior of its own beyond the membership test, and is verified indirectly through the
view-strategy tests that assert standard-library elements are excluded; it is therefore
documented here rather than as a separate unit. The `DiagramTypeRouter` unit is described in
its own chapter.
