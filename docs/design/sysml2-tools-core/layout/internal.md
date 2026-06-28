### Layout Internal Subsystem

#### Overview

The Internal subsystem provides the per-view layout strategies — the implementations of
`ILayoutStrategy` that turn the SysML semantic model into a `LayoutTree` for one diagram view.
Where the Engine subsystem solves geometric sub-problems from plain input, the Internal
strategies own the mapping from the semantic model to geometric input and back: they select
the relevant model elements, size and place the boxes, route the connectors, and assemble the
node tree the renderers consume.

The subsystem contains one strategy per supported view type:

| Unit | Responsibility |
| --- | --- |
| `GeneralViewLayoutStrategy` | Lays out user definitions grouped by package with specialization edges |
| `InterconnectionViewLayoutStrategy` | Lays out the internal parts, ports, and connections of one part definition |
| `StateTransitionViewLayoutStrategy` | Lays out states, an initial marker, and guarded transitions |
| `ActionFlowViewLayoutStrategy` | Lays out actions top-to-bottom with start/done markers and successions |

The subsystem also contains the `BrowserViewLayoutStrategy`, `GridViewLayoutStrategy`, and
`SequenceViewLayoutStrategy` strategies and the `LayoutWarnings` helper, each documented in its
own chapter.

#### Interfaces

Each strategy exposes the single `ILayoutStrategy.BuildLayout(ViewContext, RenderOptions)`
method. It consumes the semantic workspace through `ViewContext` and the theme and render
options through `RenderOptions`, and returns a `LayoutTree`. The strategies are the only
consumers of both the semantic model and the geometric engines; the renderers see only the
returned tree.

#### Design

Each strategy follows the same shape: collect the relevant model elements (excluding
standard-library declarations), compute an intrinsic size for each box, delegate placement and
routing to the geometric engines of the Engine subsystem, and build the `LayoutNode` tree. When
a connector cannot be routed without crossing a box, the strategy records a layout warning
through `LayoutWarnings` rather than silently producing a misleading diagram. A view with no
relevant elements returns a minimal empty canvas. The detailed mapping and heuristics of each
strategy are described in its own unit chapter.
