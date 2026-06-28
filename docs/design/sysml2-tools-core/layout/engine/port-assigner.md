#### PortAssigner

##### Purpose

`PortAssigner` decides where the connection ports of a single box sit on that box's outline. For
each port it selects the box side facing the port's connection target and computes the absolute
centre point on that side. When several ports land on the same side it spreads them across evenly
spaced slots. This gives connectors clean, well-separated attachment points on each box.

##### Data Model

`PortAssigner` is a static class with no instance state. Input is a list of `PortRequest` records,
each carrying the owning box `Rect` and a `Toward` `Point2D` (the point the port's connection heads
toward, typically the centre of the connected box). The result is a list of `PortPlacement`
records, one per request in input order, each carrying the absolute `CentreX`, `CentreY`, and the
chosen `PortSide`.

##### Key Methods

###### `Assign(requests)`

Computes the placements. The algorithm is:

1. **Side selection.** For each request the side is chosen from the direction of `Toward` relative
   to the box centre: the axis (horizontal or vertical) of greater magnitude decides whether the
   port goes on a left/right or top/bottom side, and the sign decides which of the two. This places
   each port on the side whose outward normal best points at its target.
2. **Grouping.** Port indices are grouped by their chosen side.
3. **Slot distribution.** Within each side, the ports are ordered by their target's coordinate
   along that edge (X for top/bottom, Y for left/right) so that connectors cross as little as
   possible, then placed at evenly spaced fractional slots — slot `s` of `count` ports sits at
   fraction `(s + 1) / (count + 1)` along the edge. This guarantees distinct, evenly spaced
   positions for any number of ports sharing a side.
4. **Coordinate computation.** Each slot fraction is mapped to an absolute point on the chosen side
   of the box rectangle.

##### Error Handling

A null `requests` argument throws `ArgumentNullException`. An empty request list returns an empty
result. No other input causes a throw; any direction yields a valid side, and the slot computation
is well-defined for any positive port count.

##### Dependencies

- `Rect` and `Point2D` (Layout subsystem) — the geometric input value types.
- `PortSide` (Layout subsystem) — the side enumeration returned in each placement.

##### Callers

View layout strategies that draw boxes with ports, which use the returned side and centre to anchor
each connector to its box.
