# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items
used by the SysML2 Tools.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool

Internal OTS tool design is out of scope; only integration and usage evidence is documented.

## OTS Items

The following OTS items have verification evidence in this section:

- ANTLR4 (*antlr4.md*) — ANTLR4 runtime (Antlr4.Runtime.Standard)
- BuildMark (*buildmark.md*) — build-notes documentation tool
- DemaConsulting.Rendering (*dema-rendering.md*) — layout intermediate representation, layered
  layout algorithm, and SVG/PNG renderers
- FileAssert (*fileassert.md*) — document assertion tool
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- xUnit (*xunit.md*) — unit-testing framework
