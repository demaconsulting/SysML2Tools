### QualifiedNameShortener

#### Purpose

`QualifiedNameShortener` is a static utility class that strips the longest leading `::`-segment
prefix shared by every name in a supplied pool of qualified names. Its single responsibility is
to make a set of related qualified names more compact for Markdown-oriented display, without
ever discarding a name's own distinguishing leaf segment.

#### Data Model

`QualifiedNameShortener` holds no instance state. The class is `internal static` with no fields
or properties other than the private `SegmentSeparator` constant (`"::"`).

#### Key Methods

**Shorten**: Computes a shortened form of every supplied qualified name.

- *Parameters*: `IReadOnlyList<string> qualifiedNames` — the pool of qualified names to shorten
  together.
- *Returns*: `IReadOnlyDictionary<string, string>` — a map from each distinct original name
  (ordinal key comparison) to its shortened form.
- *Preconditions*: `qualifiedNames` and every contained name are non-null.
- *Postconditions*: Every name in the pool keeps at least its own final ("::"-delimited) leaf
  segment; when fewer than 2 distinct names are supplied, or the names share no common leading
  segment, every value equals its key unchanged.

Algorithm: (1) reject a null pool or a null entry via `ArgumentNullException.ThrowIfNull`; (2)
reduce the pool to its distinct names (`Distinct(StringComparer.Ordinal)`); (3) if fewer than 2
distinct names remain, return an identity map (skip stripping entirely — there is nothing to
compare a lone name against); (4) split every distinct name into "::"-segments; (5) compute the
common-prefix length via the private `ComputeCommonPrefixLength` helper, which caps the search at
the shortest name's segment count minus 1 (so no name can ever be stripped down to nothing) and
walks segment indices until the first mismatch (ordinal comparison) across every name; (6) when
the computed length is 0, return an identity map; otherwise return a map of each distinct name to
`string.Join("::", segments[commonPrefixLength..])`.

#### Error Handling

`Shorten` throws `ArgumentNullException` when `qualifiedNames` itself is `null`, or when any
contained name is `null`. No other exception is thrown; a pool sharing no common prefix, or
containing only one distinct name, is a normal (non-error) input handled by returning an
identity map.

#### Dependencies

- **.NET BCL** — `string.Split`, `string.Join`, `Enumerable.Distinct`/`Min`, and
  `ArgumentNullException` are the only dependencies. No other tool units or subsystems are used.

#### Callers

- **Query** — `QueryResultRenderer.RenderMarkdown`'s `dependencies`-only rendering branch calls
  `QualifiedNameShortener.Shorten` on the pool `[Element] + Entries.Select(QualifiedName)` before
  rendering the subject sentence and bullets, so Markdown output for a related set of names is
  more compact. No other verb, and no JSON output, calls this utility.
