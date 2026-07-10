#### LayoutWarnings

##### Purpose

`LayoutWarnings` builds the non-fatal layout-quality warning messages surfaced on a `LayoutTree`
from the `DemaConsulting.Rendering` package. Its responsibility is to turn layout-quality and
deferred-filtering conditions into the human-readable warning text for a view.

##### Data Model

`LayoutWarnings` is a static class with no instance state. Inputs are the view name together with
either a crossing count, a standalone filter-expression failure, or a list of bracket-filter
expressions. Output is a read-only list of warning strings.

##### Key Methods

###### `ForCrossings(viewName, crossings)`

Returns the warnings for a view:

1. When `crossings` is zero or negative, an empty list is returned.
2. Otherwise a single warning string is produced naming the view and reporting the count. The noun
   is rendered in singular form for a count of one and plural form otherwise, and the count is
   formatted with the invariant culture.

###### `ForUnevaluatedFilter(viewName, filterExpressionText, reason = null)`

Returns the warnings for a view's declared filter expression:

1. When `filterExpressionText` is `null` (the view has no `filter [<expr>];` member), an empty
   list is returned.
2. Otherwise a single warning string is produced naming the view and stating that the filter
   expression could not be evaluated; when `reason` is non-empty, it is appended parenthetically.
   The raw expression text itself is not interpolated into the message — only its presence matters.

###### `ForUnevaluatedExposeBracketFilter(viewName, bracketFilterTexts)`

Returns the warnings for a view's bracketed `expose <path>::**[<expr>]` filters:

1. When `bracketFilterTexts` is empty, an empty list is returned.
2. Otherwise a single warning string is produced naming the view, reporting how many bracket
   filters were declared, and stating that the expressions were parsed but not yet evaluated in
   Phase 1.

##### Error Handling

N/A - the method performs no validation and does not throw; a non-positive count simply yields an
empty list and any string view name is accepted.

##### Dependencies

- `System.Globalization.CultureInfo` for invariant-culture number formatting (.NET base class
  library).

##### Callers

View layout strategies that route connectors call `LayoutWarnings.ForCrossings` to attach
crossing warnings to the `LayoutTree` they produce. `GeneralViewLayoutStrategy` calls
`LayoutWarnings.ForUnevaluatedFilter` to attach the standalone-filter fallback warning when a
view's `FilterExpressionText` cannot be evaluated, and
`LayoutWarnings.ForUnevaluatedExposeBracketFilter` when a view declares capture-only bracket
filters.
