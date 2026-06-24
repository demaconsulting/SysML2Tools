# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items
used by the Template DotNet Tool.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool

Internal OTS tool design is out of scope; only integration and usage evidence is documented.

## OTS Items

The following OTS items have verification evidence in this section:

- BuildMark (_buildmark.md_) — build-notes documentation tool
- FileAssert (_fileassert.md_) — document assertion tool
- Pandoc (_pandoc.md_) — Markdown-to-HTML conversion tool
- ReqStream (_reqstream.md_) — requirements traceability tool
- ReviewMark (_reviewmark.md_) — file review enforcement tool
- SarifMark (_sarifmark.md_) — SARIF report conversion tool
- SonarMark (_sonarmark.md_) — SonarCloud quality report tool
- VersionMark (_versionmark.md_) — tool-version documentation tool
- WeasyPrint (_weasyprint.md_) — HTML-to-PDF conversion tool
- xUnit (_xunit.md_) — unit-testing framework
