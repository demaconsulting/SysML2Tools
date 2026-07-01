### BoxMetrics

#### Purpose

`BoxMetrics` is the single home for the box title-area and folder-tab height formulas that are shared
by the layout strategies and the renderers. Because both sides derive reserved space and drawn space
from the same formulas, a box's title never collides with its nested children and a folder tab always
matches the space reserved above the box body. It is the layout-geometry peer of `NotationMetrics`.

#### Data Model

`BoxMetrics` is a static class with no instance state. Both methods take a `Theme` and return a
height in logical pixels.

#### Key Methods

##### `TitleAreaHeight(theme, hasLabel, hasKeyword)`

Returns the vertical space reserved at the top of a box for the optional keyword line and the bold
name line. Returns `0.0` when the box has neither a label nor a keyword; otherwise it accumulates one
`LabelPadding`, then `FontSizeBody + LabelPadding` when a keyword is present, then
`FontSizeTitle + LabelPadding` when a label is present.

##### `FolderTabHeight(theme)`

Returns the height of the folder tab drawn at the top-left of a `BoxShape.Folder` box, computed as
`FontSizeBody + 2 * LabelPadding`.

#### Error Handling

Both methods are pure functions over the supplied `Theme` and boolean flags; they perform no
allocation and cannot fail for a non-null theme.

#### Dependencies

- `Theme` (Rendering subsystem) — supplies the font sizes and label padding.

#### Callers

The interconnection and other view layout strategies call `TitleAreaHeight` to reserve title space
above nested children; the SVG and PNG renderers call both methods to draw the title area and folder
tab into the reserved space.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| SysML2Tools-Core-Layout-BoxMetrics-TitleAreaHeight | `TitleAreaHeight(Theme, bool, bool)` |
| SysML2Tools-Core-Layout-BoxMetrics-FolderTabHeight | `FolderTabHeight(Theme)` |
