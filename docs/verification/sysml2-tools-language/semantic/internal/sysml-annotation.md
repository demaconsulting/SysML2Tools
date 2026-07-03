#### SysmlAnnotation Verification

##### Verification Approach

`SysmlAnnotation` and `SysmlAnnotationKind` are pure data types verified indirectly through
`WorkspaceLoaderTests`. Tests construct SysML models containing `comment` and `doc` annotating
elements, call `WorkspaceLoader.LoadAsync`, and assert that the resulting node's
`SysmlNode.Annotations` list contains `SysmlAnnotation` instances with the expected `Kind` and
`Text` values. `AstSerializerTests` additionally verifies that `Annotations` round-trips through
`AstSerializer`/`AstDeserializer`.

##### Test Environment

Tests run via `dotnet test` against all three target frameworks: net8.0, net9.0, and net10.0.
Temporary `.sysml` files are created in `Path.GetTempPath()` and deleted after each test. No
external services or additional configuration are required beyond a standard .NET SDK installation.

##### Acceptance Criteria

- An element with a single `comment` member and no `doc` captures exactly one `Comment`-kind
  annotation.
- An element with a single `doc` member and no `comment` captures exactly one
  `Documentation`-kind annotation.
- An element with both a `comment` and a `doc` member captures both, in source order.
- An element with no `comment`/`doc` members has an empty (never null) `Annotations` list.
- Multi-line comment/documentation free text is preserved verbatim, including interior newlines
  and leading `*` bullet characters, with only the delimiters removed.
- Loading a real OMG fixture file (`DocumentationExample.sysml`) captures the expected
  package-level and part-def-level `doc` text on the corresponding nodes.
- `Annotations` round-trips through `AstSerializer.Serialize`/`AstDeserializer.Deserialize`.

##### Test Scenarios

| Scenario | Verified By |
| --- | --- |
| Comment-only capture | `WorkspaceLoader_LoadAsync_CommentOnly_CapturesCommentAnnotation` |
| Documentation-only capture | `WorkspaceLoader_LoadAsync_DocumentationOnly_CapturesDocumentationAnnotation` |
| Comment and doc together, in order | `WorkspaceLoader_LoadAsync_CommentAndDocumentation_CapturesBothInSourceOrder` |
| No annotations — empty, not null | `WorkspaceLoader_LoadAsync_NoAnnotations_AnnotationsIsEmptyNotNull` |
| Multi-line text preserved verbatim | `WorkspaceLoader_LoadAsync_MultiLineAnnotation_PreservesTextVerbatim` |
| Real OMG fixture end-to-end | `WorkspaceLoader_LoadAsync_DocumentationExampleFixture_CapturesExpectedDocText` |
| Serialization round-trip | `AstSerializerTests.Serialize_Annotations_Preserved` |
| Serialization round-trip, empty | `AstSerializerTests.Serialize_NoAnnotations_RoundTripsEmptyNotNull` |
