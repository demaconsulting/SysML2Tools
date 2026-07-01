# Diagram Visual Quality Standard

This standard defines the quality criteria and multimodal inspection procedure
for all generated SysML diagrams — gallery images and targeted test renders.

> **When to load this standard**: Any time an agent generates, regenerates, or
> commits rendered diagram images. Load this file before invoking any multimodal
> quality-check call.

---

## How to Run a Visual QA Check

1. Load this file.
2. For each diagram, locate the **`.sysml` source file** and the **`.png` output
   file** — both paths are passed to the multimodal checker.
3. Submit both files to a multimodal model using the **Inspection Prompt** below.
   The checker reads the `.sysml` source to derive the expected inventory of
   blocks and connectors, then checks the image against both the inventory and
   the quality checklist.
4. The model must answer **PASS** or **FAIL** for each criterion, with a short
   reason on FAIL. Any single FAIL is a blocking defect — do not commit until
   resolved.
5. Do **not** accept a response that says "looks good" or "appears correct"
   without explicit per-criterion verdicts.

---

## Block and Connector Quality Checklist

For each **block** in the diagram:

- Label is fully visible and not clipped by the box boundary
- Does not overlap any other block
- Has a non-zero gap to every neighbouring block — wide enough to fit the
  largest connector decoration (diamond, arrowhead) between them without clipping
- Is positioned near the blocks it connects to — no block is isolated far
  from all its neighbours

For each **connection** in the diagram:

- Both endpoints are on box boundaries, not inside the box interior
- The connector is visually distinguishable at both endpoints — it does not
  blend invisibly into a shared border for its entire length
- Does not pass through the interior of any box it is not connected to
- Exits and enters perpendicular to the face it connects on (clean
  right-angle stub, not a diagonal or sliding-along-edge entry)
- Its final straight approach into the target box is at least as long as the
  arrowhead, so the arrowhead reads as a clean perpendicular entry
- Every connector end decoration sits on a clean straight approach at least as
  long as the decoration's along-line length, so the rounded corner does not
  intrude into the decoration; open line-end markers (open chevrons) are drawn
  open (two strokes), not as closed triangles
- If two connections share the same destination box and face, they merge into
  a single shared trunk well before reaching the box — parallel same-destination
  runs covering more than ~30% of total connector length are a defect
- On any given box face, each connection arrives at a visibly distinct port
  point — connections must be individually traceable, not clustered at one spot
- Takes a reasonably direct route — no gratuitous detour significantly longer
  than a direct orthogonal path between the two boxes

---

## Inspection Prompt

Use this text verbatim as the instruction to the multimodal model, providing
the `.sysml` source path and the `.png` image path as inputs:

```text
You are a SysML diagram quality inspector. You are given:
  - A SysML model source file (path: {SYSML_PATH})
  - A rendered PNG image of the diagram (path: {PNG_PATH})

Step 1 — Derive the expected inventory from the SysML source:
  - List every named block / part / state / action / node in the model.
  - List every connection / transition / flow / message between them.
  Record these as your ground truth.

Step 2 — Check the image against the inventory and quality criteria below.
Report every criterion individually in the exact format:

  [CRITERION ID] PASS  — one-line reason
  [CRITERION ID] FAIL  — one-line reason describing the exact problem

Do NOT say "looks good" or "appears correct" in aggregate.

--- INVENTORY CHECKS ---

[I1] Every block/node from the inventory is visible in the image with a
     readable label (not clipped or truncated).

[I2] The count of visible connector lines is within 1 of the expected
     connection count. (A connector that is completely invisible — blending
     into a box border — counts as missing.)

[I3] No connector is completely absent: for each expected connection, trace
     a path in the image from near the source box to near the target box.
     FAIL for any connection where no such path can be traced.

--- BLOCK QUALITY CHECKS ---

[B1] No two blocks overlap.

[B2] Every block has a non-zero visible gap to every neighbouring block —
     large enough to fit the largest connector decoration (diamond, arrowhead)
     between them without clipping.

[B3] No block is isolated far from all the blocks it connects to — grossly
     misplaced elements are a defect.

[B4] The canvas is reasonably compact: no empty rectangular region larger
     than roughly 3× the average block height exists with no blocks or
     connectors passing through it.

[B5] Blocks occupy at least 20% of total canvas area (catches everything
     shrunken into one corner with excessive surrounding whitespace).

--- CONNECTOR QUALITY CHECKS ---

[C1] Every connector endpoint is on a box boundary, not inside the box
     interior.

[C2] Every connector is visually distinguishable at both endpoints — it does
     not blend invisibly into a shared border for its entire length. Port
     markers (filled squares) must be visible at endpoints where they exist.

[C3] No connector passes through the interior of a box it is not connected
     to.

[C4] Every connector exits and enters perpendicular to the face it connects
     on (clean right-angle stub — not diagonal and not sliding along an edge).

[C5] Each connector is routed independently — no shared trunks are required
     or expected. Where two or more connectors run parallel in the same
     routing corridor, each segment must be visually separated from its
     neighbours by a clear gap (at least one stroke width). Connectors
     that are so close they appear to merge into a single line are a defect.

[C6] On any given box face, each connector arrives at a visibly distinct port
     point. Connectors must be individually traceable — a cluster of connectors
     at the same point on a face where you cannot tell them apart is a defect.

[C7] Every connector takes a reasonably direct route. A gratuitous detour
     significantly longer than a direct orthogonal path between the two boxes
     is a defect.

[C8] In a left-to-right layout, every connector must exit the source box
     through its RIGHT (east) face and enter the target box through its LEFT
     (west) face. A connector whose first segment travels upward or downward
     from the source box, or whose last segment arrives at the target box from
     above or below, is a defect — regardless of angle.

[C9] Every connector's final straight approach into its target node is at
     least as long as the arrowhead, so the arrowhead reads as a clean
     perpendicular entry (no stub shorter than the arrowhead).

[C10] Every connector end decoration sits on a clean straight approach at
      least as long as the decoration's own along-line length, so the rounded
      corner completes before the decoration zone and never intrudes into it.
      Open line-end markers (open chevrons) are drawn OPEN — two strokes
      meeting at the apex with no closing base edge — not as closed triangles.

--- SUMMARY ---

After evaluating all criteria, output:
  OVERALL: PASS (all N criteria passed)
  or
  OVERALL: FAIL (failed criteria: list IDs)
```

---

## Defect Severity

| Severity     | Description                                | Action                                         |
| ------------ | ------------------------------------------ | ---------------------------------------------- |
| **Blocking** | Any criterion FAIL                         | Do not commit; fix layout engine and re-render |
| **Warning**  | Minor label crowding not causing confusion | Document in commit message; fix in next pass   |

---

## Agent Checklist (Before Committing Rendered Images)

```text
[ ] Ran visual QA on all affected PNGs using this prompt (sysml + png paths)
[ ] Recorded per-criterion PASS/FAIL verdicts (not just "looks OK")
[ ] All I1–I3 inventory checks passed
[ ] All B1–B5 block checks passed
[ ] All C1–C10 connector checks passed
[ ] Any FAIL was fixed and re-checked before committing
```
