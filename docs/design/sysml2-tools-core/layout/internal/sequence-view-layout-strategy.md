#### SequenceViewLayoutStrategy

##### Purpose

`SequenceViewLayoutStrategy` lays out a Sequence View: it renders the interaction described by a
definition's messages as a set of vertical lifelines with header boxes and a horizontal arrow for
each message, ordered top-to-bottom by declaration order. Its single responsibility is to turn the
selected definition's messages into a positioned `LayoutTree`.

##### Data Model

The strategy is a stateless `ILayoutStrategy`. Inputs are a `ViewContext` (carrying the
`SysmlWorkspace`) and `RenderOptions` (carrying the `Theme`). It uses a private `MessageItem`
record holding the sender and receiver lifeline indices and the message label. Output is a
`LayoutTree` whose nodes are `LayoutLifeline` headers/stems and `LayoutLine` message arrows.

##### Key Methods

###### `BuildLayout(context, options)`

Builds the diagram:

1. **Root selection.** `FindRoot` scans the non-stdlib declarations and chooses the definition that
   declares the most `message` connections, so the most message-rich definition drives the view.
2. **Lifeline collection.** `CollectLifelines` walks the root's messages and records the distinct
   participants in first-appearance order, where a participant is the first dot-separated segment of
   a message endpoint reference (for example `client` from `client.a`). An index map from name to
   column is built alongside.
3. **Message resolution.** `ResolveMessages` maps each message's endpoints to lifeline indices,
   preserving declaration order and skipping messages whose endpoints do not resolve.
4. **Arithmetic placement.** Lifeline X is `margin + headerWidth/2 + columnIndex * pitch`, where
   `pitch` is computed by `ComputePitch` from the widest label (clamped to a minimum). Message Y is
   `firstMessageY + messageOrdinal * rowPitch`. Header height and margins derive from the theme.
5. **Node emission.** Each lifeline becomes a `LayoutLifeline`; each message becomes a horizontal
   `LayoutLine` with no source arrowhead and a filled target arrowhead, carrying the message label
   as its midpoint label. A message whose sender and receiver are the same lifeline is emitted by
   `BuildSelfMessage` as a small rectangular self-loop.

When no root is found, or there are no lifelines or messages, a minimal empty `LayoutTree` with no
nodes is returned.

##### Error Handling

Null `context` or `options` arguments throw `ArgumentNullException`. Empty or unresolved input does
not throw: the strategy returns an empty diagram rather than failing.

##### Dependencies

- `LayoutTree`, `LayoutLifeline`, `LayoutLine`, `Point2D`, `ArrowheadStyle`, `LineStyle`
  (Layout subsystem).
- `ViewContext`, `RenderOptions`, `Theme` (Rendering subsystem).
- `SysmlWorkspace`, `SysmlDefinitionNode`, `SysmlConnectionNode`, and `StdlibFilter`
  (Semantic subsystem).

##### Callers

The layout strategy registry selects `SequenceViewLayoutStrategy` when a Sequence View is
requested; it is not called directly by other units.
