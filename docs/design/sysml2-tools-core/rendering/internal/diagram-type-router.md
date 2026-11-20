#### DiagramTypeRouter

![Core Rendering Internal Structure](CoreRenderingInternalView.svg)

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

A `Views::`-qualified render target (e.g. `render Views::asTreeDiagram;`) is matched by its
simple name. Only `asTreeDiagram` and `asInterconnectionDiagram` are members of the standard
`Views` package; the other five tokens are SysML2Tools extensions (raw names, not valid as
`Views::`-qualified SysML), and the router merely tolerates a qualified spelling of them. SysML v2.0
does not implicitly import the `Views` package, so a valid model writes the qualified form of a
standard target or adds `private import Views::*;` and uses the bare name; the router treats
all three spellings identically (a bare name without an import is still tolerated, although
the OMG Pilot Implementation rejects it as an unresolved reference).

Routing is independent of load-time diagnostics: when one of the five extension tokens does not
resolve to a declaration, `ReferenceResolver` reports a portability Warning at load time, and the
router still dispatches on it unchanged. The Language library's `ReferenceResolver.ToolRenderSelectors`
table is the second copy of these five tokens (the Language library cannot reference Core), so the
two lists must be kept in sync.

When the view's own render target is not recognized, the router walks its view definitions
(`FromViewDefinitions`): the target of the view's resolved `Typing` edge (`view V : Def`), then
transitively the targets of each definition's `Supertype` edges, breadth-first and cycle-guarded.
The first definition reached that either declares a recognized `render` target or is one of the
standard view definitions (SysML v2.1 Beta 2 9.2.20) selects the strategy, because a view usage
inherits its definition's rendering (7.26.2):

- `StandardViewDefinitions::GeneralView` → general view strategy
- `StandardViewDefinitions::InterconnectionView` → interconnection strategy
- `StandardViewDefinitions::ActionFlowView` → action flow strategy
- `StandardViewDefinitions::StateTransitionView` → state transition strategy
- `StandardViewDefinitions::SequenceView` → sequence strategy
- `StandardViewDefinitions::GridView` → grid strategy
- `StandardViewDefinitions::BrowserView` → browser (tree) strategy

`GeometryView` has no strategy and is not mapped, so it falls through to the heuristic below.
Matching uses the resolved qualified name, so `view V : GeneralView` needs the standard library
name to be visible (for example through `private import StandardViewDefinitions::*;`) or written
qualified (`StandardViewDefinitions::GeneralView`).

When neither a render target nor a view definition selects a strategy, the router falls back to
testing the view's name and its
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
- `SysmlViewNode` and `SysmlWorkspace` (Semantic subsystem) for the view's identity, its
  resolved `Typing`/`Supertype` edges, and its view definitions' declarations.

##### Callers

`DiagramRenderer`, which calls `GetStrategy` once per view before building and rendering its
layout. `DynamicViewSynthesizer` also relies on this dispatch indirectly: it sets a synthesized
view's `RenderTargetName` to one of the exact-match tokens above so that `DiagramRenderer`'s
existing call to `GetStrategy` resolves to the requested layout strategy without any special
casing.
