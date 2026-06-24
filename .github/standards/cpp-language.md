---
name: C++ Language
description: Follow these standards when developing C++ source code.
globs: ["**/*.cpp", "**/*.hpp", "**/*.h"]
---

# Required Standards

Read these standards first before applying this standard:

- **`coding-principles.md`** - Universal coding principles and quality gates

# File Organization

C++ projects use two parallel top-level folders — `include/` (public API) and
`src/` (implementation) — both mirroring the same system/subsystem/unit hierarchy
(test layout is covered in `cpp-testing.md`):

```text
include/
└── {system_name}/
    └── {subsystem_name}/
        └── {unit_name}.hpp       # public API - installed with the package

src/
└── {system_name}/
    └── {subsystem_name}/
        ├── {unit_name}.cpp       # implementation
        └── {unit_name}_impl.hpp  # internal header - not part of the public API
```

Subsystems may nest to any depth: `{system_name}[/{subsystem_name}...]/{unit_name}.hpp/cpp`.

Protect every header with `#pragma once`.

# Naming and Style Conventions

- **Symbols**: `snake_case` for all identifiers - variables, functions, types, and
  namespaces - to align with STL naming
- **Bracing**: 4-space Allman style - opening brace on its own line
- **Data objects**: use `struct` for passive data; may include simple constructors
  or helper methods but must not encapsulate invariants (use `class` for those)

# API Documentation and Literate Coding Example

Use `///` C++ Doxygen line comments.

```cpp
/// @brief Converts a raw sensor reading into a validated measurement.
///
/// Clamping is preferred over throwing on out-of-range values because
/// sensor drift at range boundaries is expected; clamping produces a usable
/// result where rejection would discard valid near-boundary readings.
/// Stateless and thread-safe; the calibration profile is read but never modified.
///
/// @param reading      Raw sensor value. Must be finite (NaN and infinities are rejected).
/// @param calibration  Calibration profile providing offset and range.
/// @returns Corrected value clamped to [calibration.minimum, calibration.maximum].
/// @throws std::invalid_argument When reading is NaN or infinite.
double process_reading(double reading, const calibration_profile& calibration)
{
    // Reject invalid inputs before any calculation - non-finite readings cannot be
    // corrected, and the calibration profile provides no offset or range to apply
    if (!std::isfinite(reading))
        throw std::invalid_argument("reading must be a finite number");

    // Apply the calibration offset to convert raw counts to physical units
    double corrected = reading + calibration.offset;

    // Clamp to the operational range so consumers can rely on the documented contract
    return std::clamp(corrected, calibration.minimum, calibration.maximum);
}
```

Key qualities demonstrated above:

- **`@brief`** is a concise one-liner explaining *what* the function does
- **Extended description** carries the extended intent - *why* it exists, design decisions,
  thread-safety, and side-effect disclosures
- **`@param` tags** state constraints so callers know what is valid without reading the body
- **`@returns`** documents the boundary guarantee so consumers can rely on the contract
- **`@throws`** names every thrown exception and the condition that triggers it
- **Inline block comments** follow the Literate Coding principles from
  `coding-principles.md`, separating logical steps so reviewers can verify each
  step against design intent

# Code Formatting

Apply clang-format using the repository `.clang-format` configuration:

- **Format file**: `clang-format -i my_file.cpp`
- Run `pwsh ./fix.ps1` to apply across the project

# Quality Checks

- [ ] Zero compiler warnings (`-Wall -Wextra -Werror`)
- [ ] Doxygen documentation complete on all symbols
- [ ] clang-format applied (run `pwsh ./fix.ps1`)
- [ ] All headers protected with `#pragma once`
- [ ] No raw owning pointers - use `std::unique_ptr` or `std::shared_ptr`
