# Constraints

## SysML Specification Constraints

The SysML v2 specification constrains *notation* (arrowhead shapes, line styles, compartment
structure) but does **not** mandate node positions or connector routing for any view type,
with one notable exception:

**Sequence View**: lifelines are vertical stems arranged horizontally and time flows strictly
downward. This is a hard notation constraint. The Sequence View layout is rule-based and does
not use the graph-drawing pipeline at all.

## Flow Direction

The layered pipeline computes every layout in a single canonical orientation — layers
progressing left-to-right — and a final transform maps that canonical result onto the
requested flow direction. Four directions are available: `RIGHT`, `DOWN`, `LEFT`, and `UP`.
Each view fixes the direction that best communicates its semantics:

- Structural views (General, Interconnection) flow left-to-right (`RIGHT`).
- Behavioral views (State Transition, Action Flow) flow top-to-bottom (`DOWN`), matching the
  conventional reading direction for execution order.

Because the direction is applied by a single transform stage over an otherwise
direction-agnostic layout, all four directions share identical placement and routing logic;
only the final coordinate mapping differs.

## Acyclicity Requirement

Layered layout is defined over a directed acyclic graph. Real models contain cycles (loops,
back-transitions), so the pipeline first makes the graph acyclic by reversing a minimal set of
back edges, lays the acyclic graph out, and then restores the original edge orientation when
emitting connectors. This keeps the flow direction consistent while still drawing every edge.

## Determinism

Every stage is deterministic and free of randomness. Component ordering, tie-breaking during
crossing reduction, and union-find representatives all resolve by original node index, so the
same input model always yields byte-identical geometry. This makes renders reproducible and
supports regression testing against stored baselines.

---
