# TestCases

This folder contains minimal SysML v2 model files that reproduce specific
layout or rendering quality issues. Each file is a targeted regression case:
it was either derived from a reported problematic diagram, or crafted to
exercise a known-difficult routing or layout pattern.

## Purpose vs. gallery

The `docs/gallery/` models are curated showcase examples — they should look
beautiful. The models here are adversarial and minimal — they exist solely to
ensure a specific quality criterion passes and continues to pass as the
implementation evolves.

## Adding a new test case

When a user reports a problematic diagram, or when a new edge case is
discovered during development:

1. Reduce the model to the smallest `.sysml` that still reproduces the issue.
2. Add it here with a descriptive name that identifies the pattern
   (e.g., `dense-fan-out-membership.sysml`).
3. Document the acceptance criterion in the table below.
4. Add or update a test in `DemaConsulting.SysML2Tools.Tests` that renders
   the model and asserts the criterion is met.

The test corpus grows monotonically — cases are never removed, only fixed.

## Test cases

| File | Pattern exercised | Acceptance criterion |
| --- | --- | --- |
| `dense-fan-out-membership.sysml` | Fan-out of membership edges | No crossings; spread ≥ `EdgeClearance` |
| `sparse-two-node.sysml` | Two boxes with one specialization edge | Canvas within 20% of minimum; no over-padding |
