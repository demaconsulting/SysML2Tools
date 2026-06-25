# Stdlib

This folder contains the SysML v2 standard library files embedded as resources
in `DemaConsulting.SysML2Tools`.

## Source

Files are taken from the
[Systems-Modeling/SysML-v2-Release](https://github.com/Systems-Modeling/SysML-v2-Release)
repository, tag **2026-04**, from the `sysml.library/` subtree.

## License

These files are Copyright © 2019–2024 Model Driven Solutions, Inc. and other
contributors, distributed under the
[Eclipse Public License 2.0 (EPL-2.0)](https://www.eclipse.org/legal/epl-2.0/).

## Contents

The library contains two file types:

- **`.sysml`** — SysML v2 surface language; parsed by the SysML v2 grammar.
- **`.kerml`** — KerML (Kernel Modeling Language) files; embedded for future use
  when a KerML grammar is integrated (Phase 2).

## Folder Naming

The upstream repository uses spaces in folder names (e.g., `Domain Libraries`,
`Kernel Data Type Library`). These have been renamed to CamelCase
(e.g., `DomainLibraries`, `KernelDataTypeLibrary`) to avoid quoting and
URL-encoding issues. The file names and file contents are unchanged.
