---
name: lint-fix
description: Fixes all lint issues. Run this once before submitting a pull request.
user-invocable: true
---

# Lint Fix Agent

Fix all lint issues in the repository until `pwsh ./lint.ps1 -All` exits cleanly.
This is the **pre-PR lint sweep** - run it once before pull request
submission, not during normal development.

# Workflow (MANDATORY)

1. **Auto-fix pass**: Run `pwsh ./fix.ps1 -All` to silently apply all
   automatic fixes (dotnet format, lintmark, yamlfix).

2. **Fix loop** (maximum 5 iterations):

   a. Run `pwsh ./lint.ps1 -All` and capture the full output. (You may re-check individual fixes
      with `-Changed` or `-Check <names>` while iterating, but the pass that ends the loop must be `-All`.)

   b. If exit code is 0 - the repository is lint-clean. Proceed to the report.

   c. Parse the failures and fix each one using the guidance below.

   d. Repeat.

3. **Budget exhausted**: If still failing after 5 iterations, report the
   remaining issues and stop with Result: FAILED.

# Fix Guidance by Failure Type

- **TextLint spelling errors** (`dotnet textlint`, configured by `.textlint.yaml`):
  decide the cause first, then apply the matching fix. The `overrides` and
  `enabled` settings used below need TextLint 0.1.0-beta.4 or later.

  1. **Typo**: Fix it in the source. Never add a misspelled word to `words`.
  2. **Genuine project or technical term used across the repository**: Add it
     to the global `words` list in US English. Never add a British-English
     spelling.
  3. **Fixture or test data** (deliberate misspellings, encoded data): Add a
     scoped `overrides` entry with `filename` globs and `words`, `ignoreWords`,
     or `ignoreRegExpList`, plus a comment explaining why. Do not use global
     `words` or scattered inline `textlint:` comment directives for these.
  4. **Never ours** (vendored, generated, or build-output files): Use
     `ignorePaths`, and only for these.
  5. **Ours, but deliberately not checked**: Add an `overrides` entry with
     `enabled: false` and a comment stating the reason.
  6. `.textlint.yaml` is a protected configuration file. Explain every change to
     it in the report.

- **LintMark line length (`line-length`, `MD013`)**: Run `pwsh ./fix.ps1 -Check lintmark` (or `dotnet lintmark --fix`),
  which re-wraps over-length paragraphs automatically. Only lines LintMark cannot break remain: a single long URL, link,
  or code span, a heading, or a table row. Fix these by shortening the content, using reference-style links, or
  restructuring the table. **Pipe-tables that cannot be wrapped without breaking structure** are a special case -
  convert them to a bullet list if the data reads naturally that way, or rewrite as a
  [grid table](https://pandoc.org/MANUAL.html#tables) if a tabular layout is essential. Do not get stuck trying to
  squeeze a wide pipe-table into 120 characters.

- **LintMark other rules** (`dotnet lintmark`): Formatting rules are fixed by `pwsh ./fix.ps1 -Check lintmark`. The
  lint-only rules (heading increment, duplicate headings, missing alt text, broken link fragments, undefined or unused
  link references, emphasis or link spacing, table too wide) need a manual fix: apply the specific fix indicated in the
  output (`file:line:col: rule-id message`).

- **yamllint errors**: Fix indentation, trailing spaces, or missing document
  markers as indicated. Run `pwsh ./fix.ps1 -Check yamlfix` again if structural YAML
  issues appear - yamlfix may handle them.

- **reqstream / reviewmark / versionmark failures**: Fix the referenced
  requirements or review configuration per the standards in
  `.github/standards/reqstream-usage.md` and `.github/standards/reviewmark-usage.md`.
  For an orphaned requirement, add the missing `children` link from the parent it
  serves; if no parent clearly needs it, report it rather than inventing a link.

# Rules

- Fix **only** lint issues - do not refactor, restructure, or make functional changes
- For spelling: follow the TextLint policy above; never add a word to the global `words` list just to silence a failure
- Never modify auto-generated files (check file headers for "auto-generated" or "do not edit")
- Respect all protected configuration files listed in AGENTS.md
- Report **all** files modified

# Report Template

```markdown
# Lint Fix Report

**Result**: (SUCCEEDED|FAILED)
**Report**: `.agent-logs/lint-fix-{subject}-{unique-id}.md`

- **Iterations**: {Number of fix-loop iterations performed}
- **Files Modified**: {List of all files changed}
- **Issues Fixed**: {Brief categorized description of what was corrected}
- **`.textlint.yaml` Changes**: {Each change and why, or "None"}

## Remaining Issues (only when Result is FAILED)

{List of unfixed lint failures with file:line references and why they could
not be automatically resolved}
```
