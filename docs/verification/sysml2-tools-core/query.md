## DemaConsulting.SysML2Tools — Query Subsystem Verification

### Verification Approach

The Query subsystem is verified by direct tests in
`test/DemaConsulting.SysML2Tools.Tests/Query/QueryOmgFixtureTests.cs`,
`QueryRenderingTests.cs`, and `QueryResultExporterTests.cs`, plus dedicated unit tests for the
`QualifiedNameShortener` helper in
`test/DemaConsulting.SysML2Tools.Tests/Utilities/QualifiedNameShortenerTests.cs`. These tests
call `QueryEngine`, `QueryResultRenderer`, `QueryResultExporter`, and `QualifiedNameShortener`
directly against loaded OMG fixture workspaces and hand-built `QueryResult` instances, so the
public Core API is verified without any dependency on the Tool project's CLI parsing or
`Context` I/O behavior.

The `uses`, `used-by`, `dependencies`, `impact`, `interface`, `list`, `find`, and stdlib-
filtering behaviors are additionally exercised at the engine level from
`test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QueryVerbsTests.cs` and, for the
`IncludeStdlib` option, `test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs`. Those
tests call `Context.Create` and `Program.RunAsync`, but the assertions cited below verify the
public `QueryEngine`/`QueryOptions` result content itself, not CLI parsing or console
reporting. This is an intentional, documented choice to reuse the Tool project's existing
fixture setup for these verbs rather than duplicating it in the Core test project; it is not a
gap in Core-level verification.

### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0 in the
`DemaConsulting.SysML2Tools.Tests` project. OMG smoke tests read files under
`test/SysMLModels/OMG/` when present in the checkout. Exporter tests create and clean up their
own temporary files; no network access or external services are required.

The additional `uses`/`used-by`/`dependencies`/`impact`/`interface`/`list`/`find`/stdlib-filter
scenarios listed under "Test Scenarios (Tool Test Project)" below run via `dotnet test` in the
`DemaConsulting.SysML2Tools.Tool.Tests` project instead, targeting net10.0 only.

### Acceptance Criteria

- All `QueryOmgFixtureTests`, `QueryRenderingTests`, `QueryResultExporterTests`, and
  `QualifiedNameShortenerTests` pass with zero failures across all three target frameworks.
- The `uses`, `used-by`, `dependencies`, `impact`, `interface`, `list`, `find`, and stdlib-
  filtering behaviors are proven at the `QueryEngine`/`QueryOptions` level by the
  `Tool.Tests`-hosted scenarios cited below, in addition to the Core-project scenarios above;
  not every verb's engine-level coverage is required to live in the Core test project.
- Representative real-world OMG fixtures produce non-empty, sensible `requirements`,
  `connections`, `states`, `hierarchy`, and `describe` results through the public
  `QueryEngine` API.
- Markdown and JSON rendering preserve the same deterministic, ordinal-by-qualified-name entry
  ordering.
- The `dependencies` verb renders Markdown as prose bullets, shortens names only in Markdown,
  retains fully qualified JSON output, and omits the `Direction` property from non-
  `dependencies` JSON results.
- `QueryResultExporter` writes exactly the renderer's Markdown and JSON text and propagates
  missing-parent-directory write failures instead of masking them.
- `QualifiedNameShortener.Shorten` strips only the longest shared leading `::`-segment prefix
  across a pool of qualified names, always capped so every name keeps at least its own leaf
  segment, and rejects `null` pools or `null` pool entries.

### Test Scenarios

**`Requirements_RequirementSatisfactionFixture_ReportsAtLeastOneRelationship`**: Loads the OMG
`RequirementSatisfaction.sysml` fixture, runs `QueryEngine.Requirements`, and verifies that at
least one satisfy/verify/allocate relationship is reported.

**`Connections_ConnectionsExampleFixture_ReportsAtLeastOneEndpoint`**: Loads the OMG
`ConnectionsExample.sysml` fixture, runs `QueryEngine.Connections`, and verifies that at least
one resolved connection endpoint is reported.

**`States_StateDecompositionFixture_ReportsStatesAndTransitions`**: Loads the OMG
`StateDecomposition-1.sysml` fixture, runs `QueryEngine.States`, and verifies that the public
API reports the reliably-produced state-transition content from that model.

**`Hierarchy_GeneralizationExampleFixture_ReportsSupertypes`**: Loads the OMG
`GeneralizationExample.sysml` fixture, runs `QueryEngine.Hierarchy`, and verifies that resolved
supertypes are reported.

**`Describe_CommentsFixture_ReportsAnnotations`**: Loads the OMG `Comments.sysml` fixture, runs
`QueryEngine.Describe`, and verifies that annotations appear in the result summary.

**`RenderMarkdown_NoEntries_ReportsNoEntries`**: Verifies that a non-`dependencies` result with
no entries renders the shared Markdown `_No entries._` fallback.

**`RenderMarkdown_DependenciesVerb_RendersBulletProseNotTable`**: Verifies that
`dependencies` Markdown renders as grouped prose bullets rather than the shared table.

**`RenderMarkdown_DependenciesVerb_NoCommonPrefix_LeavesNamesFullyQualified`**: Verifies that
`dependencies` Markdown leaves names fully qualified when the subject and entries share no
common leading `::`-segment prefix.

**`RenderMarkdown_DependenciesVerb_EmptyOutgoingAndIncoming_ReportsBothProseLines`**:
Verifies that `dependencies` Markdown emits the two prose fallback lines when neither direction
contains entries.

**`RenderMarkdown_UnorderedEntries_SortsByQualifiedNameOrdinal`**: Verifies that Markdown output
sorts entries ordinally by qualified name before rendering.

**`RenderMarkdown_EntryWithDetailAndNotes_CombinesIntoOneCell`**: Verifies that Markdown table
rendering preserves `Detail` and `Notes` content together in the detail cell.

**`RenderMarkdown_DefaultArguments_ProducesUnchangedTopLevelHeading`**: Verifies that the
default Markdown heading remains `# query <verb>[: <element>]`.

**`RenderMarkdown_CustomDepth_UsesThatManyHeadingHashes`**: Verifies that Markdown heading depth
is controlled by the `depth` argument.

**`RenderMarkdown_CustomHeading_ReplacesAutoGeneratedText`**: Verifies that a custom heading
replaces the auto-generated heading text.

**`RenderMarkdown_CustomDepthAndHeading_CombinesBothOverrides`**: Verifies that custom heading
text and custom heading depth compose correctly.

**`RenderJson_RoundTrips_PreservesShape`**: Verifies that JSON rendering round-trips the
`QueryResult` shape through `QueryResultSerializerContext`.

**`RenderJson_UnorderedEntries_SortsByQualifiedNameOrdinal`**: Verifies that JSON output uses
that same deterministic ordinal entry order.

**`RenderJson_DependenciesVerb_IncludesDirectionField`**: Verifies that `dependencies` JSON
includes the populated `Direction` field.

**`RenderJson_DependenciesVerb_NamesRemainFullyQualified`**: Verifies that JSON output never
applies Markdown-only qualified-name shortening.

**`RenderJson_NonDependenciesVerb_DirectionFieldOmittedFromOutput`**: Verifies that non-
`dependencies` JSON omits the `Direction` property entirely when it is `null`.

**`WriteMarkdown_HappyPath_MatchesRendererOutput`**: Verifies that `WriteMarkdown` writes the
same Markdown text produced by `QueryResultRenderer.RenderMarkdown`.

**`WriteMarkdown_WithDepthAndHeading_MatchesRendererOutput`**: Verifies that `WriteMarkdown`
passes custom heading depth and heading text through unchanged.

**`WriteMarkdownAsync_HappyPath_MatchesRendererOutput`**: Verifies that `WriteMarkdownAsync`
writes the same Markdown text as the synchronous path.

**`WriteJson_HappyPath_MatchesRendererOutput`**: Verifies that `WriteJson` writes the same JSON
string produced by `QueryResultRenderer.RenderJson`.

**`WriteJsonAsync_HappyPath_MatchesRendererOutput`**: Verifies that `WriteJsonAsync` writes the
same JSON string as the synchronous path.

**`WriteMarkdown_MissingParentDirectory_PropagatesIoException`**: Verifies that the Markdown
exporter does not create parent directories or suppress the resulting write failure.

**`WriteJson_MissingParentDirectory_PropagatesIoException`**: Verifies that the JSON exporter
likewise propagates a missing-parent-directory write failure.

#### QualifiedNameShortenerTests.cs

**`QualifiedNameShortener_Shorten_OneSharedLeadingSegment_StripsThatSegment`**: The worked
example `["A::B::x", "A::B::y", "A::T::g"]` is shortened; the shared leading segment `"A"` is
stripped from every name, producing `["B::x", "B::y", "T::g"]`.

**`QualifiedNameShortener_Shorten_NoCommonPrefix_LeavesNamesUnchanged`**: A pool of names
rooted in different top-level packages (`["A::B::x", "C::D::y"]`) is shortened; every name is
returned unchanged since no leading segment is shared.

**`QualifiedNameShortener_Shorten_SingleNamePool_LeavesNameUnchanged`**: A pool containing only
one distinct name is shortened; the name is returned unchanged since there is nothing to
compare it against.

**`QualifiedNameShortener_Shorten_AllIdenticalNames_KeepsLeafSegment`**: A pool where every
entry is the same name (`["A::B::x", "A::B::x", "A::B::x"]`) reduces to a single distinct name
and is returned unchanged, confirming the leaf segment `"x"` is never stripped down to an empty
string.

**`QualifiedNameShortener_Shorten_DeeperCommonPrefix_StripsAllSharedSegments`**: A pool sharing
the two leading segments `"A::B"` (`["A::B::C::x", "A::B::C::y", "A::B::D::z"]`) is shortened;
both shared segments are stripped from every name.

**`QualifiedNameShortener_Shorten_ShortestNameBoundsCap_RetainsShortestNamesLeaf`**: A pool
containing `"A::B"` (2 segments) alongside `"A::B::C"` (which shares the 2-segment prefix
`"A::B"`) is shortened; only 1 segment (`"A"`) is stripped, capped by `"A::B"`'s own segment
count, so `"A::B"` becomes `"B"` rather than an empty string.

**`QualifiedNameShortener_Shorten_NullPool_ThrowsArgumentNullException`**: `null` is passed as
the `qualifiedNames` argument; an `ArgumentNullException` is thrown.

**`QualifiedNameShortener_Shorten_NullEntryInPool_ThrowsArgumentNullException`**: A pool
containing a `null` entry is passed; an `ArgumentNullException` is thrown.

**`QualifiedNameShortener_Shorten_DuplicateNamesInPool_ReturnsOneEntryPerDistinctName`**: A pool
where one name is repeated (`["A::B::x", "A::B::x", "A::T::g"]`) is shortened; the returned map
contains exactly one entry per distinct name, both correctly shortened.

### Test Scenarios (Tool Test Project)

The scenarios below prove `uses`, `used-by`, `dependencies`, `impact`, `interface`, `list`,
`find`, and stdlib-filter behavior against the public `QueryEngine`/`QueryOptions` API. They run
in `test/DemaConsulting.SysML2Tools.Tool.Tests/Query/QueryVerbsTests.cs` (and, for one
`IncludeStdlib` parsing scenario, `test/DemaConsulting.SysML2Tools.Tool.Tests/Cli/ContextTests.cs`)
rather than the Core test project, because they reuse the Tool project's existing inline
`.sysml` fixture helpers instead of duplicating that setup in Core. Tool-side CLI parsing,
dispatch, and console-reporting concerns for these same verbs are verified separately in
`docs/verification/sysml2-tools-tool/query.md`.

#### QueryVerbsTests.cs

**`Uses_ReportsOutgoingSupertypeTypingImportEdges`**: Verifies that `QueryEngine.Uses` reports a
target element's outgoing supertype, typing, and import edges.

**`UsedBy_ReportsIncomingReferences`**: Verifies that `QueryEngine.UsedBy` reports elements that
reference the target through the semantic index's reverse lookup.

**`Dependencies_CombinesOutgoingAndIncoming_ReportsBothDirections`**: Verifies that
`QueryEngine.Dependencies` combines outgoing and incoming relationships into one result, each
entry tagged with its direction.

**`Dependencies_NoOutgoingReferences_ReportsProseLineInsteadOfBulletList`**: Verifies that
`dependencies` Markdown rendering falls back to a prose line when the target has no outgoing
references.

**`Dependencies_NoIncomingReferences_ReportsProseLineInsteadOfBulletList`**: Verifies that
`dependencies` Markdown rendering falls back to a prose line when the target has no incoming
references.

**`Impact_DepthOne_OnlyReachesDirectReferences`**: Verifies that `QueryEngine.Impact` with
`WalkDepth` of one reaches only directly-referencing elements.

**`Impact_Unbounded_ReachesTransitiveClosure`**: Verifies that unbounded `QueryEngine.Impact`
reaches the full transitive closure of incoming references without looping on cycles.

**`Interface_ReportsPortsAndTypedFeatures`**: Verifies that `QueryEngine.Interface` reports a
target definition's ports and typed features.

**`List_NoFilters_ReturnsAllNonStdlibElements`**: Verifies that `QueryEngine.List` with no
filters returns every non-stdlib workspace declaration.

**`List_KindFilter_OnlyMatchesGivenKind`**: Verifies that `QueryEngine.List` with a kind filter
returns only declarations whose kind matches the given substring.

**`List_NameFilter_OnlyMatchesGivenSubstring`**: Verifies that `QueryEngine.List` with a name
filter returns only declarations whose qualified name contains the given substring.

**`Find_WithNameFilter_Succeeds`**: Verifies that `QueryEngine.Find` applies a name filter over
the same workspace-wide search used by `list`.

**`List_IncludeStdlib_TogglesStandardLibraryVisibility`**: Verifies that supplying
`IncludeStdlib` on `QueryOptions` causes `QueryEngine.List` to include stdlib-seeded elements
that are excluded by default.

#### ContextTests.cs

**`Context_Create_QueryCommand_WithIncludeStdlibFlag_SetsIncludeStdlibTrue`**: Verifies that the
`--include-stdlib` command-line flag sets `QueryOptions.IncludeStdlib` to `true`, which is the
CLI-side input that drives the `QueryEngine.List` stdlib-filtering behavior confirmed by
`List_IncludeStdlib_TogglesStandardLibraryVisibility` above.
