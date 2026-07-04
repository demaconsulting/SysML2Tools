# DemaConsulting.SysML2Tools

## Architecture

The `DemaConsulting.SysML2Tools` core library provides the Layout and Rendering subsystems
for SysML v2 diagram generation. It depends on `DemaConsulting.SysML2Tools.Language` for
parsing and semantic analysis, and on `DemaConsulting.SysML2Tools.Stdlib` for the pre-compiled
standard library.

The core library provides two subsystems: **Layout** and **Rendering**. The Layout subsystem
maps the SysML semantic model onto the `LayoutTree` intermediate representation — nine immutable
node record types covering all SysML diagram elements — which is provided off-the-shelf by the
`DemaConsulting.Rendering` package, and delegates geometric placement and routing to the
off-the-shelf `DemaConsulting.Rendering.Layout` layered algorithm. The Rendering subsystem
consumes the off-the-shelf rendering contracts (`IRenderer`, `Theme`, `RenderOptions`,
`RenderOutput`) from the `DemaConsulting.Rendering.Abstractions` package and retains the
SysML-coupled `ILayoutStrategy` and `DiagramRenderer` that drive the pipeline.

```mermaid
flowchart TD
    subgraph External
        Language["DemaConsulting.SysML2Tools.Language"]
        Stdlib["DemaConsulting.SysML2Tools.Stdlib"]
    end
    subgraph Layout
        LayoutTree
        LayoutNode
    end
    subgraph Rendering
        DiagramRenderer
        ILayoutStrategy
        IRenderer
        Theme
        RenderOptions
    end
    Language --> DiagramRenderer
    Stdlib --> DiagramRenderer
    DiagramRenderer --> ILayoutStrategy
    DiagramRenderer --> IRenderer
    ILayoutStrategy --> LayoutTree
    IRenderer --> LayoutTree
```

## External Interfaces

**IRenderer**: Low-level renderer interface.

- *Type*: Interface.
- *Role*: Consumer.
- *Contract*: `string MediaType { get; }`, `string DefaultExtension { get; }`,
  `void Render(LayoutTree layout, RenderOptions options, Stream output)`.
  Implementations must be pure and stateless.

**ILayoutStrategy**: Layout computation interface.

- *Type*: Interface.
- *Role*: Provider.
- *Contract*: `LayoutTree BuildLayout(ViewContext context, RenderOptions options)`.

**LayoutTree**: Intermediate representation for one rendered diagram view.

- *Type*: Sealed record.
- *Role*: Data container.
- *Contract*: `double Width`, `double Height`, `IReadOnlyList<LayoutNode> Nodes`.
  All coordinates are absolute; origin is top-left.

**Theme**: Visual rendering configuration.

- *Type*: Sealed record.
- *Role*: Configuration.
- *Contract*: `DepthFillColors`, `StrokeColor`, `StrokeWidth`, `LineCornerRadius`,
  `FontSizeTitle`, `FontSizeBody`, `LabelPadding`, `Font`. Three built-in instances are
  provided by `Themes.Light`, `Themes.Dark`, and `Themes.Print`.

## Dependencies

- **DemaConsulting.SysML2Tools.Language** — provides `WorkspaceLoader`, `SysmlWorkspace`,
  `SysmlNode` and all subtypes used by `DiagramRenderer` for pattern-matching view declarations.
- **DemaConsulting.SysML2Tools.Stdlib** — provides `StdlibProvider.GetSymbolTable()` used
  by `DiagramRenderer` to seed the semantic workspace with the pre-compiled standard library.

## Packaging

The `DemaConsulting.SysML2Tools.Core` NuGet package is built with `GenerateDocumentationFile`
and `DemaConsulting.ApiMark.MSBuild` (`ApiMarkPackDocs=true`), so an `api/` folder of
ApiMark-generated API reference documentation for this library's own public types is bundled
into the package at pack time. `Language` and `Stdlib` are independent, separately packable
NuGet packages that bundle their own API reference documentation the same way; Core references
them via normal `<ProjectReference>`s that `dotnet pack` resolves to ordinary NuGet
`<dependency>` entries (not embedded assemblies).

## Risk Control Measures

N/A — not a safety-classified software item.

## Data Flow

### Layout and Rendering Data Flow

1. `DiagramRenderer.RenderWorkspace` receives a `SysmlWorkspace`, an `IRenderer`, and
   `RenderOptions`. For each view declared in the workspace it constructs a `ViewContext`
   containing the view name and workspace reference.
2. `ILayoutStrategy.BuildLayout` is called with the `ViewContext` and `RenderOptions`. The
   Layout subsystem produces a fully resolved `LayoutTree` by delegating geometric placement and
   routing to the off-the-shelf `DemaConsulting.Rendering.Layout` layered algorithm (through the
   `LayeredPlacement` helper), placing every node at absolute coordinates and routing every
   connector as an orthogonal polyline. The Rendering subsystem then renders that tree.
3. `IRenderer.Render` is called with the `LayoutTree`, `RenderOptions`, and a fresh output
   `Stream`. The renderer reads each `LayoutNode` in the tree, translates it to output-format
   primitives, and writes bytes to the stream.
4. Fill colors for `LayoutBox` nodes are derived by the renderer as
   `Theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count]`.
5. Corner rounding for `LayoutLine` elbows is applied by the renderer using
   `Theme.LineCornerRadius`; `0.0` produces sharp corners.
6. Each rendered stream is wrapped in a `RenderOutput` with `SuggestedFileName` derived from
   the view name and `IRenderer.DefaultExtension`.

## Design Constraints

- Platform: multi-targets net8.0, net9.0, and net10.0 on Windows, Linux, and macOS.
- SysML v2 parsing, semantic analysis, and standard library are provided by the Language and
  Stdlib assemblies; the Core assembly contains only Layout and Rendering concerns.
