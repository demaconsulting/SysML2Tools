#### LayoutWarnings

##### Purpose

`LayoutWarnings` builds the non-fatal layout-quality warning messages surfaced on a `LayoutTree`.
Its single responsibility is to turn a count of connectors that had to cross a box into the
human-readable warning text for a view.

##### Data Model

`LayoutWarnings` is a static class with no instance state. Inputs are the view name and the number
of crossing connectors. Output is a read-only list of warning strings.

##### Key Methods

###### `ForCrossings(viewName, crossings)`

Returns the warnings for a view:

1. When `crossings` is zero or negative, an empty list is returned.
2. Otherwise a single warning string is produced naming the view and reporting the count. The noun
   is rendered in singular form for a count of one and plural form otherwise, and the count is
   formatted with the invariant culture.

##### Error Handling

N/A - the method performs no validation and does not throw; a non-positive count simply yields an
empty list and any string view name is accepted.

##### Dependencies

- `System.Globalization.CultureInfo` for invariant-culture number formatting (.NET base class
  library).

##### Callers

View layout strategies that route connectors call `LayoutWarnings.ForCrossings` to attach
crossing warnings to the `LayoutTree` they produce.
