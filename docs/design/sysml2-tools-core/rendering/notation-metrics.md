### NotationMetrics

#### Purpose

`NotationMetrics` is the single home for all intrinsic, theme-independent notation geometry shared by
the SVG and PNG renderers: end-marker (arrowhead) shapes and sizes, port squares, folder-tab
proportions, note dog-ear folds, rounded-rectangle corner scaling, badge fractions, and the
label-background inset. It is the notation-geometry peer of `BoxMetrics`: every value is either a
documented primitive notation constant or a documented derivation of those primitives, so a geometry
literal never appears more than once in the rendering path.

#### Data Model

`NotationMetrics` is a static class with no instance state. It exposes a `readonly record struct
MarkerVertex(double Along, double Across)` that expresses one marker vertex in tip-relative notation
units: `Along` is the distance measured back from the line endpoint into the line, and `Across` is
the perpendicular offset. All other members are public `const double` notation constants grouped by
marker family (triangle, diamond, circle, bar, crossbar), plus port, folder-tab, note-fold,
rounded-rectangle, badge, and label-background constants.

The canonical values are the historical SVG marker values — triangle 10x7 with `refX` 9, diamond 14x8
with `refX` 13, circle 10x10 radius 4, bar 4x12 — and every PNG size is derived from the same
constants so the two renderers draw the identical shape.

#### Key Methods

##### `TriangleVertices()` and `DiamondVertices()`

Return the shared marker outlines in tip-relative notation units. The triangle (base corner, apex,
base corner) is used by the open chevron, hollow triangle, and filled arrow markers; its apex sits
one `EndMarkerTipOvershoot` beyond the endpoint. The diamond (near, side, far, side) is used by the
hollow and filled diamond markers; its far point lands exactly on the line endpoint.

##### `AlongLineLength(style)`

Returns the along-line length consumed by an end-marker decoration (the marker-box length for that
style, or `0.0` for `EndMarkerStyle.None`). The layout strategies use it to reserve a clean approach
and the renderers use it to clamp the final corner radius.

##### `RoundedRectRadius(theme)`

Returns the rounded-rectangle corner radius for a box: `theme.LineCornerRadius` scaled by
`RoundedRectCornerFactor`. Throws `ArgumentNullException` for a null theme.

#### Error Handling

`RoundedRectRadius` throws `ArgumentNullException` for a null `theme`. All other members are constants
or pure functions over enum inputs and cannot fail.

#### Dependencies

- `Theme` (Rendering subsystem) — supplies the base line-corner radius for `RoundedRectRadius`.
- `EndMarkerStyle` (Layout subsystem) — selects the marker family in `AlongLineLength`.

#### Callers

The SVG and PNG renderers read these constants and call `TriangleVertices`, `DiamondVertices`, and
`RoundedRectRadius` when drawing end markers and rounded boxes. The layout strategies call
`AlongLineLength` to reserve connector approach space.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-NotationMetrics-CanonicalConstants | The named notation constants for every marker family |
| SysML2Tools-Core-Rendering-NotationMetrics-MarkerVertices | `TriangleVertices()` and `DiamondVertices()` |
| SysML2Tools-Core-Rendering-NotationMetrics-AlongLineLength | `AlongLineLength(EndMarkerStyle)` |
| SysML2Tools-Core-Rendering-NotationMetrics-RoundedRectRadius | `RoundedRectRadius(Theme)` |
