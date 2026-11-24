---
name: Project Scripts
description: Follow these standards when running or modifying build.ps1, lint.ps1, fix.ps1, or scripts/.
globs: ["build.ps1", "lint.ps1", "fix.ps1", "scripts/**"]
---

# Project Scripts Standards

`build.ps1`, `lint.ps1`, and `fix.ps1` share one organization-wide contract so that every
repository, and every agent working in it, drives them the same way. The authoritative list of
switches, valid `-Check` names, and examples for a repository is the output of
`pwsh ./<script> -Help`; read it instead of guessing, and do not duplicate it in documentation.

# The Contract

- **A scope is required.** With no arguments a script prints an error line and its usage text and
  exits 1, so a caller that has not chosen a scope fails loudly. `-Help` prints the usage text and
  exits 0.
- **`-All`** runs the script's full job and cannot be combined with another scope. For `lint.ps1`
  it is the CI merge gate. `build.ps1 -All` is the full local build and test run; CI workflows may
  build with their own steps, so do not claim CI runs `build.ps1`.
- **`-Changed [-Base <ref>]`** limits the run to what the files changed since the merge-base with
  `origin/main` (else `main`), plus uncommitted and untracked files, affect.
- **`-DryRun`** prints the selection (what would run or be skipped) and runs nothing.
- **`build.ps1 -Project <names>`** exists only in multi-project repositories and builds and tests
  what the named projects affect. **`lint.ps1`/`fix.ps1 -Check <names>`** run only the named
  checks or fixers, in full; combined with `-Changed` they are limited to changed inputs. A `-Check`
  value must name at least one valid check or fixer: an unknown name, or a value with only delimiters
  (`-Check ','`), prints the valid names and exits 1 instead of running nothing.
- **Project names are unique.** The module keys the project graph by project file name, so two `.csproj`
  files with the same name (for example `src/Foo/Foo.csproj` and `test/Foo/Foo.csproj`) make the scoped
  build fail with a message naming both. Rename one, or leave the folder holding the duplicates out of
  the scanned `-Root` (fixture projects, for example).
- **`fix.ps1` always exits 0** when it actually runs fixers. Usage and argument errors still exit 1.

# Running the Scripts (ALL Agents)

- **Iterate scoped.** While developing, run `-Changed` (or `-Check`, or `-Project` where the
  repository's `build.ps1 -Help` lists it) so only the
  affected work runs. Use `-DryRun` to see the selection first.
- **Run `-All` once before handing work back** or opening a pull request: `pwsh ./build.ps1 -All`
  (code changes) and `pwsh ./lint.ps1 -All` (via the lint-fix agent). Re-run only if files change
  afterwards. A scoped run never substitutes for the full gate; say which gates ran in your report.
- **Documentation-only changes** need no build. Run `pwsh ./fix.ps1 -Changed`, then
  `pwsh ./lint.ps1 -All` before the pull request.
- **Do not guess switches.** Run `pwsh ./<script> -Help` when unsure; a call without a scope fails.

# Modifying the Scripts (ALL Agents)

- **The three scripts belong to the project.** Extend them as the project grows: add checks,
  fixers, steps, tool versions, and optional focus parameters at the `[PROJECT-SPECIFIC]` blocks.
- **`scripts/ProjectScripts.psm1` is shared verbatim.** It holds the logic common to every
  repository (usage text, argument validation, changed-file discovery, project graph). It is
  delivered by the Agents release zip, which is extracted into the repository root and so places
  the module at `scripts/ProjectScripts.psm1`. Never edit it in a consuming repository; update it
  only by extracting a newer release. If it must change for every
  repository, change it in the Agents repository (`src/scripts/ProjectScripts.psm1`) instead.
- **Repository-specific behavior** (extra checks or fixers, runtime-read maps, input filters) goes
  in the `[PROJECT-SPECIFIC]` blocks of the scripts, never in the module.
- **`-All` must keep doing the full job.** Adding a check or fixer means `-All` runs it; it must
  also appear in the usage text and be a valid `-Check` name.
- **Never remove or weaken a check** (or a test step) to make lint or tests pass.
- **Keep the usage text accurate and short** (about 25 lines or fewer). Update it whenever
  switches, checks, or fixers change.
- **`-DryRun` must reflect the real selection.** Whatever `-Changed`, `-Check`, or (where it exists) `-Project`
  would run is exactly what `-DryRun` prints; add a new selection rule to both together.
- **Keep the no-argument behavior.** Do not give a script a default scope; no arguments must still
  print the error and usage and exit 1.
- **Update callers.** Change every workflow under `.github/workflows`, script, and document that
  invokes a script to pass a scope, for example `pwsh ./lint.ps1 -All`.

# Quality Checks

- [ ] No-argument run prints an error and usage and exits 1; `-Help` exits 0
- [ ] `-All` runs every check, fixer, and test step; `-DryRun` matches what a real run selects
- [ ] `scripts/ProjectScripts.psm1` is byte-identical to the copy in the Agents release zip (source:
      `src/scripts/ProjectScripts.psm1` in the Agents repository)
- [ ] Repository-specific changes sit in `[PROJECT-SPECIFIC]` blocks
- [ ] Usage text is accurate and about 25 lines or fewer
- [ ] Workflows and documents call the scripts with an explicit scope
