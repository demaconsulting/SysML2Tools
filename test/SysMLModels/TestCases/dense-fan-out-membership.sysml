// Regression: dense-fan-out-membership
//
// Reproduces the "fan-out" layout quality issue where one central definition
// owns many typed part/port features, producing a large number of membership
// edges all departing from the same box. With insufficient inter-row spacing
// these edges stack in a single narrow channel and become unreadable.
//
// Acceptance criterion:
//   - Renders with no crossing warnings
//   - Parallel edge segments in the same horizontal channel are spread at
//     least EdgeClearance (12px) apart — not stacked on top of each other
//   - The central definition (Hub) appears in a visually distinct lower row,
//     with its type definitions above it

package DenseFanOut {

    // Type definitions — these will appear as boxes in the upper rows
    part def TypeA;
    part def TypeB;
    part def TypeC;
    part def TypeD;

    port def PortX;
    port def PortY;

    // Central definition that owns all of the above as typed features.
    // This is the fan-out source: six membership edges all depart from Hub.
    part def Hub {
        part a : TypeA;
        part b : TypeB;
        part c : TypeC;
        part d : TypeD;
        port x : PortX;
        port y : PortY;
    }
}
