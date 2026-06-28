### ConnectorLabelPlacer

#### Purpose

`ConnectorLabelPlacer` computes non-overlapping screen positions for connector (midpoint) labels.
Its single responsibility is to assign each labelled line a label centre that reads as belonging to
that connector while keeping labels from colliding with one another. Both the SVG and PNG renderers
share this unit so their label layouts match.

#### Data Model

`ConnectorLabelPlacer` is a static class with no instance state. Inputs are the lines to place
labels for (in render order) and the body font size. It uses a private `Rect` value type for label
overlap tests. Output is a dictionary mapping each labelled `LayoutLine` to its chosen `(X, Y)`
label centre; lines without a label are absent from the dictionary.

#### Key Methods

##### `Place(lines, fontSize)`

Iterates the lines in order, skipping any with no label or no waypoints. For each labelled line it
estimates the label box half-width and half-height from the text length and font size (plus a small
clearance gap), chooses a position via `ChoosePosition`, records the occupied box, and stores the
result. Processing lines in input order makes the result deterministic.

##### `ChoosePosition(waypoints, halfWidth, halfHeight, placed)`

Selects a label centre for one line:

1. The segment midpoints are ordered by descending segment length, so the longest (most open) run is
   preferred.
2. The first segment midpoint that does not overlap an already-placed label box is used.
3. If every segment midpoint collides, the label is nudged along the longest segment's perpendicular
   in increasing steps (alternating sides) until a clear position is found.
4. If no clear position is found, the longest segment's midpoint is used as a fallback.

A single-waypoint line places its label at that point. `Collides` performs the axis-aligned box
overlap test used throughout.

#### Error Handling

A null `lines` argument throws `ArgumentNullException`. Degenerate input never throws: lines with no
label or no waypoints are skipped, and a line whose labels cannot be separated falls back to its
longest-segment midpoint rather than failing.

#### Dependencies

- `LayoutLine` and `Point2D` (Layout subsystem).

#### Callers

The SVG and PNG renderers call `ConnectorLabelPlacer.Place` to position connector labels before
drawing them.
