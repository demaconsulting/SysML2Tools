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

Returns the strategy for the view. When the node is a view, the router tests the view's name
and its declared supertype names (case-insensitively) for a recognized view-kind marker, in a
fixed priority order: Interconnection, then StateTransition/State, then ActionFlow/Action, then
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
layout.
