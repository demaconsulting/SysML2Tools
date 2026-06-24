---
name: lint-fix
description: Fixes all lint issues. Run this once before submitting a pull request.
user-invocable: true
---

# Lint Fix Agent

Fix all lint issues in the repository until `pwsh ./lint.ps1` exits cleanly.
This is the **pre-PR lint sweep** - run it once before pull request
submission, not during normal development.

# Workflow (MANDATORY)

1. **Auto-fix pass**: Run `pwsh ./fix.ps1` to silently apply all
   automatic fixes (dotnet format, markdownlint, yamlfix).

2. **Fix loop** (maximum 5 iterations):

   a. Run `pwsh ./lint.ps1` and capture the full output.

   b. If exit code is 0 - the repository is lint-clean. Proceed to the report.

   c. Parse the failures and fix each one using the guidance below.

   d. Repeat.

3. **Budget exhausted**: If still failing after 5 iterations, report the
   remaining issues and stop with Result: FAILED.

# Fix Guidance by Failure Type

- **cspell spelling errors**: Add legitimate technical terms to `.cspell.yaml`
  under the `words` list. Correct genuine misspellings in the source text.
  Do not add misspelled words to the dictionary.

- **markdownlint MD013 (line length)**: Wrap long lines at natural break points,
  after commas, before conjunctions, or at sentence boundaries. Do not break
  in the middle of a code span or URL. **Pipe-tables that cannot be wrapped
  without breaking structure** are a special case - convert them to a bullet
  list if the data reads naturally that way, or rewrite as a
  [grid table](https://pandoc.org/MANUAL.html#tables) if a tabular layout is
  essential. Do not get stuck trying to squeeze a wide pipe-table into 120
  characters.

- **markdownlint other rules**: Apply the specific fix indicated in the output
  (e.g., missing blank lines, heading levels, code fence languages).

- **yamllint errors**: Fix indentation, trailing spaces, or missing document
  markers as indicated. Run `pwsh ./fix.ps1` again if structural YAML
  issues appear - yamlfix may handle them.

- **reqstream / reviewmark / versionmark failures**: Fix the referenced
  requirements or review configuration per the standards in
  `.github/standards/reqstream-usage.md` and `.github/standards/reviewmark-usage.md`.

# Rules

- Fix **only** lint issues - do not refactor, restructure, or make functional changes
- For spelling: prefer adding terms to `.cspell.yaml` over rewriting correct technical text
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

## Remaining Issues (only when Result is FAILED)

{List of unfixed lint failures with file:line references and why they could
not be automatically resolved}
```
