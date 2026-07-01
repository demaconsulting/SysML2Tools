### LayoutTree

#### Purpose

`LayoutTree` is the intermediate representation consumed by every renderer: a tree of immutable
records that describes the visual structure of one rendered diagram view. All spatial decisions are
made by an `ILayoutStrategy` before the tree is handed to a renderer; the renderer only reads the
tree and writes output.

#### Grouped Unit

This unit is a **grouped data-model unit**. It bundles the ten cohesive `LayoutTree` record source
files that together define the representation, because they are small immutable records with no logic
that are always used and versioned as one vocabulary:

- `LayoutTree.cs` — root container (`Width`, `Height`, top-level `Nodes`) and `Point2D`.
- `LayoutNode.cs` — the abstract discriminated-union base.
- `LayoutBox.cs` — rectangular container plus `LayoutCompartment`.
- `LayoutPort.cs` — edge connection point.
- `LayoutLine.cs` — pre-routed orthogonal polyline.
- `LayoutLabel.cs` — standalone text label (and shared `TextAlign`).
- `LayoutBadge.cs` — small icon decorator.
- `LayoutBand.cs` — swim-lane container.
- `LayoutGrid.cs` — tabular node with `LayoutGridRow` / `LayoutGridCell`.
- `LayoutLifeline.cs` — sequence-diagram lifeline plus the nested `LayoutActivation`.

The full per-record contract (every field of every node type) is documented in the **Layout
Subsystem** chapter's *Interfaces* section and is not duplicated here; this chapter records the
grouping and the data-model invariants that the unit tests pin.

#### Data Model

Every type is a `sealed record` (or the abstract `LayoutNode` base). The tree has no methods; all
construction is performed by `ILayoutStrategy` implementations. Coordinates are absolute, boxes carry
an integer `Depth` (not a color), and nesting is expressed through `LayoutBox.Children` and
`LayoutBand.Children`.

#### Error Handling

The records are immutable value containers; construction stores the supplied values unchanged and
performs no validation beyond the language's non-nullable reference guarantees.

#### Dependencies

- `Theme` (Rendering subsystem) is referenced only indirectly: renderers index `Theme.DepthFillColors`
  with `LayoutBox.Depth`. The Layout records themselves depend on nothing beyond the base class
  library.

#### Callers

Every `ILayoutStrategy` constructs a `LayoutTree`; every `IRenderer` reads one.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Layout-LayoutTree-RecordFamily | The ten grouped record source files |
| SysML2Tools-Core-Layout-LayoutTree-AbsoluteCoordinates | Absolute coordinate fields on every node type |
| SysML2Tools-Core-Layout-LayoutTree-DepthAndHierarchy | `LayoutBox.Depth` and `LayoutBox.Children` |
