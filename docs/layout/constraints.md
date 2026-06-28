# Constraints

## SysML Specification Constraints

The SysML v2 specification constrains *notation* (arrowhead shapes, line styles,
compartment structure) but does **not** mandate block positions or connector routing for
any view type, with one exception:

**Sequence View**: lifelines are vertical swimlanes arranged horizontally; time flows
strictly downward. This is a hard notation constraint. The Sequence View layout is
rule-based and bypasses the placement algorithm entirely.

## Axis Symmetry

All layout operations treat X and Y identically unless there is explicit semantic reason
not to. The only permitted axis bias is the soft hierarchical reading-direction preference.
Specifically:

- Congestion measurement covers both axes
- Gap compression is applied on both axes simultaneously
- Force fields are isotropic unless a directional bias parameter is explicitly set

## Reading Direction Convention

The following are strong conventions (not spec requirements) encoded as soft forces:

- Specialization and composition hierarchies read top-to-bottom
- Action flow and state machine execution reads top-to-bottom or left-to-right

---
