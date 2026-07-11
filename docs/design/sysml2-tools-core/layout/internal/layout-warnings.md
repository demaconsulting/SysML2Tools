#### LayoutWarnings

##### Purpose

`LayoutWarnings` builds the non-fatal layout-quality warning messages surfaced on a `LayoutTree`
from the `DemaConsulting.Rendering` package. Its responsibility is to turn layout-quality and
deferred-filtering conditions into the human-readable warning text for a view.

##### Data Model

`LayoutWarnings` is a static class with no instance state. Inputs are the view name together with
either a crossing count, a standalone filter-expression failure, or a list of bracket-filter
failures. Output is a read-only list of warning strings.

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

###### `ForUnevaluatedExposeBracketFilter(viewName, failures)`

Returns the warnings for a view's bracketed `expose <path>::**[<expr>]` filters that failed to
parse or evaluate (Phase 2a):

1. When `failures` (a list of `BracketFilterFailure` — an expression's raw source text plus a
   short optional reason) is empty, an empty list is returned — including the case where every
   bracket filter in the view parsed and evaluated successfully, since a successful bracket filter
   now has real narrowing effect and needs no disclaimer.
2. Otherwise one warning string is produced per failed expression, naming the view, quoting the
   failed expression text, appending the reason parenthetically when present, and stating that the
   exposed path falls back to its whole containment subtree.

##### Error Handling

N/A - the method performs no validation and does not throw; a non-positive count or empty failure
list simply yields an empty list and any string view name is accepted.

##### Dependencies

- `System.Globalization.CultureInfo` for invariant-culture number formatting (.NET base class
  library).
- `BracketFilterFailure` (Layout Internal subsystem, defined alongside `ExposeScopeResolver`) —
  the per-expression failure record `ForUnevaluatedExposeBracketFilter` iterates.

##### Callers

View layout strategies that route connectors call `LayoutWarnings.ForCrossings` to attach
crossing warnings to the `LayoutTree` they produce. `GeneralViewLayoutStrategy` calls
`LayoutWarnings.ForUnevaluatedFilter` to attach the standalone-filter fallback warning when a
view's `FilterExpressionText` cannot be evaluated, and (Phase 2a)
`LayoutWarnings.ForUnevaluatedExposeBracketFilter` with `scope?.Failures ?? []` to attach one
warning per bracket-filter expression that failed to parse or evaluate — never for a
successfully-evaluated bracket filter.
