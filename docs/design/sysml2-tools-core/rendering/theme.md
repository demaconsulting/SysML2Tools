### Theme

#### Purpose

`Theme` is the immutable visual-configuration record that gathers every appearance parameter for a
rendered diagram into a single object, so the whole look of a diagram can be changed by substituting
one `Theme`. The companion static `Themes` provider exposes the three built-in themes (`Light`,
`Dark`, `Print`). The subsystem-level contract for the record's fields is described in the *Rendering
Subsystem* chapter; this unit chapter focuses on the connector-geometry members and the built-in
theme values that the unit tests pin.

#### Data Model

`Theme` is a `sealed record` whose primary constructor carries `DepthFillColors`, `StrokeColor`,
`StrokeWidth`, `LineCornerRadius`, `FontSizeTitle`, `FontSizeBody`, `LabelPadding`, `ConnectorStub`,
`BendRadius`, and `CleanLegMargin`. `DepthFillColors` is indexed as `DepthFillColors[depth % count]`
to derive a box fill from its nesting depth. `Themes` is a static class exposing `Light`, `Dark`, and
`Print` as static read-only `Theme` instances.

#### Key Methods

##### `ConnectorApproachZone(connectorClearance)`

Returns the clear distance a connector needs off a box face before it can bend, computed as
`ConnectorStub + BendRadius + connectorClearance`. The layout strategies call it to reserve that
approach zone; `BendRadius` contributes only to this reservation and is not read by any renderer
(renderers round connector elbows using `LineCornerRadius`).

##### `BackgroundColor`

Returns the depth-0 fill (`DepthFillColors[0]`), used to occlude connector lines behind hollow
enclosing end markers.

#### Built-in Theme Values

- `Light` and `Dark` share connector geometry: `ConnectorStub` 8.0, `BendRadius` 4.0.
- `Print` is tighter: `ConnectorStub` 6.0, `BendRadius` 0.0, reserving no extra bend allowance in the
  connector approach zone for crisp black-and-white output.

#### Error Handling

`Theme` is an immutable record; construction stores the supplied values unchanged.
`ConnectorApproachZone` is a pure arithmetic helper that cannot fail.

#### Dependencies

None beyond the base class library. `Theme` is consumed by `RenderOptions`, the layout strategies,
and every `IRenderer` implementation.

#### Callers

`RenderOptions` holds a `Theme`; the SVG and PNG renderers read its colors, stroke, and connector
geometry; the layout strategies call `ConnectorApproachZone` to reserve connector space.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Rendering-Theme-ConnectorApproachZone | `Theme.ConnectorApproachZone(double)` |
| SysML2Tools-Core-Rendering-Theme-ConnectorGeometry | `Themes.Light`, `Themes.Dark`, `Themes.Print` connector fields |
