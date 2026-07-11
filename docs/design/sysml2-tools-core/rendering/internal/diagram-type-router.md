#### DiagramTypeRouter

##### Purpose

`DiagramTypeRouter` selects the `ILayoutStrategy` to use for a given view, so the renderer can
treat every view kind uniformly. It is the single dispatch point that maps a view to its
diagram type.

##### Data Model

`DiagramTypeRouter` is a static class with no instance state. Its input is a view node and the
workspace; its output is an `ILayoutStrategy` instance, with an `out string?` carrying a
diagnostic message when no strategy can be determined.

##### Key Methods

###### `GetStrategy(viewNode, workspace, out unsupportedMessage)`

Returns the strategy for the view. Dispatch first checks the view's declared `render` target
(`SysmlViewNode.RenderTargetName`) for an exact, case-sensitive (`StringComparison.Ordinal`)
match against a recognized rendering-kind name, taking precedence over the name/supertype
heuristic below regardless of the view's own name or declared supertypes:

- `asTreeDiagram` → browser (tree) strategy
- `asInterconnectionDiagram` → interconnection strategy
- `asGeneralDiagram` → general view strategy
- `asStateTransitionDiagram` → state transition strategy
- `asActionFlowDiagram` → action flow strategy
- `asSequenceDiagram` → sequence strategy
- `asGridDiagram` → grid strategy

The latter five tokens are additive: they give every remaining layout strategy the same
explicit, precedence-taking dispatch path already enjoyed by `asTreeDiagram`/
`asInterconnectionDiagram`. They are also the exact tokens the dynamic (ad-hoc) view CLI feature
(`render --view-type <kind> --view-target <name>`, implemented by `DynamicViewSynthesizer`)
relies on to select a layout strategy independently of the target's own name or supertypes —
see `dynamic-view-synthesizer.md`.

`asElementTable`, `asTextualNotation`, any other unrecognized rendering-kind name, and a `null`
(absent) render target are deliberately left unmapped: they have no effect and fall through
unchanged, with no diagnostic, to the name/supertype heuristic. `asElementTable` is left
unmapped because its `TabularRendering` semantics (a per-row/per-column table composition) are
fundamentally different from `GridViewLayoutStrategy`'s matrix layout, not merely a naming
variant of it; `asTextualNotation` is left unmapped because it is a non-graphical rendering
style with no corresponding `ILayoutStrategy` implementation.

When no render target matches, the router falls back to testing the view's name and its
declared supertype names (case-insensitively) for a recognized view-kind marker, in a fixed
priority order: Interconnection, then StateTransition/State, then ActionFlow/Action, then
Grid/Matrix/Tabular, then Browser/Tree, then Sequence. The first marker that matches selects the
corresponding strategy. When no marker matches — or the node is not a view — the router returns
the general view strategy. The fixed order resolves views that carry more than one marker
deterministically.

##### Error Handling

The router never throws for an unrecognized view; it returns the general view strategy as a
safe default. The `unsupportedMessage` out-parameter is reserved for future view kinds that
cannot be rendered; it is currently always null because every view resolves to a strategy.

##### Dependencies

- The view-strategy units in the Layout Internal subsystem (the strategies it returns).
- `SysmlViewNode` and `SysmlWorkspace` (Semantic subsystem) for the view's identity.

##### Callers

`DiagramRenderer`, which calls `GetStrategy` once per view before building and rendering its
layout. `DynamicViewSynthesizer` also relies on this dispatch indirectly: it sets a synthesized
view's `RenderTargetName` to one of the exact-match tokens above so that `DiagramRenderer`'s
existing call to `GetStrategy` resolves to the requested layout strategy without any special
casing.
