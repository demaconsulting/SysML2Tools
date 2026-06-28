# Design Goals

The layout pipeline aims to produce diagrams with the following properties. These goals
are partly contradictory and collectively NP-Hard to optimise globally; the algorithm
approximates them through a principled pipeline.

- Blocks with high mutual connectivity are near each other — no long connector journeys
- Blocks that connect directly tend to be orthogonally aligned so connectors are straight
  (zero bends)
- Every gap between blocks is exactly wide enough for its connectors — not a pixel wasted,
  not a pixel short
- Connectors never cross, or if they must, crossing count is provably minimal
- Connectors have the minimum possible number of bends (ideally zero or one)
- Parallel connectors through a channel are equidistant — clean even spacing, not bunched
- Connectors from the same source merge into a shared trunk then branch — like a bus bar
- Connectors converging on the same target arrive at a shared stub
- The canvas is compact and balanced — no large empty regions, reasonable aspect ratio
- An implicit grid emerges — blocks align to invisible grid lines, connectors run along them
- The hierarchy is immediately visually obvious — levels readable at a glance
- Relationship semantics are spatially consistent — specialization flows one direction
- Labels never overlap each other, blocks, or connectors
- No connector detours unnecessarily — paths are the shortest available route
- Blocks within the same package cluster visually
- Small model changes produce small layout changes — layout stability

---
