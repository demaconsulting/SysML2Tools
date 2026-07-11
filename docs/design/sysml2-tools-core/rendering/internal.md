### Rendering Internal Subsystem

#### Overview

The Internal sub-subsystem of Rendering holds the implementation details that the public
rendering pipeline relies on but does not expose: selecting a layout strategy for each view,
excluding standard-library elements from user-facing diagrams, and synthesizing an ad-hoc view
node for any resolvable target on request. It contains the `DiagramTypeRouter` and
`DynamicViewSynthesizer` units. The stdlib exclusion is performed with the `StdlibFilter` helper,
which is documented in the Rendering subsystem design.

#### Interfaces

The components are internal and are consumed only by other Core types. `DiagramTypeRouter`
exposes a single `GetStrategy` method returning an `ILayoutStrategy`. `DynamicViewSynthesizer`
exposes a single `Synthesize` method returning a `(SysmlViewNode?, string?)` tuple. The view
strategies consume the `StdlibFilter.IsStdlibElement` predicate to decide whether an element
belongs to the standard library.

#### Design

`DiagramTypeRouter` first checks a view's declared `render` target for an exact match against
`asTreeDiagram` (browser/tree strategy), `asInterconnectionDiagram` (interconnection strategy),
`asGeneralDiagram` (general view strategy), `asStateTransitionDiagram` (state transition
strategy), `asActionFlowDiagram` (action flow strategy), `asSequenceDiagram` (sequence strategy),
or `asGridDiagram` (grid strategy), taking precedence over the name/supertype heuristic; other
render targets (including `asElementTable`, `asTextualNotation`, or none) have no effect. Absent
a recognized render target, it inspects a view's name and declared supertype names for a
recognized view kind (interconnection, state transition, action flow, grid/matrix, browser/tree,
sequence) and returns the matching strategy, defaulting to the general view strategy. To keep
diagrams focused on the user's model, the view strategies omit standard-library elements by
testing each qualified name with the `StdlibFilter` helper.

`DynamicViewSynthesizer` builds an in-memory `SysmlViewNode` targeting any resolvable, non-stdlib
element in a workspace, without requiring the user to add a `view def` to the model — the engine
behind the `render --view-type <kind> --view-target <qualified-name> [--filter <expr>]` CLI
feature. It maps `--view-type` to one of `DiagramTypeRouter`'s exact-match `RenderTargetName`
tokens above, resolves and validates the target, runs a cheap per-kind structural compatibility
pre-check, and constructs a node scoped to the target via a single manually-populated `Expose`
edge — the same mechanism a real, parsed `view def V { expose Target; }` produces.

The `DiagramTypeRouter` and `DynamicViewSynthesizer` units are each described in their own
chapter.
