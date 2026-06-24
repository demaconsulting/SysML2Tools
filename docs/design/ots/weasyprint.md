## WeasyPrint

This document describes the integration and usage design for the `WeasyPrint` OTS software item.

### Purpose

WeasyPrint (`DemaConsulting.WeasyPrintTool`) is chosen to convert HTML documents to PDF as part of
the documentation build pipeline. It provides reliable, repeatable HTML-to-PDF rendering so that
each documentation collection can be delivered as a final PDF artifact.

### Features Used

- HTML-to-PDF conversion
- PDF rendering of Pandoc-generated HTML documents

### Integration Pattern

WeasyPrint is consumed as a dotnet tool restored from the tool manifest. Each HTML document
produced by Pandoc is converted individually with `dotnet weasyprint` to produce the final PDF
artifact. WeasyPrint does not provide dotnet self-validation; its output is validated by FileAssert
assertions on the generated PDF, so a successful FileAssert step is the integration evidence. Each
invocation is a single process call with no persistent state.
