## DemaConsulting.SysML2Tools — Query Subsystem Verification

### Verification Approach

The Query subsystem is verified by direct tests in
`test/DemaConsulting.SysML2Tools.Tests/Query/QueryOmgFixtureTests.cs`,
`QueryRenderingTests.cs`, `QueryEngineImpactTests.cs`, and `QueryResultExporterTests.cs`, plus
dedicated unit tests for the
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

- All `QueryOmgFixtureTests`, `QueryRenderingTests`, `QueryEngineImpactTests`,
  `QueryResultExporterTests`, and
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
- `describe`'s `Children: N` summary line always equals the number of rows in the `Entries`
  table (visible, named children only), never the raw count of the element's underlying AST
  child nodes.
- Every verb except `dependencies` precedes its entries table (or "no entries" fallback) with a
  verb-specific bold-text label and fallback message (e.g. `**Children**` / `_No children._` for
  `describe`), falling back to a generic `**Entries**` / `_No entries._` label for any
  unrecognized verb. The label is always plain bold text, never an ATX heading, so the report
  never branches into a Markdown sub-section regardless of the caller's requested heading depth.
- `impact` without `IncludeConnections` reports a reference-only result that excludes `Connect`
  and `Binding` edges entirely, so a subject whose only relationship to the rest of the model is
  a connector reports no impacted elements — including when both connector endpoints are
  declared part usages rather than nested ports.
- `impact` with `IncludeConnections` reaches the part usage on the far side of a connector, from
  either end of that connector, proving the traversal is undirected rather than reverse-only.
- Connector endpoints that are nested ports are attributed to their nearest owning part usage,
  with the raw far endpoint preserved in the entry's `ViaQualifiedName` and `Notes`. A far
  endpoint that is itself a declared element is reported unchanged, with no roll-up performed
  and `ViaQualifiedName` left null, so `impact` and `connections` agree on the same connector's
  topology and the enclosing definition that also owns the subject is never reported in the
  endpoint's place.
- Connector hops are bounded to one per traversal path when no `WalkDepth` is supplied, and to
  `WalkDepth` when it is, while reference-edge depth semantics are unchanged. The bound holds
  for connector chains whose endpoints are declared part usages, not only for port endpoints.
- A single connector produces exactly one impact entry — its far endpoint rolled up to the
  nearest owning declaration — regardless of which side of the connector the nested port sits
  on, and never an additional raw-endpoint entry.
- An element re-reached at a strictly lower connector-hop count is re-expanded, so elements
  within the hop budget of the reference closure are never dropped because a costlier path
  reached them first, while the re-reached element keeps its first-arrival depth and relation
  attribution and is still reported exactly once.
- A cyclic connector topology terminates and reports each impacted element exactly once.
- `Binding` connectors (`bind A = B;`) are traversed undirected exactly like `Connect`
  connectors.
- The public `QueryEngine.Impact`/`QueryOptions` API delivers all of the above with no CLI
  involvement, as consumed by non-CLI clients such as SysML2Workbench.
- Traversal-produced entries carry structured `Depth`, `Relation`, and (where roll-up occurred)
  `ViaQualifiedName` values; JSON emits `Relation` as its enum member name, omits all three
  when null, and `Detail` remains the same human-readable `"depth N"` text in Markdown.

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
no entries is preceded by its verb-specific bold-text label (e.g. `**Uses**`) and renders the
verb-specific "no entries" fallback text (e.g. `_No outgoing references._`) instead of an empty
table.

**`RenderMarkdown_EntriesPresent_IncludesVerbSpecificBoldLabel`**: Verifies that a
non-`dependencies` result with entries is also preceded by its verb-specific bold-text label
(e.g. `**Children**` for `describe`), not just the "no entries" fallback case, and that the
label is plain bold text rather than an ATX heading.

**`RenderMarkdown_UnrecognizedVerb_FallsBackToGenericEntriesLabel`**: Verifies that a verb with
no specific entries-label mapping falls back to the generic `**Entries**` label and `_No
entries._` fallback text.

**`RenderMarkdown_ListOrFindVerb_UsesMatchingElementsLabel`**: Verifies that both `list`
and `find` share the same `**Matching Elements**` label, since `find` is a filtered form
of `list` and their entries mean the same thing.

**`RenderMarkdown_MaxHeadingDepth_EntriesLabelStaysBoldTextNotHeading`**: Verifies that at the
maximum Markdown heading depth (6), the entries label remains plain bold text (`**Children**`)
rather than becoming (or being mistaken for) an ATX heading, so the report never branches into
a Markdown sub-section at any valid heading depth.

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

**`RenderJson_EntryWithDepthAndRelation_IncludesBothFieldsAndRoundTrips`**: Renders an impact
entry carrying `Depth`, `Relation`, and `ViaQualifiedName`; verifies that JSON contains
`"Depth": 1` and `"Relation": "Connect"` (the enum member *name*, not its numeric value) and
that all three values round-trip back through `QueryResultSerializerContext`, which also proves
`JsonStringEnumConverter<SysmlEdgeKind>` is compatible with the source-generated context.

**`RenderJson_EntryWithoutTraversalMetadata_OmitsDepthRelationAndVia`**: Verifies that an entry
produced by a non-traversing verb omits `Depth`, `Relation`, and `ViaQualifiedName` from JSON
entirely rather than serializing them as `null`, so no existing verb's payload shape changes.

**`RenderMarkdown_ImpactEntry_DetailRemainsHumanReadableDepthText`**: Verifies that an impact
entry carrying structured metadata still renders its `Detail` cell as the plain `depth 1` text
and that the structured `Relation` value does not leak into Markdown output.

#### QueryEngineImpactTests.cs

These scenarios exercise the public Core API only — a temp-file workspace loaded through
`WorkspaceLoader`, a hand-constructed `QueryOptions`, and a direct `QueryEngine.Impact` call —
proving that a non-CLI client such as SysML2Workbench obtains connection-aware impact results
and their structured metadata without any dependency on the Tool project.

**`Impact_IncludeConnections_ThroughPublicApi_ReturnsConnectedPartEntries`**: Runs `Impact`
twice over the same connected-parts fixture, once with `IncludeConnections` unset and once with
it set; verifies the first returns no entries and the second returns the connected hub part
usage.

**`Impact_ConnectionEntry_RecordsDepthRelationAndViaQualifiedName`**: Verifies that a connection
entry records `Depth` of 1, `Relation` of `SysmlEdgeKind.Connect`, and a `ViaQualifiedName` of
the nested port endpoint the entry was rolled up from.

**`Impact_ReferenceEntry_RecordsDepthAndReferenceRelation`**: Verifies that a reference entry
records `Depth` of 1 and `Relation` of `SysmlEdgeKind.Supertype`, with no `ViaQualifiedName`
since no roll-up occurred.

**`Impact_IncludeConnections_DeclaredFarEndpoint_ReportsEndpointItself`**: Uses the
declared-endpoints fixture — sibling part usages `alpha`, `beta`, and `gamma` joined by
`connect alpha to beta;` and `bind beta = gamma;` with no ports anywhere — and queries `alpha`
with `IncludeConnections` set; verifies the impacted entry is `Model::System::beta` itself at
`Depth` 1 with `Relation` of `SysmlEdgeKind.Connect`, and that neither the enclosing
`Model::System` definition nor the subject `Model::System::alpha` appears among the entries.

**`Impact_ConnectionEntry_WithoutRollUp_OmitsViaQualifiedName`**: Uses the same
declared-endpoints fixture and subject; verifies that the `Model::System::beta` entry leaves
`ViaQualifiedName` null because no roll-up occurred, while its `Notes` still name both raw
connector endpoints so no information is lost.

**`Impact_IncludeConnections_SourceSidePortEndpoint_ProducesExactlyOneEntry`**: Uses the
source-side-port fixture, in which the nested port is the connector's *source*
(`connect hub.J1 to motorA;`) rather than its target, and queries `Model::System::motorA` with
`IncludeConnections` set. That orientation makes the subject itself the incoming-edge key for
the connector, which is the precondition for duplicate attribution — an unfiltered reference
pass reports the raw port `Model::System::hub::J1` in addition to the correctly rolled-up
`Model::System::hub`. Verifies that the result contains **exactly one** entry in total, that it
is `Model::System::hub` at `Depth` 1 with `Relation` of `SysmlEdgeKind.Connect` and
`ViaQualifiedName` of `Model::System::hub::J1`, and that no entry names the raw port. The
whole-list `Assert.Single` overload is used deliberately: the predicate overload asserts only
that a *matching* entry is unique and is structurally blind to an extra non-matching entry,
which is how the duplicate previously escaped detection.

**`Impact_IncludeConnections_ReReachedAtLowerHopCount_KeepsFirstArrivalDepthAndAttribution`**:
Uses the minimum-hop fixture, in which `b` is reached from `s` over `connect b to s` at one
connector hop and re-reached one breadth-first level deeper over the subsetting chain
`b :> s2 :> s` at zero hops. Verifies that `b` appears exactly once and retains its
first-arrival `Depth` of 1 and `Relation` of `SysmlEdgeKind.Connect` — the cheaper re-arrival
re-opens it for expansion but never rewrites its attribution — and that `z`, reachable only by
expanding the re-opened `b`, is reported with `Relation` of `SysmlEdgeKind.Connect` at `Depth`
**3**. Depth 3 is correct and is not an off-by-one: `b` is re-reached cheaply at level 2 and
therefore expands its connectors at level 3, which is genuinely the first breadth-first level
at which `z` becomes reachable within the hop budget.

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

The connection-aware `impact` scenarios below use the shared `QueryTestFixtures.
GantryConnections` fixture (a minimal reduction of the three-axis-gantry topology: two motor
part usages, each connecting one of its nested ports to a distinct port of a shared hub part
usage) except where another shared fixture or a local variant is named. No motor references the
other and no motor has an incoming reference edge, so any element reported was necessarily
reached through a connector. Because every `GantryConnections` connector endpoint is a nested
port, the queried part usage is never a key in the incoming-edge index; scenarios that must
detect connector edges leaking into the reference closure therefore use
`ChainedDeclaredConnectors` or `SourceSidePortConnector`, whose endpoints are declared part
usages or source-side ports, instead.

**`Impact_IncludeConnectionsFlagAbsent_ReportsReferenceOnlyResult`**: Uses the
`QueryTestFixtures.ChainedDeclaredConnectors` fixture (four sibling part usages joined by
`connect b to a; connect c to b; connect d to c;`) and queries `Model::System::a` without the
flag; verifies zero impacted elements, that none of `b`, `c`, or `d` is named, and that the
summary does not mention connections. The fixture is deliberately *not* `GantryConnections`:
because every `GantryConnections` connector endpoint is a nested port, the queried part usage is
never a key in the incoming-edge index, so the defective path — connector edges leaking into the
reference closure — is never reached and no assertion over that fixture could ever fail. This
scenario is the lock on the corrected default: connector edge kinds are excluded from the
reference closure entirely.

**`Impact_IncludeConnections_ReachesConnectedSiblingPart`**: Verifies that the same query with
`--include-connections` reaches the connected hub part usage and reports the one-hop connection
bound in its summary.

**`Impact_IncludeConnections_QueriedFromFarEndpoint_ReachesOriginatingPart`**: Undirected proof
— verifies that querying from the hub (the endpoint named second in every connector) reaches
both motors, the mirror image of the previous scenario.

**`Impact_IncludeConnections_PortEndpoints_RollUpToOwningPartUsage`**: Verifies that the
reported entry is the owning part usage with kind `part` (not the nested port), and that the raw
port-to-port endpoint pair is preserved in the entry's notes.

**`Impact_IncludeConnections_NoWalkDepth_BoundsConnectionHopsToOne`**: Uses the
`QueryTestFixtures.ChainedDeclaredConnectors` fixture and queries `Model::System::a` with
`--include-connections`; verifies exactly `1 element(s) transitively impacted`, that the `b` row
is present, that neither `c` nor `d` is named, and that the summary reads
`including connections (connection hops <= 1)`. The fixture change from `GantryConnections` is
essential for the same reason given above — only declared (non-port) connector endpoints make
the queried element an incoming-edge key, and only then can a connector chain be followed past
the hop bound the summary claims. Asserting the bare element count, not just names, is what
makes the scenario sensitive to any extra row.

**`Impact_IncludeConnections_WalkDepthTwo_ReachesSecondConnectionHop`**: Verifies that
`--walk-depth 2` raises the connector hop bound to two, reaching the second motor, and reports
the raised bound in the summary.

**`Impact_IncludeConnections_CyclicConnections_TerminatesWithoutDuplicates`**: Uses a local
variant joining one motor and the hub with two connectors written in opposite textual order,
with `--walk-depth 5`; verifies the query terminates and reports the hub exactly once.

**`Impact_IncludeConnections_BindingEdges_AreTraversedUndirected`**: Uses a local `bind A = B;`
variant and verifies that each bound part is reachable from the other, proving `Binding` edges
are traversed exactly like `Connect` edges.

**`Impact_IncludeConnections_DeclaredEndpointConnector_ReportsSiblingPartNotOwningDefinition`**:
Uses the `QueryTestFixtures.DeclaredEndpointConnections` fixture, in which every connector and
binding names a directly declared sibling part usage rather than a nested port, and queries
`Model::System::alpha` with `--include-connections`; verifies the rendered table contains the
`| Model::System::beta | part |` row and never the `| Model::System | part def |` row for the
enclosing definition.

**`Impact_IncludeConnections_ChainedDeclaredConnectors_StopsAtOneConnectorHop`**: Uses the
`ChainedDeclaredConnectors` fixture and queries `Model::System::a` with
`--include-connections`; verifies exactly one impacted element, that it is `b`, and that neither
`c` (two connector hops) nor `d` (three) is reported. This is the direct regression lock for the
reported defect in which a declared-connector chain was traversed unbounded while the summary
still claimed a one-hop bound.

**`Impact_ChainedDeclaredConnectors_FlagAbsent_ReportsNoImpactedElements`**: Same fixture and
subject without the flag; verifies zero impacted elements, that `b` is not named, and that the
summary omits the connections suffix — proving connector edges contribute nothing to the default
reference closure.

**`Impact_IncludeConnections_ChainedDeclaredConnectors_WalkDepthTwo_ReachesSecondHopOnly`**:
Same fixture and subject with `--walk-depth 2`; verifies that `b` and `c` are reported, `d` is
not, and the summary reads `including connections (connection hops <= 2)` — proving the bound
tracks `--walk-depth` rather than being absent.

**`Impact_IncludeConnections_SourceSidePortEndpoint_ReportsOwnerNotRawPort`**: Uses the
`QueryTestFixtures.SourceSidePortConnector` fixture (`connect hub.J1 to motorA;`, the nested port
on the connector's source side) and queries `Model::System::motorA` with
`--include-connections`; verifies exactly one impacted element, that it is the
`| Model::System::hub | part |` row, and that no `| Model::System::hub::J1 | connect` row
appears. This is the regression lock for the reported defect in which a single connector
produced two rows — the correct rolled-up owner plus a raw port entry.

**`Impact_SourceSidePortEndpoint_FlagAbsent_ReportsNoImpactedElements`**: Same fixture and
subject without the flag; verifies zero impacted elements and that the hub is not named at all.

**`Impact_IncludeConnections_CheaperReferencePath_ReExpandsConnectorsFromReReachedElement`**:
Uses the `QueryTestFixtures.MinimumHopReExpansion` fixture and queries `Model::Assembly::s` with
`--include-connections`; verifies exactly three impacted elements — `b` (reached over a
connector), `s2` (reached over a reference edge), and `z`, which is reachable only because `b`
is re-expanded after being re-reached at a cheaper connector-hop count. `z` is reported at depth
3, since `b` is re-reached at breadth-first level 2 and expands its connectors at level 3; that
is the first level at which `z` is genuinely reachable within the hop budget, not an off-by-one.
Without minimum-hop tracking `z` is silently dropped and the result depends on frontier
iteration order.

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
