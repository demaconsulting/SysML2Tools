#### GeneralViewLayoutStrategy Verification

##### Verification Approach

`GeneralViewLayoutStrategy` is verified through unit tests in `GeneralViewLayoutStrategyTests`
that construct a synthetic `SysmlWorkspace` (or, for the end-to-end and gallery cases, load real
model text through `WorkspaceLoader`), invoke `BuildLayout`, and assert on the returned
`LayoutTree`. Recursive helpers collect boxes, lines, and labels from the (possibly nested) node tree
so assertions can confirm box keywords, shapes, appearance, compartments, and the end marker, line
style, endpoints, and direction of every specialization, feature membership, typing, subsetting,
reference subsetting, redefinition, satisfy, verify, import, connect, allocate, dependency, and
binding line. Assertions are on behavior (markers, endpoints, styles, relative positions), not on
absolute coordinates. The node policy is selected through the strategy's `internal` constructor, so
the default (Pilot) behavior and each alternative policy are tested by passing the policy under test.
No mocking is required; the strategy depends only on the in-memory model and render options, all
constructed directly by the tests. (Phase 2a) Bracket-form `expose` filter-narrowing behavior is
delegated to `ExposeScopeResolver` and is verified directly by `ExposeScopeResolverTests` (see
`expose-scope-resolver.md`) plus an end-to-end `RenderIntegrationTests` case exercising the full
rendering pipeline; both are cross-referenced here since the observable effect on
`GeneralViewLayoutStrategy`'s rendered output is part of this unit's requirement coverage.

The layout engine, the end markers, the box appearance, and the renderers are off-the-shelf
(`DemaConsulting.Rendering`), so the tests of this unit also serve as the evidence for the OTS
requirements `SysML2Tools-OTS-DemaRendering-NotationStyling` and
`SysML2Tools-OTS-DemaRendering-NestedDirectionAndAutoLayout` (see the OTS verification chapter).

In addition to the automated tests, the gallery images of this view are verified visually, because
overlap, crossing, and whitespace quality cannot be asserted exhaustively by unit tests. The
diagram-quality checklist (inventory I1–I3, blocks B1–B5, connectors C1–C10) is applied to each image
in the list under *Acceptance Criteria*, and each image is compared with the notation target (glyph
per relationship, dashed reference usages, italic abstract definition names, centered italic
compartment titles, names in folder tabs, no `«package»` line, dashed comment links without an arrowhead).

##### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, net10.0, and net11.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- All `GeneralViewLayoutStrategyTests` pass with zero failures across all four target frameworks.
- Each user definition appears as a box carrying its definition keyword.
- A definition's name line shows the declared short name and specialization in the textual
  notation (`<PD2> PartDef2`, `Car :> Vehicle`, `<T> Truck :> Vehicle, Hauler`), and the box is at
  least as wide as that label; `FormatNameLine` omits every null or empty piece and orders the
  pieces short name, name, supertypes, typing, multiplicity.
- A top-level usage and a usage nested in a box are rounded-rectangle boxes, while a definition is a
  plain rectangle.
- A usage's keyword carries its prefix words inside the guillemet marks (`abstract part`, `variation part`,
  `individual part`, `variant part`, `ref part`), a plain `ref` usage reads `ref` once, and the name line
  reads `name : Type [multiplicity] ordered nonunique :> subsetted :>> redefined ::> referenced` with a
  conjugated typing shown as `~Type`, omitting every piece the model does not declare.
- A reference usage has a dashed outline and a composite usage a solid one. An abstract definition has an
  italic name and the `abstract part def` keyword; an abstract usage does not have an italic name.
- By default (the Pilot policy) a part usage is a rounded node placed beside its owner in the owner's
  scope (the folder), not inside the owner's box, and is no longer listed as a row; a usage nested in a
  usage is a node joined to its owner usage; an attribute usage, a plain reference usage, and a port
  without nested features stay compartment rows of the owner and draw no node and no edge; a port that
  owns nested features is a node joined to its owner by a filled diamond and lists its features in its
  own compartments.
- Under the all-usages policy an attribute usage and a plain port are nodes (the attribute joined by a
  hollow diamond); under the row-only policy no usage is a node, every usage stays a row, and no
  membership or typing edge is drawn. An unnamed usage, a usage without a keyword, and a constraint
  expression are never nodes.
- Two usage nodes of different owners that read exactly alike are both drawn and the layout completes.
- With a depth limit that does not permit an owner's usage nodes, the owner stays a leaf box whose last
  compartment is the untitled `…` row, and no usage node or edge is drawn. A recursive `expose`, a
  namespace `expose`, and an exact `expose` of a definition or node-kind usage all draw the node usages the
  exposed element owns as nodes joined to it (and the node usages those own in turn), with no `parts` row;
  a typing edge appears only to a definition that is exposed too; a bracket-filtered `expose` leaves the
  parts of the selected elements as rows; and no element is drawn both as a node and as a row, or twice.
- A package folder that has content carries its name in the tab (`FolderTitlePlacement.Tab`) and keeps
  the `Folder` shape; an exposed package without content and a depth-truncated package keep the name
  inside the body.
- When the view has no resolved `expose` scope, a bare package's definitions appear inside a
  folder-shaped box labeled with the package name and carrying no keyword (no `«package»` line).
- A definition that owns nested definitions appears as exactly one box, with its nested
  definitions placed inside that box (as its own `Children`) rather than as a duplicate sibling
  container — regardless of whether the view is scoped or unscoped.
- When the view has a resolved `expose` scope, a bare package is never rendered as a wrapping
  folder merely because it is an ancestor of admitted content; the package's admitted items are
  promoted directly to the diagram's root instead.
- A box's owned features that are rows appear as compartment rows (`name : Type`, with direction,
  conjugated type, multiplicity and modifiers, subsetting, redefinition, reference-subsetting, and
  value clauses as declared), and every compartment title is centered and italic.
- A definition's compartments are ordered `doc` first, then `attributes`, `enums`, `parts`, and
  `ports`, then every other group in declaration order, regardless of the order the usages are
  declared in.
- When the depth limit hides a definition's nested definitions, the definition is a single leaf box
  (no placeholder child box) whose last compartment is an untitled `…` row, appended after any
  compartments the definition already has, making the box taller than the same untruncated leaf.
- A specialization yields a line with an open end marker at the supertype end.
- A composite usage (`part`, `port` with nested features, nested usage) yields a solid line from the
  usage box to its owner box with a filled-diamond end marker at the owner.
- A reference usage (and an attribute usage under the all-usages policy) yields a solid line to its
  owner with a hollow-diamond end marker at the owner.
- A usage whose type resolves to a rendered definition yields a solid line from the usage to the
  definition with a hollow-triangle-with-two-dots end marker at the definition, including an
  enumeration-typed attribute under the all-usages policy and a conjugated typing (to the underlying
  port definition); two usages of the same type yield two lines; a type that does not resolve yields no
  typing edge.
- An `attribute`-feature shown as a row yields neither a diamond edge nor a typing edge.
- Without an in-view resolution (a private member, a candidate a filtered import rejects, a name
  reachable only through another package's import), no edge is drawn.
- A dense model with many part edges produces a layout in which no two definition boxes overlap,
  confirming `LayeredPlacement` keeps boxes separated.
- A connected model (many cross-referencing part edges) produces a layout in which all definition
  boxes remain mutually non-overlapping.
- A sparse model (two boxes and one edge) produces a compact canvas with no warnings, confirming the
  delegated layout does not over-pad sparse diagrams.
- Standard-library-only input (by prefix or by seed set) yields a minimal empty canvas.
- An empty workspace yields a 200×100 canvas with no nodes.
- A view whose `ViewContext.ViewNode` carries a resolved `Expose` edge scopes the diagram to that
  target's containment subtree, excluding unrelated sibling definitions and producing fewer boxes
  than an unscoped (no-`ViewNode`) rendering of the same workspace.
- A view whose `ViewContext.ViewNode` carries a `RenderTargetName` but no resolved `Expose` edges
  renders the full workspace, byte-identical to a view with a `null` `ViewNode` — proving
  `RenderTargetName` never affects scope, regardless of `FilterExpressionText`.
- A view whose resolved `Expose` edge names a feature usage (not a definition) still renders that
  usage's type's containment subtree, by additionally resolving the usage's own `Typing` edge —
  the fix for the usage-vs-definition containment gap.
- (Phase 2a) A view whose `expose <path>::**[<expr>];` entry carries a bracket-filter expression
  that parses and evaluates successfully narrows that entry's contribution to only the descendant
  definitions the expression matches, rather than that entry's whole containment subtree; end-to-end
  rendering of such a view produces SVG output containing only the matched definitions, with no
  "could not be evaluated" warning.
- (Phase 2a) When one `expose` entry in a view carries a successfully-evaluated bracket filter and
  another entry in the same view carries none, each entry narrows independently — the unfiltered
  entry still contributes its whole containment subtree while the bracket-filtered entry
  contributes only its matched definitions.
- A view whose `ViewContext.ViewNode` carries a supported `FilterExpressionText` narrows the
  already expose-scoped candidate definitions to the matched subset, including the empty-set case.
- A view whose `ViewContext.ViewNode` carries an unsupported or malformed `FilterExpressionText`
  emits a "could not be evaluated" warning through `LayoutTree.Warnings`, while still rendering
  the resolved (unfiltered) scope.
- A view with a `null` `ViewContext.ViewNode` (the `--auto` synthesized-view path, and the
  pre-scoping-change 2-argument `ViewContext` construction used throughout the rest of this test
  file) renders every non-stdlib definition in the workspace, unchanged from before this feature —
  the critical regression guard confirming full backward compatibility.
- A usage that redefines a bare-named inherited usage (declared by a resolved supertype of its owner)
  yields a solid line from the redefining usage box to the redefined usage box with a
  hollow-triangle-crossbar end marker at the redefined usage.
- A bare-name redefinition whose declaring ancestor is two or more supertype hops away targets the
  usage of the actual declaring ancestor, not the immediate supertype, proving the walk is transitive.
- A usage that redefines a qualified `Owner::feature` reference yields a hollow-triangle-crossbar edge
  directly to the named usage, without needing the owner to be an immediate or transitive supertype.
- An unresolvable redefinition reference produces no edge, and layout completes without throwing.
- A genuinely self-referential redefinition produces no edge, and layout completes without throwing.
- A resolved `Connect` edge whose endpoints map (via `ResolveOwningBox`) to two distinct rendered
  boxes yields a solid line with no end marker between them — including the dominant real-world
  shape (two sibling features declared directly in their owning `part def`s, resolved via
  `ReferenceResolver`'s instance-path-preserving type-hierarchy fallback), verified end to end
  with the real `WorkspaceLoader` and `BuildLayout`, not just a hand-built fixture.
- A resolved `Connect` edge whose endpoints both map to the same rendered box (a genuine self-loop
  — e.g. two sibling features of the same enclosing definition with no distinguishing owner)
  yields no edge, and the drop is surfaced as a `Connect`-kind warning via
  `LayoutWarnings.ForDroppedRelationshipEdges`.
- A resolved `Allocate` edge yields a solid line with an open-chevron end marker at the target
  and a `«allocate»` midpoint label.
- A resolved `Dependency` edge yields a dashed line with an open-chevron end marker at the target
  and a midpoint label: the dependency's declared name when it is named, otherwise `«depend»`.
- A resolved `Binding` edge whose endpoints map to two distinct rendered boxes yields a solid line
  with no end marker and an `=` midpoint label.
- A usage that subsets a bare-named or qualified inherited usage, or a sibling usage of the same
  definition, yields a solid line from the subsetting usage box to the subsetted usage box with a
  hollow-triangle end marker at the subsetted usage.
- A subsetting reference that resolves back to the subsetting usage itself produces no edge, and
  an unresolved reference never takes the target another reference of the same usage resolved to.
- A usage that reference-subsets another (`::>`) yields a solid line with a
  hollow-triangle-with-four-dots end marker at the referenced usage; an unresolvable reference draws
  nothing.
- A resolved `satisfy` relationship yields a solid line with an open arrowhead from the satisfying box
  to the requirement box, labeled `«satisfy»`; a resolved `verify` relationship is labeled `«verify»`,
  and a verifying declaration nested in a rendered box (an objective) is drawn on the nearest rendered
  box on its path.
- An import yields a dashed line with an open arrowhead from the importing package's folder (or box) to
  the imported package's folder (or box), labeled `«import»` for a membership import, `«import»*` for a
  namespace import, and `«import»**` for a recursive one; an import of the standard library and an import
  between a package and a package nested in it draw no edge and no warning.
- Any `Connect`/`Allocate`/`Dependency`/`Binding` edge whose endpoint fails to resolve to a
  rendered box, or whose endpoints resolve to the same box, is surfaced as a warning in
  `LayoutTree.Warnings` (defense-in-depth diagnostic) — except an unresolved-endpoint drop caused
  solely by the endpoint falling outside an active `expose` scope narrowing, which is expected
  behavior and produces no warning.
- Every definition or usage rendered as a box with one or more `Comment` annotations gets exactly one
  `BoxShape.Note` box, connected to the element's own box by a plain dashed line with no end
  marker; an element with no annotations gets no note box. A nested part's `doc` is a compartment of
  the part's own box (not of its owner), and a `comment` on the nested part is a note joined to the
  part's own box. Under the default Pilot `NodePolicy`, attributes, plain references, and ports without
  nested features stay compartment rows of their owner, and the documentation and comments of such a
  row are not rendered (consistent with the Pilot implementation); this is verified only for usages
  rendered as their own boxes, and the row limitation is recorded as a follow-up in `ROADMAP.md`
  (annotating a row through a dashed link to the row, which the specification permits).
- A `"subject"`/`"assume constraint"`/`"require constraint"`/`"constraint"`-keyword feature
  compartment is titled with the guillemet-wrapped stereotype form (e.g. `«subject»`), not the
  generic pluralized-keyword default.
- A constraint-kind feature (non-null `ExpressionText`) renders its raw expression text in place
  of the generic `name : Type [multiplicity]` row shape.
- An `"enum value"`-keyword feature compartment is titled `"enums"`.
- `CollectDefinitions` admits named usage-level (`SysmlFeatureNode`) candidates alongside
  definitions (Phase 2d), so a bare or `SysML::`-qualified metaclass-kind filter
  (`filter @PartUsage;`/`filter @SysML::PartUsage;`) renders only the matching usage-kind boxes,
  reproducing the OMG `42.Views/ViewsExample.sysml` pattern, instead of an empty canvas.
- With no filter present at all, usage-level candidates render as boxes too, except a usage that its
  rendered owner lists as a compartment row under the node policy: `RemoveRowUsages` excludes such a
  usage from standalone rendering, keeps a usage that is a node and records its owner, and never silently
  drops a usage nested two or more levels deep whose intermediate usage is itself a row (that usage
  instead survives as its own standalone box).
- The OMG Safety feature-views fixture's exposed-vehicle-subtree scope renders a non-empty scoped
  diagram containing the vehicle's part usages (e.g. `seatBelt`, `bumper`) while still excluding
  `Safety`/`Security` from the scoped result.
- A usage nested directly inside an independently-rendered definition does not also get a standalone
  box when the policy shows it as a row — the direct regression reproduction of the box-count defect.
- A nested usage whose immediate parent is excluded from the final rendered set by an active
  metaclass filter (rather than by scope) still renders as its own standalone box, proving
  `RemoveRowUsages` runs after — not before — standalone filter narrowing.
- The real gallery corpus model `docs/gallery/models/01-drone-general.sysml`'s `DroneGeneralView`
  renders exactly 21 square-cornered definition boxes — the regression guard for the box-count defect;
  its part usages are additional rounded boxes.
- A usage nested two or more levels deep (e.g. `part def A { part b { part c; } }`) under the row-only
  policy renders as its own standalone box (`c`) when its intermediate parent (`b`) is itself a row:
  exactly 2 boxes render (`A` and `c`).
- A package nested in another drawn package appears as a folder inside the owning package's
  folder (as the owner box's own `Children`), never as a separate folder beside it; an enclosing
  package that owns no definitions directly still gets its folder when a nested package is drawn;
  in a scoped view a nested exposed package sits inside its exposed owner, and only the nearest
  exposed enclosing package (or the root, when none is exposed) holds it.
- A model relationship between two drawn definitions is drawn as a single connector between the two
  endpoint boxes themselves — across sibling packages, between a nested package and its owner,
  and across any depth of nesting — declared in the innermost container scope that encloses both
  endpoints (the root for sibling top-level packages, the owner folder for a nested-versus-outer or
  sibling-subpackage pair); a relationship between a definition and a definition it contains lays
  out without error.
- The depth limit counts each enclosing package folder as one nesting level: a limit of 1 collapses
  the top-level folder to a bare `…` label, a limit of 2
  keeps the top-level folder's own definitions and collapses the nested folder, and a relationship to
  a definition hidden by the limit draws no connector.
- A bare `…` truncation label is attached to the box of the truncated package folder regardless of the order
  in which the layout engine emitted a scope's boxes, at the root and inside nested containers.
- The gallery model `docs/gallery/models/11-nested-packages-general.sysml` renders `Powertrain` and
  `Body` folders inside the `Vehicle` folder with the cross-package specialization and the
  compositions into `Car` drawn in the `Vehicle` scope between the actual endpoint boxes.
- The relationships that leave a package folder are drawn without crossing one another in the
  deep-nesting and sibling-nested-package examples, whichever way the folder's definitions were declared
  (the layout engine orders a container's interior by its cross-hierarchy relationships); a folder with
  no relationship leaving it keeps declaration order; and a relationship to a partner hidden by the depth
  limit does not disturb the layout. For the nested-package vehicle model the declaration order does not
  change any box position and
  no two definition boxes overlap; a residual crossing between its cross-folder connectors is a layout
  engine routing outcome that is not asserted by the tests and is judged in the visual verification.
- The diagram flows top-to-bottom: the owner is placed above its owned usage, the supertype above the
  specialized definition, and the definition typing a usage below the usage; a specialization and a
  membership line start at the lower box and end, with the marker, at the upper box, while a typing line
  starts at the usage and ends, with the marker, at the definition below it. For this direction the
  diagram-quality rule "marker on the right face of the source and the left face of the target" (C8) is
  read as: a connector toward an owner or supertype leaves the top face of the lower box and enters the
  bottom face of the upper box, and a typing connector leaves the bottom face of the usage and enters
  the top face of the definition.
- A model of one definition owning several usages is compact: the usages of the owner share one row
  directly below it, each typing definition hangs below its usage, and the drawing stays within the
  asserted size, with no overlap and every connector waypoint inside the canvas.
- Twelve unconnected definitions and a specialization pair, in a folder and at the root, are drawn
  without overlap; a connector between two root-level folders lies entirely inside the canvas.
- A connector end decoration that is reached over a lane has a straight final leg of at least the
  decoration length plus one corner radius; the lanes of one box face keep their order and are at least
  8 px apart; a connector with one bend, a face whose move would leave a leg at the other box too short,
  and a face whose move would add a crossing or a near-parallel overlap keep the engine's routing.
- Definitions that no relationship touches are placed, when the connected part of their scope leaves room,
  inside the bounds of that part (clear of every box, connector, and label by 12 px) and the canvas is then
  smaller than with the engine's packing; with no room, or with no connected content in the scope, the
  engine's packing is kept. For the quadcopter gallery model the leaf blocks cover at least 20 % of the
  canvas and the largest empty square is at most three times the average leaf block height.

###### Visual verification of the gallery images

Each of the following General View images (`docs/gallery/png/*.png`, regenerated from
`docs/gallery/models/` with the command in `docs/gallery/README.md`) is viewed and judged against the
inventory (I1–I3), block (B1–B5), and connector (C1–C10) criteria and against the notation target.
Because the diagram flows top-to-bottom, C8 is applied in the top-to-bottom reading given above.

The measurements below use the checked-in PNGs: an obstacle is every non-white pixel grown by 12 px, the
largest empty square is found on a 4 px grid, a leaf block is a definition or usage rectangle (the
package frames and notes are not counted), the B4 limit is three times the average leaf block height, and
the B5 threshold is 20 % of the canvas.

| Image | Canvas | Leaf blocks | Largest empty square (limit) | Leaf share of canvas |
| --- | --- | --- | --- | --- |
| `DroneGeneralView` | 1644×1330 | 29 | 220 px (243 px) | 21.2 % |
| `BatterySubsystemView` | 423×286 | 1 and a note | 120 px (438 px) | 18.3 % (27.8 % with the note) |
| `MissionControlGeneralView` | 404×588 | 7 | 160 px (224 px) | 24.2 % |
| `GroundSegmentRecursiveView` | 372×286 | 6 | 52 px (236 px) | 48.0 % |
| `VehiclePackagesGeneralView` | 552×939 | 8 | 116 px (150 px) | 14.8 % (36.2 % with the nested frames) |
| `DriveUnitGeneralView` | 1076×592 | 9 | 120 px (166 px) | 15.1 % |
| `ChargerGeneralView` | 550×410 | 5 | 100 px (208 px) | 27.4 % |
| `VehicleTypingGeneralView` | 605×554 | 7 | 100 px (150 px) | 19.1 % (46.8 % with the nested frames) |

The rows are re-measured on rendering engine 0.3.0-beta.5, which sizes containers tightly.

Before the placement of the unconnected definitions, the quadcopter view was 1959×1308 px with a 460 px
empty square and a leaf share of 17.6 %; the result above is therefore a pass of B4 and B5, checked on
every change by `GeneralViewLayoutStrategy_BuildLayout_DroneGalleryModel_MeetsBlockPlacementCriteria`.

- `01`, `DroneGeneralView`: 30 boxes (21 definitions, 8 usage nodes, 1 note); owner diamonds and the
  subsetting, redefinition (as an attribute row), typing, allocate, dependency, connect, and note-link
  lines all traceable; ports are rows. The nine definitions that nothing refers to form a block of three
  columns in the free area right of `Battery` and below `ImuSensor` and `GpsSensor`. Every owner-end
  decoration has a clean straight approach (final legs of 15 px for the diamonds, 11 px for the open
  chevrons; C10). Four connector crossings remain, all engine routing outcomes that are not asserted: the
  `connect` line from `controller` to `battery` crosses the typing connector of `motors`, the
  subsetting connector of `frontMotors`, and the composition connector of `RacingDrone`, and the dashed
  dependency from `FlightController` (labeled `«depend»`, the label sitting clear on the horizontal leg of
  the dashed line) crosses the specialization from `RacingMotor`.
- `01`, `BatterySubsystemView`: scoped view; `doc`, attributes, and ports compartments; note below its
  element with a dashed link.
- `17`, `VehicleStructureGeneralView` (569×546, 5 usage nodes and 2 definitions): `expose Vehicle;
  expose Engine;` draws `Vehicle` with its parts `engine`, `wheels`, and the dashed `driver` (◇) as nodes,
  `hub` chained below `wheels`, and `cylinders` below `Engine`; `mass` and `displacement` stay rows;
  the typing glyph appears only for `engine : Engine`, since `Wheel` and `Driver` are not exposed; the
  named dependency shows its name `fueling` clear of every line. No overlap or clipped label.
- `10`, `MissionControlGeneralView` and `GroundSegmentRecursiveView`: nested folders and containers with
  names in tabs, no edges.
- `11`, `VehiclePackagesGeneralView`: nested folders, cross-package specialization, typing glyph at
  `Engine` and `Chassis`; one residual crossing: the `Hybrid` to `Engine` connector crosses the
  `chassis` to `Chassis` typing connector just right of `Engine` (engine routing, not asserted).
- `14`, `DriveUnitGeneralView`: square definitions, rounded usages, dashed `«ref part»`,
  `«abstract part def»` with an italic name, ◆ versus ◇ at the owner, typing, subsetting, redefinition,
  and specialization glyphs, `doc` compartment, note; every owner-end decoration has a clean approach
  (final legs of 13.5 to 15 px for the triangles and the diamonds). Three connector crossings remain,
  all engine routing outcomes that are not asserted: the `frontMotor` subsetting connector crosses the
  dashed note link and the `main` to `Motor` typing connector, and the `mainMotor` redefinition
  connector crosses the `frontMotor` to `RearDrive` composition connector. The order in which the strategy
  hands the boxes and edges to the engine cannot remove them: the same ten boxes and thirteen edges laid
  out directly with the engine in 1500 random insertion orders give 3 crossings at best (3 in 620 orders,
  4 in 687, and more in the rest), and the engine's own ordering gives 3. The cause is that `frontMotor`
  must sit under both `motors` and `RearDrive` while `main` (with its note) lies between them.
- `15`, `ChargerGeneralView`: `doc` compartments and the dashed comment link without arrowhead on
  nested parts; both diamonds at `Charger` have a 15 px clean approach.
- `16`, `VehicleTypingGeneralView`: tab names, ◆ at `Car`, typing glyph with dots at the definition,
  triangles.

Owner waivers and limits. The following results do not meet the checklist and are waived explicitly, each
caused by the layout engine and not hand-tuned (the engine items are in `ROADMAP.md`):

- W1, `VehiclePackagesGeneralView`: one residual crossing (B4 is now met: 116 px against a limit of 150 px,
  where it was 172 px before the engine sized containers tightly in 0.3.0-beta.5). The
  tall portrait follows from `Hybrid` (in `Body`) specializing `Engine` (in `Powertrain`): the engine
  places a folder's externally connected single box after the connected group whatever the declaration
  order, and ordering the folder items by the partner's vertical position does not change the picture.
  The result is far better than the 1407×398 strip of the former layout.
- W2, `MissionControlGeneralView`: B4 is met (160 px against 224 px; it was 196 px before beta.5), because
  the engine packs the two top-level folders side by side. The layout is smaller than before.
- W3, B5 for the diagrams that are dominated by package frames and tiers: the leaf share is 14.8 %
  (`VehiclePackagesGeneralView`) and 19.1 % (`VehicleTypingGeneralView`) when only the leaf blocks are
  counted (`MissionControlGeneralView` is now 24.2 % and passes), and 36.2 % and 46.8 %
  when the nested package frames are counted as blocks. The package frame, its tab, the layer spacing,
  and the room for the connector lanes are constants of the engine (`CoreOptions.LayerSpacing` is honored
  only as a floor, 70 px by default, and `CoreOptions.NodeSpacing` is only a floor, so the strategy
  cannot tighten them); counting the frames as blocks is the reading under which these two pass.
  `DriveUnitGeneralView` (15.1 %) has a single package frame, so the reading does not help it: it is a
  wide, three-tier tree of nine boxes whose spacing and lanes are the engine's, and its largest empty
  square (120 px against 166 px) passes B4.
- W5, there is no gallery model for requirements and imports (`satisfy`, `verify`, `import`, `::>`): the
  usage form of a requirement renders crossing, overlapping connectors and tangled labels and keeps no
  typing, and the definition form uses requirement definitions as `satisfy` and `verify` targets, which the
  specification defines for requirement usages. These relationships stay covered by unit tests and are
  recorded in `ROADMAP.md`.

Two connectors of `DriveUnitGeneralView` have a short lateral step of 2.6 to 3.4 px (cosmetic): the
`InductionMotor` to `Motor` specialization connector, 10 px below `Motor`, and the `spare` to
`InductionMotor` typing connector, 60 px above the definition. Both come from the layout engine, not from
this strategy: the first layout (before `FitConnectorsToCanvas`, `OrientEdges`, and
`ClearEndDecorations`) already contains both steps with the same size (`ClearEndDecorations` only moves the
bar of the specialization connector 5 px away from the triangle at `Motor`, and changes neither step). The
engine spreads the connectors that end on one box face evenly over that face (here two connectors at a
quarter and three quarters of the width of `InductionMotor`) and does not align such a port with the port
at the other end, so a source port at the middle of `Motor` and a target port 3.4 px away give a Z-shaped
route with a 3.4 px bar. The effect needs no reversed edge and no code of this strategy: a plain graph of
three boxes (`U` above `M` above `I`, edges `U` to `M`, `M` to `I`, and the long edge `U` to `I`, all
declared top-down, sizes of the gallery model) laid out with `DemaConsulting.Rendering.Layout`
0.3.0-beta.3 gives steps of 2.6, 2.6, 0.4, and 0.5 px. The renderer shortens the corner radius to the step, so
the step reads as a tiny S-bend. Aligning those ports needs engine support (ROADMAP, rendering engine
items). In `VehiclePackagesGeneralView` (left face) and
`VehicleTypingGeneralView` (right face), one typing connector enters `Engine` through a side face
instead of the top face; this is the engine's cross-hierarchy routing and is not asserted.

The raw largest empty square of `VehiclePackagesGeneralView` is 199 px against the 172 px
quoted above for the dilated measurement; both exceed the 150 px limit, and W1 covers both.

##### Test Scenarios

- `GeneralViewLayoutStrategy_BuildLayout_EmptyWorkspace_ReturnsMinimalCanvas`:
  200×100 canvas with no nodes
- `GeneralViewLayoutStrategy_BuildLayout_StdlibOnlyWorkspace_ReturnsMinimalCanvas`:
  Stdlib defs excluded; no nodes
- `GeneralViewLayoutStrategy_BuildLayout_OneUserPartDef_ProducesLayoutBox`:
  A user part def produces at least one box
- `GeneralViewLayoutStrategy_BuildLayout_MixedDefinitionKinds_RendersAllWithKeywords`:
  Each def carries its keyword
- `GeneralViewLayoutStrategy_BuildLayout_PackagedDefinitions_ProducesFolderBox`:
  Folder box labeled with the package name and carrying no keyword
- `GeneralViewLayoutStrategy_BuildLayout_TopLevelUsage_IsRoundedAndDefinitionIsSquare`:
  A top-level `part` usage is a `RoundedRectangle` box and its `part def` a `Rectangle` box
- `GeneralViewLayoutStrategy_BuildLayout_PilotPolicy_PartUsage_IsOwnNodeBesideOwner`:
  A part usage owned by a definition is a rounded sibling of the owner inside the folder, labeled
  `wheel : Wheel` with keyword `part`; the owner has no children and no compartments for it
- `GeneralViewLayoutStrategy_BuildLayout_PilotPolicy_AttributeAndPlainReference_StayRows`:
  An attribute and a plain `ref` stay rows (`attributes`, `references` compartments) while a part of the
  same owner becomes a node
- `GeneralViewLayoutStrategy_BuildLayout_PilotPolicy_NestedUsageInUsage_ChainsOwners`:
  `part def A { part b { part c; } }` draws the owner chain A, b, c with a filled diamond from each
  usage to its owner
- `GeneralViewLayoutStrategy_BuildLayout_RowsOnlyPolicy_PartUsage_StaysRowAndDrawsNoEdges`:
  Under the row-only policy a part stays a row and no ownership or typing edge is drawn
- `GeneralViewLayoutStrategy_NodePolicy_IsNode_KeywordMatrix_ReturnsExpected`:
  Theory over policy and keyword: Pilot nodes are the occurrence-kind usages, attributes, plain
  references, plain ports, enumeration values, and constraints stay rows; the all-usages policy draws
  every named keyword usage except constraints and enumeration values; the row-only policy draws none
- `GeneralViewLayoutStrategy_NodePolicy_IsNode_UnnamedWithoutKeywordOrExpression_IsNeverNode`:
  An unnamed usage, a usage without keyword, and an expression usage are never nodes in any policy
- `GeneralViewLayoutStrategy_NodePolicy_IsNode_PilotPort_IsNodeOnlyWithNestedContent`:
  A port is a Pilot node only when it owns at least one named nested feature
- `GeneralViewLayoutStrategy_BuildLayout_UsageKeyword_PrefixWords_AreInsideTheKeyword`:
  Theory: `abstract part`, `variation part`, `individual part`, `variant part`, `ref part`
- `GeneralViewLayoutStrategy_BuildLayout_PlainReferenceUsage_KeywordIsNotDoubled`:
  A plain `ref` usage reads `ref` once
- `GeneralViewLayoutStrategy_BuildLayout_UsageNameLine_ShowsTypeMultiplicitySubsettingRedefinitionAndReferenceSubsetting`:
  The name line lists short name, name, typing, multiplicity, ordering, then subsetting, redefinition, and
  reference subsetting
- `GeneralViewLayoutStrategy_BuildLayout_ReferenceUsage_OutlineIsDashedAndCompositeUsageIsSolid`:
  Only a reference usage has a dashed outline
- `GeneralViewLayoutStrategy_BuildLayout_AbstractDefinition_NameIsItalicButAbstractUsageIsNot`:
  An abstract definition has an italic name and the `abstract part def` keyword; an abstract usage's name
  is not italic
- `GeneralViewLayoutStrategy_BuildLayout_FeatureRows_UseTextualNotation`:
  A row shows the direction keyword, conjugated type, multiplicity with modifiers, subsetting and
  redefinition clauses, and the value clause
- `GeneralViewLayoutStrategy_BuildLayout_PackageFolder_NameInTabOnlyWhenItHasContent`:
  A folder with content asks for the name in the tab; an empty exposed package and a truncated package keep
  the name in the body
- `GeneralViewLayoutStrategy_BuildLayout_DepthLimit_OwnerWithUsageNodes_StaysLeafWithEllipsisRow`:
  A depth limit that does not permit the usage nodes leaves the owner a leaf with a trailing `…` row and
  draws no usage node or edge
- `GeneralViewLayoutStrategy_BuildLayout_ScopedExpose_RecursiveAndNamespace_BothDrawUsageNodesAsNodes`:
  `expose P::**` and `expose P::*` both draw the exposed definition's usage as a node joined by a filled
  diamond, with no `parts` row
- `GeneralViewLayoutStrategy_BuildLayout_ExposedPartDef_DrawsOwnedPartsAsNodesJoinedByFilledDiamond`:
  `expose V;` draws each part of `V` as a rounded node joined by a filled diamond; the attribute stays a
  row, there is no `parts` compartment, and the typing definitions are not pulled into the diagram
- `GeneralViewLayoutStrategy_BuildLayout_ExposedPartDefAndItsTypes_DrawsTypingEdgeFromUsageToDefinition`:
  A typing edge (hollow triangle with two dots) joins a part to its definition only when the definition
  is exposed too
- `GeneralViewLayoutStrategy_BuildLayout_ExposedPartDef_ReferencePartUsesHollowDiamondAndDashedNode`:
  A `ref part` of an exposed definition is a dashed node joined by a hollow diamond
- `GeneralViewLayoutStrategy_BuildLayout_ExposedPartDef_NestedInlineUsageChainsOwners`:
  A part nested inline in a part of an exposed definition is drawn too, chained to its owner
- `GeneralViewLayoutStrategy_BuildLayout_RecursiveExpose_DoesNotDuplicateUsageNodes`:
  A recursive expose, alone or combined with the exposed owner in either order, draws every box once and
  no part also as a row
- `GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_DrawsOwnedPartsAsNodes`:
  An exposed part usage draws the parts it owns as nodes
- `GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_TypingDefinitionIsBoxWithoutPartTree`:
  `expose myVehicle;` draws the typing definition as a box whose own parts stay `parts` rows, while the
  parts the exposed usage owns are nodes
- `GeneralViewLayoutStrategy_BuildLayout_BracketFilteredExpose_KeepsUnselectedUsagesAsRows`:
  A bracket-filtered expose selects the definition but leaves its parts as `parts` rows
- `GeneralViewLayoutStrategy_BuildLayout_DepthLimit_ExposedDefWithUsageNodes_TruncatesToEllipsisThenDrawsNodes`:
  The depth limit applies to part trees as to unscoped views: a leaf with a trailing `…` row at limits one
  and two, the part nodes at limit three
- `GeneralViewLayoutStrategy_BuildLayout_ViewConditionRejectingUsage_KeepsItAsRow`:
  A view condition that rejects the usages drops the part nodes and the owner lists them as rows
- `GeneralViewLayoutStrategy_BuildLayout_IdenticalUsageNodesOfDifferentOwners_AreBothDrawn`:
  Two usage nodes that read exactly alike (`data : P`) are both drawn and the layout completes
- `GeneralViewLayoutStrategy_BuildLayout_TopDown_OwnerAndGeneralAboveUsageAndDefinitionBelow`:
  The owner and supertype are above the owned usage and the specialized definition, the typing definition
  is below the usage, and the connectors keep their notation direction
- `GeneralViewLayoutStrategy_BuildLayout_DroneLikeModel_IsCompactAndTopDown`:
  A definition owning eight part usages: the usages share one row below the owner, each typing definition
  hangs below its usage, the size stays within 2000 × 1300, nothing overlaps, and every waypoint is inside
  the canvas
- `GeneralViewLayoutStrategy_BuildLayout_TopDown_UnconnectedDefinitionsDoNotOverlap`:
  Twelve unconnected definitions and a specialization pair in a folder and at the root do not overlap
  (guards the automatic algorithm selection for folders)
- `GeneralViewLayoutStrategy_BuildLayout_TopDown_ConnectorsBetweenRootFolders_StayInsideCanvas`:
  Every waypoint of every connector lies inside the canvas (guards the canvas fit)
- `GeneralViewLayoutStrategy_BuildLayout_OwnerFanOut_EndDecorationsHaveCleanApproach`:
  A charger-like owner with two part usages: both filled diamonds are drawn, each sits on a straight final
  leg at least as long as the diamond (whether the engine routes it straight or over a lane), and every one
  that ends a lane has a final straight leg of at least the diamond length plus the corner radius
- `GeneralViewLayoutStrategy_BuildLayout_SpecializationAndReferenceUsageFanOut_EndDecorationsHaveCleanApproach`:
  A hollow diamond, a filled diamond, and a specialization triangle on one owner face all have a clean
  approach
- `GeneralViewLayoutStrategy_BuildLayout_FanOutLanes_KeepDistinctOrderedLanes`:
  The lanes of a seven-usage fan-out are on the far side of the owner, lanes whose extents overlap are at
  least 8 px apart, the nearest lane clears the decoration, and no two lane segments are collinear and
  overlapping
- `GeneralViewLayoutStrategy_BuildLayout_GalleryGeneralViews_EndDecorationsHaveCleanApproach`:
  Real-corpus guard over the gallery General Views (drone, battery, nested packages, drive unit,
  charger, vehicle typing) with the tool's plain theme, and the drive-unit and charger views also with
  the light theme: every decorated connector that ends a lane has a clean approach
- `GeneralViewLayoutStrategy_BuildLayout_ManyUnconnectedDefinitions_AreTuckedIntoFreeCornerOfWideTree`:
  Nine definitions that nothing refers to lie inside the bounds of a wide connected tree, clear of every
  connector, inside the canvas, without overlap, and the canvas is not wider than the connected drawing
  plus its margin
- `GeneralViewLayoutStrategy_BuildLayout_UnconnectedDefinitions_NoFreeSpace_KeepEngineShelf`:
  Five isolated definitions beside a two-box tree are drawn outside the tree's bounds (the engine's
  packing), without overlap
- `GeneralViewLayoutStrategy_BuildLayout_UnconnectedDefinitions_InNestedFolder_AreTuckedInsideThatFolder`:
  The isolated definitions of a nested folder stay inside that folder's box, clear of connectors
- `GeneralViewLayoutStrategy_BuildLayout_AllDefinitionsUnconnected_KeepsEngineLayout`:
  A folder of six unrelated definitions keeps the engine's packing: six boxes inside the folder, no overlap
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionWithCommentNote_IsNotDeferred`:
  A definition with a comment is joined to its note by a dashed line, so it is part of the connected
  drawing
- `GeneralViewLayoutStrategy_BuildLayout_DroneGalleryModel_MeetsBlockPlacementCriteria`:
  The quadcopter gallery model: leaf blocks cover at least 20 % of the canvas and the largest empty
  square (obstacles grown by 12 px) is at most three times the average leaf block height
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionNameLine_ShowsShortNameAndSupertypes`:
  Labels `Vehicle`, `Car :> Vehicle`, `<PD2> PartDef2`, and `<T> Truck :> Vehicle, Hauler`; the
  longest label's box is wide enough for it
- `GeneralViewLayoutStrategy_FormatNameLine_AllOptionalsNull_ReturnsNameOnly`:
  Only the name is returned when every optional piece is absent
- `GeneralViewLayoutStrategy_FormatNameLine_EachOptionalAlone_UsesTextualNotation`:
  Short name, supertypes, typing, and multiplicity each render on their own (`<c1> Car`,
  `Car :> A, B`, `car : Car`, `car [2]`); empty values behave like null
- `GeneralViewLayoutStrategy_FormatNameLine_AllPieces_CombinesInSpecOrder`:
  `<r> ram :> Base : Memory [1..*]`
- `GeneralViewLayoutStrategy_BuildLayout_Subclassification_ProducesEdge`:
  Line with open end marker at supertype
- `GeneralViewLayoutStrategy_BuildLayout_UnresolvedSupertypeAndAliasOfSameTarget_DrawsOneEdge`:
  `:> X, A` / `:> A, X` where `X` is unresolved and the alias `A` resolves to `P::X` draws exactly one
  specialization edge, in either reference order
- `GeneralViewLayoutStrategy_BuildLayout_QualifiedAndBareSupertypesSharingName_DrawsBothEdges`:
  `:> Y::X, X` (bare `X` resolved through an import to `P::X`) draws two specialization edges
- `GeneralViewLayoutStrategy_BuildLayout_UnresolvedSubsettingAndAliasOfSameFeature_DrawsOneEdge`:
  `part s :> slotX, Alias` where `slotX` is unresolved and `Alias` resolves to the feature draws exactly
  one subsetting edge
- `GeneralViewLayoutStrategy_TryGetResolvedTarget_IdentifiedEdges_MatchesExactTextOnly`:
  With edges identified only as `A`, asking for `X` is a known answer with no target and asking for `A`
  returns its target
- `GeneralViewLayoutStrategy_TryGetResolvedTarget_NoIdentifiedEdges_ReportsNotKnown`:
  No declaration, no edges, edges without `RawReference`, or edges of another kind report "not known" so the
  caller falls back to name resolution
- `GeneralViewLayoutStrategy_TryGetResolvedTarget_DuplicateAndQualifiedText_UsesExactKey`:
  A duplicated reference text answers the same target; a qualified text is a different key from the bare text
- `GeneralViewLayoutStrategy_BuildLayout_SeedStdlibNames_AreExcluded`:
  Seed-listed definitions excluded; empty canvas
- `GeneralViewLayoutStrategy_BuildLayout_UserPackageNamedLikeStdlibPackage_RendersUserMembersOnly`:
  A user `package Parts { part def Wheel; }` draws the `Parts` folder and `Wheel` and nothing from the
  real standard-library `Parts` package
- `GeneralViewLayoutStrategy_BuildLayout_UserPackageNamedLikeStdlibPackage_StdlibMembersStayExcluded`:
  `Wheel` in a user `Parts` package and `Hub` in another package are drawn; the standard-library
  `Part` is not
- `GeneralViewLayoutStrategy_BuildLayout_OccurrenceKindUsage_ShowsTypingLabelAndEdge`:
  Each occurrence-kind usage (part, item, occurrence, action, state, requirement, concern, use case,
  analysis, verification, case, calc) is a box labeled `u : Def` with a hollow-triangle-two-dots typing edge
  to `Def`
- `GeneralViewLayoutStrategy_BuildLayout_NestedRequirementUsage_ShowsTypingLabelAndEdge`:
  A requirement usage nested in a part def, under the Pilot policy, is a node labeled `r : R` with a
  typing edge to `R`
- `GeneralViewLayoutStrategy_BuildLayout_PackageConstraintUsage_ShowsTypingLabelAndEdge`:
  A typed constraint usage declared in a package is a node labeled `k : K` with a typing edge to `K`
- `GeneralViewLayoutStrategy_BuildLayout_NestedConstraintUsage_IsNotANode`:
  A constraint usage nested in a part def is not a node under the Pilot policy: no box and no typing edge
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionWithUsages_ProducesCompartments`:
  Attribute and port compartments under the row-only policy, with centered italic titles
- `GeneralViewLayoutStrategy_BuildLayout_EventOccurrenceUsage_IsNotACompartmentRow`:
  A part def owning an attribute and an `event occurrence` usage draws only the `attributes` compartment;
  the event occurrence is sequence data and is not listed as a row
- `GeneralViewLayoutStrategy_BuildLayout_Compartments_AreInCanonicalOrder`:
  A part def declaring an item, port, enum value, part, and attribute (in that order) plus
  documentation lists `doc`, `attributes`, `enums`, `parts`, `ports`, `items` in that order
- `GeneralViewLayoutStrategy_BuildLayout_CompositeMembership_ProducesFilledDiamondEdge`:
  A part def owning a part usage draws the usage as its own node, joined to the owner by a solid line
  with a filled diamond at the owner; the usage's rounded node is the line's source
- `GeneralViewLayoutStrategy_BuildLayout_PortFeature_ProducesFilledDiamondEdge`:
  A port that owns nested features is a node joined to its owner by a filled diamond
- `GeneralViewLayoutStrategy_BuildLayout_PlainPort_IsRowOfOwnerPortsCompartment`:
  A port without nested features is a row of the owner's `ports` compartment: only the owner is drawn,
  with no membership and no typing edge
- `GeneralViewLayoutStrategy_BuildLayout_PortWithNestedContent_IsNodeAndPlainPortStaysRow`:
  A port with nested features is a node listing them in its own compartments, while a plain sibling port
  stays a row of the owner
- `GeneralViewLayoutStrategy_BuildLayout_PortPolicyVariants_AllUsagesDrawsPlainPortAsNode_RowsOnlyDoesNot`:
  The all-usages policy draws a plain port as a node; the row-only policy draws no port as a node
- `GeneralViewLayoutStrategy_BuildLayout_ReferenceMembership_ProducesHollowDiamondEdgeAndDashedNode`:
  A `ref part` usage is a node with a dashed outline and the keyword `ref part`, joined to the owner by a
  solid line with a hollow diamond at the owner
- `GeneralViewLayoutStrategy_BuildLayout_AttributeFeature_DoesNotProduceDiamondEdge`:
  No diamond edge for an `attribute` feature
- `GeneralViewLayoutStrategy_BuildLayout_AttributeTyping_RowDrawsNoTypingEdge`:
  An attribute usage is a row (`mass : Mass`) and draws no typing edge however its type is declared
- `GeneralViewLayoutStrategy_BuildLayout_AllUsages_AttributeUsage_IsNodeWithTypingAndHollowDiamond`:
  Under the all-usages policy an attribute usage is a node typed by its attribute definition (two-dots
  triangle at the definition) and joined to its owner by a hollow diamond
- `GeneralViewLayoutStrategy_BuildLayout_EnumTypedAttribute_ProducesFeatureTypingEdge`:
  Under the all-usages policy an enumeration-typed attribute is a node with a typing edge to the enum def
- `GeneralViewLayoutStrategy_BuildLayout_UsageTyping_UnresolvedType_ProducesNoTypingEdge`:
  No typing edge when the usage's type does not resolve to a definition in the view
- `GeneralViewLayoutStrategy_BuildLayout_TwoAttributesSameType_ProducesTwoTypingEdgesWithoutException`:
  Two usages of the same type draw two typing edges, resolved by endpoints, without an exception
- `GeneralViewLayoutStrategy_BuildLayout_ConjugatedTyping_ShowsTildeAndTypesTheUsageByThePortDefinition`:
  A conjugated typing reads `~Type` in the name line and draws the typing edge to the port definition
- `GeneralViewLayoutStrategy_BuildLayout_ReferenceSubsetting_ProducesHollowTriangleFourDotsEdgeOnlyWhenResolved`:
  `part b ::> a` draws a solid line from `b` to `a` with the four-dots triangle at `a`; an unresolvable
  reference draws nothing
- `GeneralViewLayoutStrategy_BuildLayout_AdaptiveGap_DenseModelProducesNonOverlappingBoxes`:
  Dense model produces a layout with no overlapping definition boxes
- `GeneralViewLayoutStrategy_BuildLayout_HeatLayout_ConnectedModelKeepsBoxesSeparated`:
  Connected cross-referencing model keeps every definition box non-overlapping
- `GeneralViewLayoutStrategy_BuildLayout_HeatLayout_SparseModelProducesCompactCanvas`:
  Sparse canvas stays compact with no warnings (no over-padding)
- `GeneralViewLayoutStrategy_BuildLayout_ExposedName_UnionsAdditionalSubtree`:
  A resolved `Expose` edge scopes the diagram to the target's containment subtree, fewer boxes
  than the full workspace
- `GeneralViewLayoutStrategy_BuildLayout_RenderTargetNameOnly_NoExposeEdges_RendersFullWorkspace`:
  A `RenderTargetName` with no resolved `Expose` edges renders the full workspace unchanged
- `GeneralViewLayoutStrategy_BuildLayout_ExposedUsage_ResolvesThroughTypingToDefinitionSubtree`:
  A resolved `Expose` edge naming a feature usage resolves through the usage's `Typing` edge to
  include its type's containment subtree
- `ResolveExposedScope_BracketFilterEvaluatesSuccessfully_NarrowsToMatchedDefinitionsOnly`
  (`ExposeScopeResolverTests`):
  A bracket-filtered `expose` entry that parses and evaluates successfully narrows to only the
  matched descendant definitions instead of the whole containment subtree
- `ResolveExposedScope_MixedFilteredAndUnfilteredEntries_NarrowsIndependently`
  (`ExposeScopeResolverTests`):
  A bracket-filtered entry and an unfiltered entry on the same view narrow independently
- `DiagramRenderer_RenderWorkspace_BracketExposeMandatorySafetyView_FiltersToMandatoryPart`
  (`RenderIntegrationTests`):
  End-to-end rendering of a `expose <path>::**[<expr>];` view includes only the matched definition
  and emits no "could not be evaluated" warning for the successfully-evaluated filter
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionPresent_EmitsNotYetEvaluatedWarning`:
  Unsupported filter text emits the "could not be evaluated" warning while the unfiltered scope
  still renders
- `GeneralViewLayoutStrategy_BuildLayout_OversizeScope_PassesEngineReducedEffortWarningThrough`:
  A package whose connectors between nested folders exceed the engine's full-effort work limit
  (60 definitions in six folders, each specializing one in the next folder) keeps the engine's
  "work limit" reduced-effort hint in `Warnings`, exactly once, and still draws every box
- `GeneralViewLayoutStrategy_BuildLayout_SmallScope_HasNoEngineWarning`:
  A small cross-folder model draws with no warning, so the pass-through adds none
- `GeneralViewLayoutStrategy_BuildLayout_OversizeScopeWithOwnWarning_ReportsBothWarnings`:
  The oversize model laid out by a view whose filter cannot be evaluated reports the strategy's own
  "could not be evaluated" warning first and the engine's hint after it, neither replacing the other
- `GeneralViewLayoutStrategy_BuildLayout_FilterExpressionMatchesNothing_RendersEmpty`:
  A supported filter expression that matches no candidates narrows the diagram to an empty canvas
- `GeneralViewLayoutStrategy_BuildLayout_NullViewNode_RendersFullWorkspaceUnchanged`:
  A `null` `ViewNode` (`--auto`/default) renders every definition, unchanged (regression guard)
- `GeneralViewLayoutStrategy_BuildLayout_BareNameRedefinition_ProducesHollowTriangleCrossbarEdge`:
  A bare-name redefinition produces a solid hollow-triangle-crossbar edge from the redefining usage to
  the redefined usage declared by the supertype of its owner
- `GeneralViewLayoutStrategy_BuildLayout_TransitiveBareNameRedefinition_ProducesHollowTriangleCrossbarEdgeToDeclaringAncestorUsage`:
  A bare-name redefinition whose declaring ancestor is two supertype hops away produces a
  hollow-triangle-crossbar edge to the usage of that ancestor, not of the immediate supertype
- `GeneralViewLayoutStrategy_BuildLayout_QualifiedRedefinition_ProducesHollowTriangleCrossbarEdgeToRedefinedUsage`:
  A qualified `Owner::feature` redefinition produces a hollow-triangle-crossbar edge to the named usage
- `GeneralViewLayoutStrategy_BuildLayout_UnresolvableRedefinition_ProducesNoEdge`:
  An unresolvable redefinition produces no edge and does not throw
- `GeneralViewLayoutStrategy_BuildLayout_SelfReferentialRedefinition_ProducesNoEdge`:
  A genuinely self-referential redefinition (resolving back to its own owning definition via a
  self-referential supertype cycle) produces no edge and does not throw
- `GeneralViewLayoutStrategy_BuildLayout_Connect_DifferentOwningTypes_ProducesUnmarkedSolidEdge`:
  A `Connect` edge between two sibling usage nodes (each nested in the same enclosing definition) produces
  a solid line with no end marker between the two distinct owning boxes
- `GeneralViewLayoutStrategy_BuildLayout_Connect_SameOwningType_ProducesNoEdge`:
  A `Connect` edge whose endpoints resolve to the same owning box (two features nested in the same usage
  node — a self-loop) produces no edge, and the drop is reported as a `Connect`-kind warning in
  `LayoutTree.Warnings`
- `GeneralViewLayoutStrategy_BuildLayout_ConnectDominantShape_RealWorkspaceLoader_ProducesDistinctBoxes`:
  End-to-end regression guard: the real `WorkspaceLoader` + `BuildLayout` pipeline (no synthetic
  edges) renders a `Connect` edge between two distinct boxes for the dominant real-world shape,
  with no dropped-edge warning
- `GeneralViewLayoutStrategy_BuildLayout_Allocate_ProducesSolidChevronEdgeWithLabel`:
  An `Allocate` edge produces a solid open-chevron line with a `«allocate»` midpoint label
- `GeneralViewLayoutStrategy_BuildLayout_Dependency_ProducesDashedChevronEdge`:
  A `Dependency` edge with no declared name produces a dashed open-chevron line with a `«depend»`
  midpoint label
- `GeneralViewLayoutStrategy_BuildLayout_Dependency_Named_ShowsDeclaredNameInsteadOfKeyword`:
  Loaded from text, a named dependency (`dependency link from A to B;`) is labeled with its name, an
  unnamed one keeps `«depend»`, and two named dependencies between the same boxes each show their own
  name
- `GeneralViewLayoutStrategy_BuildLayout_Dependency_NamedAndUnnamedSameEnds_KeepOwnLabels`:
  A named and an unnamed dependency between the same two boxes each keep their own label, with the
  unnamed one declared first and with the named one declared first (the edges appear in declaration order)
- `GeneralViewLayoutStrategy_BuildLayout_Binding_ProducesSolidEdgeWithEqualsLabel`:
  A `Binding` edge produces a solid line with no end marker and an `=` midpoint label
- `GeneralViewLayoutStrategy_BuildLayout_Subsetting_CrossesSpecializationBoundary_ProducesSolidHollowTriangleUsageEdge`:
  `part frontMotors subsets motors` (declared on the supertype) produces a solid hollow-triangle edge from
  the subsetting usage node to the subsetted usage node
- `GeneralViewLayoutStrategy_BuildLayout_SameDefinitionSubsetting_ProducesUsageToUsageEdge`:
  A `subsets` reference to a sibling usage of the same definition draws a usage-to-usage edge
- `GeneralViewLayoutStrategy_BuildLayout_SelfReferentialSubsetting_ProducesNoEdge`:
  A self-referential subsetting reference produces no edge
- `GeneralViewLayoutStrategy_BuildLayout_AnnotatedDefinition_EmitsNoteBox`:
  A definition with a `Comment` annotation gets a `BoxShape.Note` box connected by
  a plain dashed line with no end marker
- `GeneralViewLayoutStrategy_BuildLayout_NestedPartDocAndComment_AttachToOwnNode`:
  A nested part's `doc` is a compartment of the part's own node (not of its owner) and a `comment` on it is
  a note joined to the part's own node by a dashed line without arrowhead; this covers usages rendered as
  their own boxes, while the documentation and comments of attributes and plain ports, which stay
  compartment rows, are not rendered under the default policy
- `GeneralViewLayoutStrategy_BuildLayout_Satisfy_ProducesSolidOpenArrowEdgeWithKeywordLabel`:
  `satisfy` draws a solid open-arrowhead line from the satisfying box to the requirement, labeled
  `«satisfy»`
- `GeneralViewLayoutStrategy_BuildLayout_Verify_NestedSourceMapsToNearestRenderedBox`:
  `verify` is labeled `«verify»`; a verifying declaration nested in a rendered box maps to the nearest
  rendered box on its path
- `GeneralViewLayoutStrategy_BuildLayout_SatisfyAndVerifyOfSamePair_DrawTwoEdgesAndRepeatsCoalesce`:
  a `satisfy` and a `verify` between the same pair of boxes draw two edges labeled `«satisfy»` and
  `«verify»`; a repeated `satisfy` of the pair draws one edge
- `GeneralViewLayoutStrategy_BuildLayout_Imports_ProduceDashedOpenArrowEdgesLabeledByKind`:
  Membership, namespace, and recursive imports draw dashed open-arrowhead lines from the importing folder
  to the imported folder or box, labeled `«import»`, `«import»*`, and `«import»**`
- `GeneralViewLayoutStrategy_BuildLayout_Imports_StdlibAndContainedNamespace_DrawNoEdge`:
  An import of the standard library and an import between a package and a package nested in it draw no
  edge and produce no warning
- `GeneralViewLayoutStrategy_BuildLayout_Imports_NamespaceAndRecursiveOfSamePackage_DrawTwoEdgesWithDistinctLabels`:
  `import A::*;` and `import A::**;` in one package draw two edges between the same folders, labeled
  `«import»*` and `«import»**`
- `GeneralViewLayoutStrategy_BuildLayout_Imports_MembershipAndNamespaceOfSameTarget_DrawTwoEdgesWithDistinctLabels`:
  `import A::X;` and `import A::X::*;` resolve to the same imported box and draw two edges labeled
  `«import»` and `«import»*`
- `GeneralViewLayoutStrategy_BuildLayout_Imports_ExactDuplicate_DrawsOneEdge`:
  Two identical imports draw one edge

  `expose Pkg;` draws exactly one folder-shaped package node labeled `Pkg` with no children
- `GeneralViewLayoutStrategy_BuildLayout_BarePackageExposeWithDepthLimitOne_HasNoTruncationIndicator`:
  with a depth limit of 1 the empty package node still has no children (no `…` truncation indicator)
- `GeneralViewLayoutStrategy_BuildLayout_ViewConditionOnExposedPackage_KeepsOrRemovesPackageNode`:
  `filter @SysML::Package;` keeps the exposed package node; `filter @SysML::PartDefinition;` removes it
- `GeneralViewLayoutStrategy_BuildLayout_NamespaceExpose_DrawsMembersWithoutPackageNode`:
  `expose Pkg::*;` draws the member boxes and no package node
- `GeneralViewLayoutStrategy_BuildLayout_RecursivePackageExpose_DrawsPackageNodeWithMembers`:
  `expose Pkg::**;` draws the `Pkg` package node containing the member boxes
- `GeneralViewLayoutStrategy_BuildLayout_UnannotatedDefinition_EmitsNoNoteBox`:
  A definition with no annotations gets no note box (regression guard)
- `GeneralViewLayoutStrategy_BuildLayout_MultipleAnnotations_ProduceOneNoteBox`:
  A definition with multiple `Comment` annotations gets exactly one note box, with
  every comment's text concatenated into it
- `GeneralViewLayoutStrategy_BuildLayout_SameNamedDefsInSiblingPackages_WiresEachToItsOwn`:
  Three sibling packages each defining and using a same-named `Checker` part def render three
  `Checker` boxes, and each package folder holds exactly one filled-diamond membership edge between
  its own two boxes (no cross-package wiring to the first same-named definition)
- `GeneralViewLayoutStrategy_BuildLayout_SameSimpleNameInUnrelatedPackage_DrawsNoEdge`:
  A type reference that does not resolve by SysML v2 name resolution (a same-named definition
  exists only in an unrelated package, no import) draws no typing edge and no layout warning
- `GeneralViewLayoutStrategy_BuildLayout_UnrenderedQualifiedEndpoint_IsNotMappedBySimpleName`:
  A dependency endpoint outside the exposed scope is not re-mapped to a same-named rendered box
- `GeneralViewLayoutStrategy_BuildLayout_UnresolvedSubsettingAndRedefinition_DrawNoEdge`:
  Subsetting and redefinition references that do not resolve draw no edge and no warning, while the
  same references in a specialization of the declaring type draw exactly one edge of each kind
- `GeneralViewLayoutStrategy_BuildLayout_FileRootImportSubsettingAndRedefinition_DrawsEdges`:
  A subsetting and a redefinition reachable only through a file-root import (invisible to the
  layout's own name resolver) still draw their edges, because the semantic edge is used first
  (regression guard for removing the simple-name fallback)
- `GeneralViewLayoutStrategy_BuildLayout_PrivateMemberReference_DrawsNoEdge`:
  A reference to a private member of another package has no semantic edge and draws no edge and no
  layout warning, while a public reference is wired through its semantic edge
- `GeneralViewLayoutStrategy_BuildLayout_FilteredOutImportCandidate_DrawsNoEdge`:
  A candidate a filtered import rejects draws no edge; the accepted one does
- `GeneralViewLayoutStrategy_BuildLayout_ImportOfSiblingPackage_DoesNotResolveForThisPackage`:
  A name reachable only through a sibling package's import draws an edge for the package that
  imported it and none for the package that did not
- `GeneralViewLayoutStrategy_BuildLayout_DocumentationByDefault_RendersWrappedDocCompartment`:
  By default the definition's box carries a `doc` compartment whose rows preserve every word, wrap
  onto several rows, and each fit within the box width; the comment note is drawn and the doc
  text is in no note
- `GeneralViewLayoutStrategy_BuildLayout_FilterNotDocumentation_HidesDocButKeepsComment`:
  `filter not (@SysML::Documentation);` hides the `doc` compartment but keeps the comment note
- `GeneralViewLayoutStrategy_BuildLayout_PositiveFilterExcludingDocumentation_HidesDoc`:
  `filter @SysML::PartDefinition;` keeps the part definition but hides its doc and comment
- `GeneralViewLayoutStrategy_BuildLayout_InheritedViewCondition_HidesDoc`:
  a view typed by a view definition owning `filter not (@SysML::Documentation);` hides the doc
- `GeneralViewLayoutStrategy_BuildLayout_ViewTypedByStandardViewDefinition_AppliesOwnConditions`:
  a view typed by the library's `StandardViewDefinitions::GeneralView` applies its own conditions
- `GeneralViewLayoutStrategy_BuildLayout_ExposeFilterNotDocumentation_HidesDoc`:
  `expose P::**[not (@SysML::Documentation)];` hides the doc but keeps the definition and comment
- `GeneralViewLayoutStrategy_BuildLayout_MembershipExpose_ExposesDocOnlyWhenRecursive`:
  a bare `expose P::Battery;` draws no doc compartment; `expose P::**;` does
- `GeneralViewLayoutStrategy_WrapText_LongWord_IsHardBroken`:
  Word wrapping keeps every row within the limit, hard-breaking a word longer than the limit
- `GeneralViewLayoutStrategy_BuildLayout_RequirementSubject_UsesGuillemetTitle`:
  A `"subject"`-keyword feature compartment is titled `«subject»`, not the generic pluralized form
- `GeneralViewLayoutStrategy_BuildLayout_ConstraintFeatures_ShowExpressionText`:
  `"require constraint"`/`"assume constraint"` features render their raw `ExpressionText` instead
  of a `name : Type [multiplicity]` row, under `«require constraint»`/`«assume constraint»`
  compartment titles
- `GeneralViewLayoutStrategy_BuildLayout_EnumDefLiteralValues_ProducesEnumsCompartment`:
  `"enum value"`-keyword features are grouped under an `"enums"` compartment title
- `GeneralViewLayoutStrategy_BuildLayout_QualifiedPartUsageFilter_RendersOnlyPartUsages`:
  A `filter @SysML::PartUsage;` expression renders only usage-level candidates whose own keyword
  maps to `PartUsage`, reproducing the OMG `42.Views/ViewsExample.sysml` regression pattern against
  a mixed part/requirement/other-usage workspace
- `GeneralViewLayoutStrategy_BuildLayout_BarePartUsageFilter_RendersOnlyPartUsages`:
  The bare `filter @PartUsage;` spelling matches identically to the qualified form
- `GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersUsageLevelCandidatesToo`:
  With no filter present, flat/top-level usage-level candidates render as boxes by default; this
  synthetic workspace has no nesting, so `RemoveRowUsages` has nothing to exclude here
- `GeneralViewLayoutStrategy_BuildLayout_NoFilter_ExcludesUsageNestedInsideRenderedDefinition`:
  a usage nested directly inside an independently-rendered definition that the policy shows as a row is
  excluded from standalone box rendering in the default (unfiltered) case
- `GeneralViewLayoutStrategy_BuildLayout_MetaclassFilter_KeepsNestedUsageWhenParentExcluded`:
  a nested usage whose immediate parent is excluded from the final rendered set by an active
  metaclass filter still renders as its own standalone box, proving the row-removal step runs
  after — not before — standalone filter narrowing
- `GeneralViewLayoutStrategy_BuildLayout_DroneGalleryModel_RendersExactly21BoxesMatchingCheckedInSvg`:
  the real gallery corpus model `docs/gallery/models/01-drone-general.sysml`'s
  `DroneGeneralView` renders exactly 21 square-cornered definition boxes — the automated regression
  guard for the box-count defect
- `GeneralViewLayoutStrategy_BuildLayout_NoFilter_RendersDeeplyNestedGrandchildUsageWhenIntermediateParentExcluded`:
  a 3-level nested workspace (`Root::A` definition, `Root::A::b` and `Root::A::b::c`
  usages) under the row-only policy renders exactly 2 boxes — `A` and `c` — with `b` correctly shown as a
  row but `c` never silently dropped, proving the depth-ordered cascading pass treats `b` as
  absent for `c`'s own test once `b` is itself excluded
- `GeneralViewLayoutStrategy_BuildLayout_OmgSafetyFeatureViewsFixture_ScopesToExposedVehicleSubtree`:
  The OMG Safety feature-views fixture's exposed-vehicle-subtree scope now renders a non-empty
  scoped diagram containing the vehicle's part usages (`seatBelt`/`bumper`), while still excluding
  `Safety`/`Security`
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionOwningNestedDefinitions_RendersOneContainerBoxUnscoped`:
  A definition owning nested definitions (`OperatorConsole` owning `DisplayPanel`/`CommsHandset`,
  all inside package `Sys`, unscoped) renders exactly one `OperatorConsole` box, with
  `DisplayPanel`/`CommsHandset` nested as its own children; the `Sys` folder still exists and its
  own direct children are just the single `OperatorConsole` box — the Defect A regression guard
- `GeneralViewLayoutStrategy_BuildLayout_DefinitionOwningNestedDefinitions_RendersOneContainerBoxScoped`:
  The same nested-definition-owning fixture, scoped by an `expose Sys::OperatorConsole::**;`
  view, still renders exactly one `OperatorConsole` box with its children correctly nested, and no
  `Sys` folder appears at all — combining the Defect A and Defect B regression guards
- `GeneralViewLayoutStrategy_BuildLayout_ExposedNamespaceChildren_BarePackageAncestor_NoFolderRendered`:
  A view exposing `Sys::*` (direct-children recursion) over a bare package `Sys` with plain sibling
  definitions renders no `BoxShape.Folder` box anywhere, with the exposed definitions promoted
  directly to the root — the Defect B regression guard, isolated from any nested-definition case
- `GeneralViewLayoutStrategy_BuildLayout_Unscoped_StillRendersFullPackageFolderStructure`:
  An explicit regression guard confirming that, with no `expose`/no `ViewNode`, an ordinary
  bare-package case still renders a `Folder`-shaped box with the expected package label and
  expected (non-definition-container) children — unscoped behavior is provably unchanged
- `GeneralViewLayoutStrategy_BuildLayout_ExposedDefinitionInsideBarePackage_NoAncestorFolderRendered`:
  Mirrors the real `BatterySubsystemView` gallery scenario directly: `part def Battery` inside
  bare package `QuadcopterDrone`, exposed via `expose Battery;` (exact match) — no folder-shaped
  box exists and the `Battery` box is present directly at the root level, the primary Defect B
  correctness guard
- `GeneralViewLayoutStrategy_BuildLayout_NestedPackage_RendersInsideOwnerFolder`:
  `package P { part def A; package Sub { part def B; } }` renders one root folder `P` holding the `A`
  box and a `Sub` folder (which holds `B`), and no `Sub` folder at the root
- `GeneralViewLayoutStrategy_BuildLayout_NestedPackage_ParentWithoutDirectItems_CreatesEnclosingFolder`:
  `Outer { Inner { Deep { X } } }` renders three nested folders around `X` and nothing else at the root,
  although `Outer` and `Inner` own no definitions
- `GeneralViewLayoutStrategy_BuildLayout_ScopedExpose_NestedExposedPackageInsideOwner`:
  `expose P::**;` draws `Sub` inside `P`; `expose P::Sub::**;` draws `Sub` at the root with no `P` folder
- `GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_SiblingPackages_DeclaredAtRootAndRoutedToNestedBoxes`:
  A specialization between definitions of two sibling top-level packages is exactly one line at the root
  scope, starting on the specializing box and ending on the supertype box
- `GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_NestedVersusOuter_DeclaredInOuterScope`:
  A nested-package definition specializing an owner-package definition yields one line among the owner
  folder's children (none at the root or in the nested folder), ending on both boxes
- `GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_SiblingNestedPackages_DeclaredInCommonParent`:
  A specialization between two sibling nested packages is declared in their common parent folder
- `GeneralViewLayoutStrategy_BuildLayout_CrossPackageEdge_DeepNestingAcrossTopLevelPackages_DrawsEdge`:
  A definition three packages deep specializing a definition of another top-level package is drawn at
  the root between the two boxes, beside an unpackaged definition
- `GeneralViewLayoutStrategy_BuildLayout_EdgeBetweenContainerAndOwnDescendant_DoesNotThrow`:
  A definition related to a definition it contains lays out without error with both boxes present
- `GeneralViewLayoutStrategy_BuildLayout_NestedPackages_DepthLimit_CountsPackageNesting`:
  Depth limit 1 collapses `P` to a bare `…` label; limit 2 keeps `A` and collapses `Sub` to `…`; limit 3
  and no limit show everything with no truncation label
- `GeneralViewLayoutStrategy_BuildLayout_NestedPackages_DepthLimit_EdgeToTruncatedPackageIsDropped`:
  A relationship into a nested package collapsed by the depth limit draws no line and does not throw
- `GeneralViewLayoutStrategy_BuildLayout_PackageFolderContents_DepthLimitOne_StillTruncatesFolder`:
  A depth limit of 1 over a package with two sibling definitions draws the folder with a bare `…`
  label child and neither definition's box
- `GeneralViewLayoutStrategy_BuildLayout_NestedDefinitionContainment_DepthLimitTruncatesInnerLevel`:
  A depth limit of 2 over `Outer` owning `Inner` keeps `Outer` as a leaf box (no children, no `Inner`
  box) whose last compartment is an untitled `…` row
- `GeneralViewLayoutStrategy_BuildLayout_TruncatedDefinitionWithCompartments_AppendsEllipsisRow`:
  A truncated definition that already has an `attributes` compartment keeps it and gains the `…` row
  as a separate trailing untitled compartment, and is taller than the same definition untruncated
- `GeneralViewLayoutStrategy_BuildLayout_TruncatedBoxBesideRoutedEdge_DecoratesOnlyTruncatedBox`:
  A scope with a routed specialization line beside a truncated and an untruncated definition: only the
  truncated definition carries the `…` row
- `GeneralViewLayoutStrategy_BuildLayout_TruncatedNestedPackages_DecorateOnlyMatchingBoxes`:
  With sibling and nested packages at two scopes, only the packages whose contents the depth limit hides
  carry the bare `…` label
- `GeneralViewLayoutStrategy_BuildLayout_NestedPackagesGalleryModel_RendersFoldersNestedAndCrossPackageEdges`:
  The gallery model `11-nested-packages-general.sysml` draws `Powertrain` and `Body` inside `Vehicle`, the
  `Hybrid` to `Engine` specialization and both compositions into `Car` in the `Vehicle` scope between the
  endpoint boxes
- `GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DeclarationOrder_DoesNotMoveBoxes`:
  The nested-package model with `Hybrid` declared last and again first in `Body` draws every box at the
  same position in both orders and no two definition boxes overlap, so the picture does not depend on
  declaration order (a residual cross-folder connector crossing is an engine routing outcome, not asserted)
- `GeneralViewLayoutStrategy_BuildLayout_NoExternalEdges_KeepsDeclarationOrder`:
  A package with unconnected definitions and a specialization pair and no relationship leaving it
  keeps its declaration order (`Zeta` before `Alpha` before `Beta`)
- `GeneralViewLayoutStrategy_BuildLayout_ExternalEdgeToTruncatedPartner_DoesNotThrowAndKeepsOrder`:
  A relationship into a nested package collapsed by the depth limit has no partner box to connect to;
  the layout completes, both definitions of the folder are drawn in declaration order, and the
  hidden partner's box is absent
- `GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DeepNesting_DoNotCross`:
  Two definitions of `A::B` leading into `Lo::In` and `Hi::In` are drawn with two connectors that do
  not cross, for both declaration orders
- `GeneralViewLayoutStrategy_BuildLayout_ExternalEdges_DefinitionToSiblingNestedPackages_DoNotCross`:
  Two definitions of package `P` specializing definitions of sibling nested packages `A` and `B` are
  drawn with two connectors that do not cross, for both declaration orders
- `GeneralViewLayoutStrategy_BuildLayout_ParallelEdgesInNestedPackage_AreNotMerged`:
  Two attribute usages of the same type, drawn as nodes under the all-usages policy, inside a nested
  package give two separate typing connectors, so parallel-edge merging stays off in nested folder scopes
- `GeneralViewLayoutStrategy_BuildLayout_ParallelEdgesInDefinitionContainer_AreNotMerged`:
  Two relationships between the same two definitions inside a definition container give two separate
  connectors, so parallel-edge merging stays off in definition-container scopes
- `GeneralView_BuildLayout_ExcludedMembers_RenderIdenticallyToWorkspaceWithout`:
  Members flagged `IsExcludedFromDiagrams` (bare parameters, `perform`/portion usages, a hidden nested
  definition, and a hidden member of a port) add no box, row or port box: the SVG equals that of the
  workspace without them
