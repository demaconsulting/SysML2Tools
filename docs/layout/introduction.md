# Introduction

This document is the technical reference for the SysML2Tools layout pipeline. It
describes the algorithms used to place blocks, route connectors, and produce readable
diagrams from a SysML v2 model. It is intended for contributors extending or maintaining
the layout subsystem and for reviewers evaluating algorithmic correctness.

The layout pipeline converts a parsed SysML v2 workspace into a `LayoutTree` — an
intermediate representation of positioned boxes, routed lines, and labels — which the SVG
and PNG renderers then paint. The pipeline is responsible for every spatial decision: where
blocks sit, how connectors route between them, and how much space separates them.

## Relationship to Other Documents

| Document | Covers |
|---|---|
| User Guide | CLI usage, output formats, view selection |
| Design (`docs/design/`) | Per-component architecture and interfaces |
| Verification (`docs/verification/`) | Test scenarios and acceptance criteria |
| This document | Layout algorithms, engine catalog, per-view analysis |

---
