## DemaConsulting.SysML2Tools — Layout Subsystem Verification

### Verification Approach

The Layout subsystem is verified by unit tests in `DemaConsulting.SysML2Tools.Tests`. Tests
construct `LayoutTree` and its node types directly using positional record constructors, and
assert that all fields are stored and retrievable without modification. No mocking is required;
the Layout node types are pure immutable records with no dependencies.

### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
No external network access or services are required. All test inputs are constructed inline.

### Acceptance Criteria

- All unit tests pass with zero failures across all three target frameworks.
- `LayoutTree` stores `Width`, `Height`, and `Nodes` exactly as supplied to the constructor.
- `LayoutBox` stores all nine constructor parameters including `Depth` as a plain integer.
- `LayoutBox.Children` can contain nested `LayoutNode` instances of any concrete type.
- `LayoutPort` stores absolute `CentreX`, `CentreY`, `Side`, and optional `Label`.
- `LayoutLine` stores the `Waypoints` list, arrowhead styles, line style, and optional label.
- `LayoutLabel` stores `X`, `Y`, `MaxWidth`, `Text`, and `Align`.
- `LayoutBadge` stores `CentreX`, `CentreY`, `Size`, `Shape`, and optional `Label`.
- `LayoutBand` stores position, size, `Orientation`, optional `Label`, and `Children`.
- `LayoutLifeline` stores `CentreX`, `TopY`, `BottomY`, `Label`, `HeaderWidth`, and `HeaderHeight`.
- `LayoutActivation` stores `CentreX`, `TopY`, and `BottomY`.
- `LayoutGrid` stores `X`, `Y`, and `Rows`; each `LayoutGridRow` stores `IsHeader` and `Cells`.
- All coordinate values are stored as supplied; no transformation is applied by the data model.

### Test Scenarios

**LayoutTree_Construction_StoresWidthHeightNodes**: A `LayoutTree` is constructed with
explicit `Width = 800.0`, `Height = 600.0`, and a non-empty `Nodes` list containing a single
`LayoutBox`; all three properties are asserted to equal the supplied values. This confirms
that `LayoutTree` is a transparent data container with no side effects.

**LayoutBox_Construction_StoresAllFields**: A `LayoutBox` is constructed with all nine
parameters set to non-default values; each property is asserted to equal the supplied value.
This confirms correct positional record construction and property projection.

**LayoutBox_Depth_IsInteger**: A `LayoutBox` is constructed with `Depth = 3`; the `Depth`
property is asserted to be of type `int` with value `3`. This confirms the depth-not-color
invariant — no color property is present on the node.

**LayoutBox_Coordinates_AreAbsolute**: A `LayoutBox` is constructed with `X = 100.0` and
`Y = 200.0`; the `X` and `Y` properties are asserted to equal the supplied values without
offset. This confirms that the data model does not apply any coordinate transform.

**LayoutBox_Children_ContainsNestedNodes**: A `LayoutBox` is constructed with a `Children`
list containing a `LayoutPort` and a nested `LayoutBox`; both child nodes are retrievable
from `Children` in insertion order. This confirms that `LayoutBox.Children` supports
heterogeneous node types.

**LayoutPort_Construction_StoresAllFields**: A `LayoutPort` is constructed with `CentreX`,
`CentreY`, `Side`, and `Label` set; all four properties are asserted to equal the supplied
values. This confirms that ports carry sufficient information for absolute positioning.

**LayoutPort_Coordinates_AreAbsolute**: A `LayoutPort` is constructed with
`CentreX = 250.0` and `CentreY = 150.0`; both properties are asserted to equal the supplied
values. This confirms that port positions are absolute and not relative to the parent box.

**LayoutLine_Construction_StoresAllFields**: A `LayoutLine` is constructed with a two-element
`Waypoints` list, non-default `SourceArrowhead`, `TargetArrowhead`, `LineStyle`, and a
non-null `MidpointLabel`; all five properties are asserted to equal the supplied values.

**LayoutLine_Waypoints_AreAbsolute**: A `LayoutLine` is constructed with waypoints at
`(10.0, 20.0)` and `(200.0, 300.0)`; both `Point2D` instances in `Waypoints` are asserted
to have the supplied `X` and `Y` values. This confirms that routing produces absolute coordinates.

**LayoutLabel_Construction_StoresAllFields**: A `LayoutLabel` is constructed with all five
parameters set to non-default values; each property is asserted to equal the supplied value.

**LayoutBadge_Construction_StoresAllFields**: A `LayoutBadge` is constructed with all five
parameters set; each property is asserted to equal the supplied value.

**LayoutBand_Construction_StoresAllFields**: A `LayoutBand` is constructed with all seven
parameters set; each property is asserted to equal the supplied value.

**LayoutLifeline_Construction_StoresAllFields**: A `LayoutLifeline` is constructed with all
six parameters set; each property is asserted to equal the supplied value.

**LayoutActivation_Construction_StoresAllFields**: A `LayoutActivation` is constructed with
`CentreX`, `TopY`, and `BottomY` set; all three properties are asserted to equal the supplied values.

**LayoutGrid_Construction_StoresAllFields**: A `LayoutGrid` is constructed with `X`, `Y`,
and a `Rows` list containing one `LayoutGridRow` with `IsHeader = true` and one
`LayoutGridCell`; all fields are asserted to equal the supplied values.
