# DemaConsulting.SysML2Tools.Svg

## Architecture

The `DemaConsulting.SysML2Tools.Svg` package provides the SVG renderer, implementing the
`IRenderer` interface from the core library with zero external dependencies. This system is
a Phase 0 stub; detailed design will be populated in Phase 4+.

## External Interfaces

*To be defined in Phase 4+.*

## Dependencies

- **DemaConsulting.SysML2Tools** — provides `IRenderer` interface and semantic model.

## Risk Control Measures

N/A — not a safety-classified software item.

## Data Flow

*To be defined in Phase 4+.*

## Design Constraints

- Platform: multi-targets net8.0, net9.0, and net10.0 framework compatibility specifications
  on Windows, Linux, and macOS.
- Zero external NuGet dependencies beyond the core library.
