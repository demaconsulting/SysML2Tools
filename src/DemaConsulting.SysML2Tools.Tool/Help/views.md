---
title: Views and diagrams
summary: Which views render draws, how a view picks its diagram, and on-demand views.
aliases:
  - rendering
  - diagrams
  - diagram
tags:
  - reference
order: 6
see-also:
  - commands/render
  - sysml-models
examples:
  - name: all
    kind: command
    language: shell
    description: Draw every view that the model declares.
    content: '{{tool}} render vehicle.sysml --output out'
    expect: 'VehicleView.svg'
  - name: dynamic
    kind: command
    language: shell
    description: Draw Car as an interconnection view without declaring one.
    content: '{{tool}} render vehicle.sysml --view-type interconnection --view-target Vehicle::Car --output out'
    expect: "Synthesizing dynamic 'interconnection' view for 'Vehicle::Car'..."
  - name: filtered
    kind: command
    language: shell
    description: Draw the package, kept to elements with SoftwareInfo.
    content: '{{tool}} render vehicle.sysml --view-type general --view-target Vehicle --filter "@SoftwareInfo" --output out'
    expect: '$Vehicle.svg'
  - name: state
    kind: command
    language: shell
    description: Draw the Ignition state machine.
    content: '{{tool}} render vehicle.sysml --view-type state --view-target Vehicle::Ignition --output out'
    expect: '$Ignition.svg'
  - name: depth
    kind: command
    language: shell
    description: Draw only the outermost level of nesting.
    content: '{{tool}} render vehicle.sysml --walk-depth 1 --output out'
    expect: 'Rendered 1 view(s).'
---
A `view` in the model chooses what a diagram shows, and
[[commands/render]] draws one file per view, named after it, such as
`VehicleView.svg` ([[#all]]).

**Selection.** One view or many: `render` draws all of them, or only
the one that `--view <name>` names. No view: `render` writes nothing,
unless `--auto` asks it to draw the main part definition.

**What a view shows.** `expose P;` shows the element `P` itself (a
package as a folder without contents), `expose P::*;` the members of
`P`, and `expose P::**;` `P` and everything inside it. A `filter`
statement keeps the elements that match an expression, such as
`filter @Safety;`.

**Diagram kind.** Type the view by a standard view definition, after
`private import StandardViewDefinitions::*;`:

```sysml
view CarParts : GeneralView { expose Car; }
view CarInside : InterconnectionView { expose Car; }
```

`GeneralView`, `InterconnectionView`, `StateTransitionView`,
`ActionFlowView`, `SequenceView`, `GridView`, and `BrowserView` are
supported. An untyped view is a general view, unless its name contains
a kind, such as `Interconnection` or `State`.

**Views made on demand.** `--view-type <kind>` with
`--view-target <name>` draws any element without a `view` in the model
([[#dynamic]]). The kinds are `general`, `interconnection`, `state`,
`action`, `sequence`, `grid`, and `browser`; the file is named after the
element with a `$` in front, such as `$Car.svg`. `--filter <expr>` then
narrows the view ([[#filtered]]); an expression that cannot be
evaluated prints a warning and draws the view unfiltered. These options
cannot be combined with `--view` or `--auto`.

**Depth.** `--walk-depth <n>` limits the nesting that is drawn; hidden
content is marked with an ellipsis, never left out silently
([[#depth]]).

**Limit of interconnection views.** A connection between a port on
the boundary of the drawn element and a port of a part inside it is
not drawn. For a part def `Torch` with `port supply`, a part `lamp`,
and `connect supply to lamp.pwrIn;`, the view of `Torch` shows the
port `supply` and the part `lamp`, but no line between them, and
prints no warning. Connections between ports of inner parts, such as
`connect battery.outlet to lamp.pwrIn;`, are drawn.

**Formats.** SVG is the default; `--format png` writes PNG images, which
look the same on every platform. A PNG wider or taller than 8192 pixels
is scaled down with a warning; use SVG for a full-size diagram.
