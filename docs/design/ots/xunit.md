## xUnit

This document describes the integration and usage design for the `xUnit` OTS software item.

### Purpose

xUnit (`xunit.v3` together with `xunit.runner.visualstudio`) is chosen as the unit-testing framework
for the project. It provides test discovery and execution and emits TRX result files that serve as
the traceability evidence consumed by ReqStream.

### Features Used

- `[Fact]` and `[Theory]` test discovery and execution
- Visual Studio test runner integration for `dotnet test`
- TRX result file output

### Integration Pattern

xUnit is consumed as a NuGet package reference in the test projects. Tests run via `dotnet test`,
which discovers and executes the `[Fact]` and `[Theory]` tests and writes TRX result files that
ReqStream consumes for requirements traceability. xUnit does not provide a self-validation mode; the
project's own passing test suite is the integration evidence. No additional initialization or
disposal is required beyond the standard test runner lifecycle.
