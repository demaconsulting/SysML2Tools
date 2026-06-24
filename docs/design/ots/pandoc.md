## Pandoc

This document describes the integration and usage design for the `Pandoc` OTS software item.

### Purpose

Pandoc (`DemaConsulting.PandocTool`) is chosen to convert Markdown documents to HTML as part of the
documentation build pipeline. It provides reliable, repeatable Markdown-to-HTML conversion so that
each documentation collection can be compiled into a consistent rendered form.

### Features Used

- Markdown-to-HTML conversion
- Document definition driven input ordering and resource resolution

### Integration Pattern

Pandoc is consumed as a dotnet tool restored from the tool manifest. Each documentation section
(build notes, code quality, code review, design, verification, requirements, user guide) is
converted individually with `dotnet pandoc` before WeasyPrint renders it to PDF. Pandoc does not
provide dotnet self-validation; its output is validated by FileAssert assertions on the generated
HTML, so a successful FileAssert step is the integration evidence. Each invocation is a single
process call with no persistent state.
