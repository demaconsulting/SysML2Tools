# Algorithm Overview

The algorithm has two layers: a **mode-specific placement phase** and a **shared
pipeline** that all modes feed into.

```mermaid
flowchart TD
    subgraph modes["Mode-Specific Placement"]
        M1["Free 2D\n(General View)"]
        M2["Directed Flow\n(Action / State)"]
        M3["Hard-coded\n(Sequence)"]
    end
    subgraph pipeline["Shared Pipeline"]
        S1["Step 1 · Connectivity Analysis"]
        S2["Step 2 · Monte Carlo Seeds"]
        S3["Step 3 · Coarse Force-Directed"]
        S4["Step 4 · Highway Assignment"]
        S5["Step 5 · Grid Quantisation"]
        S6["Step 6 · Oversized Placement"]
        S7["Step 7 · Route Edges (initial)"]
        S8["Step 8 · Gravity Compression"]
        S9["Step 9 · Re-route (final)"]
        S10["Step 10 · Grid Snap"]
        S11["Step 11 · Post-Processing"]
        S1 --> S2 --> S3 --> S4 --> S5 --> S6 --> S7 --> S8 --> S9 --> S10 --> S11
    end
    M1 --> S1
    M2 --> S1
    M3 --> S11
```

The three modes share all pipeline steps. They differ in the force parameters used in
Steps 2–3 and in whether back-edge arc routing is active. Hard-coded Sequence View skips
Steps 1–10 entirely and enters at Step 11.

## Three Layout Modes

```mermaid
flowchart LR
    subgraph Free2D["Free 2D · General View"]
        F1["Soft hierarchy bias\nκ_h = 0.15"]
        F2["Isotropic forces"]
        F3["2D clusters emerge"]
    end
    subgraph Directed["Directed Flow · Action / State"]
        D1["Strong flow bias\nκ_h = 1.0"]
        D2["Anisotropic forces"]
        D3["Back-edge arc routing"]
    end
    subgraph Hard["Hard-coded · Sequence"]
        H1["Rule-based positions"]
        H2["Bypasses Steps 1–10"]
    end
```

---
