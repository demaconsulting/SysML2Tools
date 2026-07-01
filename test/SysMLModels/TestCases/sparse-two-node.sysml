// Regression: sparse-two-node
//
// Minimal model with exactly two definitions and one specialization edge.
// Verifies that the adaptive spacing mechanism does not over-pad simple
// diagrams — the canvas should be close to the theoretical minimum.
//
// Acceptance criterion:
//   - Canvas width and height each within 20% of the theoretical minimum
//     (sum of box sizes plus default gap values)
//   - Specialization edge renders with a hollow-triangle arrowhead at Base
//   - No crossing warnings

package SparseSimple {
    part def Base;
    part def Derived :> Base;
}
