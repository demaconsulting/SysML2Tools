---
name: C# Language
description: Follow these standards when developing C# source code.
globs: ["**/*.cs"]
---

# Required Standards

Read these standards first before applying this standard:

- **`coding-principles.md`** - Universal coding principles and quality gates

# API Documentation and Literate Coding Example

```csharp
/// <summary>
///     Converts a raw sensor reading into a validated measurement ready for downstream consumers.
/// </summary>
/// <remarks>
///     Clamping is preferred over throwing on out-of-range values because sensor drift at
///     range boundaries is expected; clamping produces a usable result where rejection would
///     discard valid near-boundary readings. Stateless and thread-safe; the calibration
///     profile is read but never modified.
/// </remarks>
/// <param name="reading">Raw sensor value. Must be finite (NaN and infinities are rejected).</param>
/// <param name="calibration">Calibration profile providing offset and range. Must not be null.</param>
/// <returns>Corrected value clamped to [calibration.Minimum, calibration.Maximum].</returns>
/// <exception cref="ArgumentException">Thrown when <paramref name="reading"/> is NaN or infinite.</exception>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="calibration"/> is null.</exception>
public double ProcessReading(double reading, CalibrationProfile calibration)
{
    // Reject invalid inputs before any calculation - non-finite readings cannot be
    // corrected, and a null calibration profile provides no offset or range to apply
    if (!double.IsFinite(reading))
        throw new ArgumentException("Reading must be a finite number.", nameof(reading));
    ArgumentNullException.ThrowIfNull(calibration);

    // Apply the calibration offset to convert raw counts to physical units
    var corrected = reading + calibration.Offset;

    // Clamp to the operational range so consumers can rely on the documented contract
    return Math.Clamp(corrected, calibration.Minimum, calibration.Maximum);
}
```

Key qualities demonstrated above:

- **`<summary>`** is a brief one-liner explaining *what* the method does
- **`<remarks>`** sits directly after summary and carries the extended intent -
  *why* it exists, design decisions, thread-safety, and side-effect disclosures
- **`<param>` tags** state constraints (finite, non-null) so callers know what
  is valid without reading the body
- **`<returns>`** documents the boundary guarantee so consumers can rely on the
  contract
- **`<exception>` tags** name every thrown exception and the condition that
  triggers each one
- **Inline block comments** follow the Literate Coding principles from
  `coding-principles.md`, separating logical steps so reviewers can verify each
  step against design intent

# Code Formatting

- **Format entire solution**: `dotnet format`
- **Format specific project**: `dotnet format MyProject.csproj`
- **Format specific file**: `dotnet format --include MyFile.cs`

# Quality Checks

- [ ] Zero compiler warnings (`TreatWarningsAsErrors=true`)
- [ ] XmlDoc documentation complete on all members (public, internal, protected, private)
- [ ] `dotnet format` applied (run `pwsh ./fix.ps1`)
