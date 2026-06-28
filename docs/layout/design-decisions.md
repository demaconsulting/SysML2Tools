# Resolved Design Decisions

The following questions were raised during algorithmic review; each is resolved here and
the resolution is reflected in the algorithm steps above.

1. **Coarse-to-fine coupling** — *Resolved: integer coarse grid.* The coarse cell is sized
   as an integer multiple of G (`cell = round(4 × avg_block / G) × G`) so coarse positions
   map to fine positions by exact rescale. Fine placement uses constraint-graph compaction
   (Step 5), not interpolation, eliminating the risk of a cluster that fits in coarse cells
   becoming infeasible on the fine grid.

2. **Highway threshold** — *Resolved: geometric necessity rule.* The absolute
   `highway_threshold = 3` is replaced by `highway ⇔ peak_lanes·G + 2·wire_margin >
   min_gap` (Step 4), where `peak_lanes` is the peak concurrent occupancy. This is
   scale-free, derived from `EdgeClearance`/`G`/`min_gap`, and stays discriminating at both
   density extremes. Reserved width is capped at `W_cap`, and hub bundles split across
   opposite faces.

3. **Compression step size** — *Resolved: no stepping.* Because corridors are hard
   constraints, the minimal feasible gap is closed-form (Step 8); the fixed-step (and the
   binary-search alternative) are unnecessary. A bounded (≤2) outer re-evaluation of
   corridor assignments replaces per-step iteration.

4. **Back-edge arc radius** — *Resolved: nesting-depth radius.* Concurrently-open
   back-edges are treated as nested intervals; a back-edge at nesting depth `k` arcs at
   `R_k = EdgeClearance × (1 + k)`, so deeper loops arc further out and never overlap.

5. **Cluster threshold** — *Resolved: community detection.* An all-pairs-above-threshold
   clique rule cannot cluster a hub-and-spoke (zero leaf-leaf affinity), so it is replaced
   by label-propagation/Louvain community detection on an affinity graph whose edges are
   admitted at a relative threshold (top tertile of positive weights).

6. **Interconnection View scope** — *Resolved: partial adoption.* Keep `ForceDirectedEngine`
   placement (do not impose layering on a port graph), but adopt the shared pipeline from
   Step 4 onward with port-aware anchoring, plus an explicit self/nested-connection rule.

7. **Highway stability after compression** — *Resolved: committed per round, ≤2
   re-evaluations.* Hard corridor membership means edge counts cannot drift within a round.
   Between rounds, at most one re-evaluation is allowed; the best feasible result is kept;
   a warning is emitted otherwise.

8. **Label highways** — *Resolved: yes.* Label bounding boxes are added to channel
   `required_width`/`required_height` during compression (Step 8), so label space is
   reserved by construction; Step 11 only *positions* labels within that reserved space.

---
