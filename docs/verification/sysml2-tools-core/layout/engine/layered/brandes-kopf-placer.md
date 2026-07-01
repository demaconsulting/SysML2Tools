##### BrandesKopfPlacer Verification

###### Verification Approach

`BrandesKopfPlacer` is verified through unit tests in `BrandesKopfPlacerTests` that run the stages up
to and including placement and assert on the produced coordinate arrays. One test confirms that every
augmented node receives a finite X and Y and that the per-column arrays are sized to the layer count;
another confirms that column left edges increase strictly in layer order. The stage is pure and
deterministic, so no mocking is required.

###### Test Environment

Tests run via `dotnet test` against net8.0, net9.0, and net10.0. No external services, files, or
configuration are required beyond a standard .NET SDK installation.

###### Acceptance Criteria

- All `BrandesKopfPlacerTests` pass with zero failures across all three target frameworks.
- Every augmented node receives a finite X and Y coordinate.
- The per-column arrays are sized to the number of layers.
- Column left edges strictly increase in layer order.

###### Test Scenarios

| Test | Assertion |
| --- | --- |
| `BrandesKopfPlacer_Apply_ChainGraph_AssignsCoordinateArrays` | Coordinate arrays are sized and finite |
| `BrandesKopfPlacer_Apply_ColumnsAreLeftToRightInLayerOrder` | Each column's left edge exceeds the previous column's |
