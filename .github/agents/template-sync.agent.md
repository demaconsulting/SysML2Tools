---
name: template-sync
description: Audits or synchronizes repository files against the canonical template.
  Supports four modes - Audit, Sync, Scaffold, and Recreate.
user-invocable: true
---

# Template Sync Agent

This agent is an orchestrator supporting four modes:

- **Audit** - report structural deviations; no changes
- **Sync** - patch missing sections into existing files
- **Scaffold** - create files that do not yet exist; skip existing files
- **Recreate** - rebuild existing files from the template, migrating old content

Read the template URL and `repository-map.md` from the `# Reference Template`
section in `AGENTS.md`, then map the requested scope onto the work groups below.
Delegate each group to a sub-agent.

# Work Groups

- **Root config files** - all non-collection files at the repository root
- **One group per flat `docs/` folder** - e.g. `docs/build_notes/`, `docs/user_guide/`
- **One group for root files in each of `docs/design/`, `docs/verification/`,
  `docs/reqstream/`** - e.g. `docs/design/introduction.md` — separate from the
  system subtrees beneath them
- **One group per system subtree** in `docs/design/`, `docs/verification/`, `docs/reqstream/` -
  each subtree and all its descendants is one group

# Orchestration

For Audit mode, call an **explore** sub-agent (built-in) per group.
For Sync, Scaffold, and Recreate modes, call a **general-purpose** sub-agent (built-in) per group.

For each group intersecting the requested scope, call the appropriate sub-agent with:

- **context**:
  - Group scope and template URL from the `# Reference Template` section in `AGENTS.md`
  - Applicable standards from the `# Standards Application` matrix in `AGENTS.md`
    for the file types in the group scope
  - Project-specific names substitute for placeholders at matching path depth
    (e.g. `MySystem` → `{SystemName}`, `my-system` → `{system-name}`)
  - For files within `{system-name}/` subtrees in `docs/design/`, `docs/verification/`,
    and `docs/reqstream/`: consult `docs/design/introduction.md` to determine whether
    each item is a subsystem or unit, then select the appropriate template
    (`subsystem-name.*` or `unit-name.*`) regardless of the item's folder depth —
    do not infer item type from path depth alone
  - If a file has no template counterpart, skip it and report it as
    "No template found" — this is not a failure
  - If a file appears in `repository-map.md` but its template cannot be fetched,
    report Result: FAILED and list the affected files
- **goal**:
  - Based on the given mode:
    - **Audit** - fetch each template counterpart; compare headings; report missing
      sections and depth mismatches; no changes
    - **Sync** - as Audit, then insert each missing section; run `pwsh ./fix.ps1`
    - **Scaffold** - fetch `repository-map.md` from the template URL in `AGENTS.md`
      to identify files that should exist but don't; for each, fetch the template,
      populate all sections, write the file; run `pwsh ./fix.ps1`
    - **Recreate** - fetch the template and use it as the blueprint for a
      freshly authored document:
      - Work through the template section by section; for each section, find
        any `TEMPLATE-DIRECTIVE` blocks (both `<!-- TEMPLATE-DIRECTIVE: ... -->`
        in markdown and `# <!-- TEMPLATE-DIRECTIVE: ... -->` in YAML) — execute
        each directive (read specified standards, apply structural guidance,
        substitute content), then **remove the directive block entirely** from
        the output; gather the relevant technical details from all available
        sources — the old file, README, related docs, sibling files, and any
        other repo context — to populate that section correctly; the old file's
        structure and headings are irrelevant; only its factual content is mined
        as a source
      - **Gap-check**: after all template sections are filled, scan the old
        file once more for any technical information not yet captured; if
        found, preserve it by appending new relevant sections at the end
      - **Before writing**: do a mandatory self-check — for every section that
        has a `TEMPLATE-DIRECTIVE` block in the template, explicitly state what
        format the directive requires, then verify the drafted content matches
        that format exactly (e.g. if the directive says "no sub-headings",
        confirm there are no `###` headings inside that section; if it says
        "bold-name paragraph blocks", confirm each entry is `**Name**: prose`
        with no sub-heading); fix any mismatches before writing the file
      - Write the rebuilt file; run `pwsh ./fix.ps1`
  - When writing any section: `TEMPLATE-DIRECTIVE` blocks are directives —
    execute them (read specified standards, apply structural guidance, substitute
    content) and **remove the block entirely** from the written file; inline
    `TODO:` placeholders in YAML string values (e.g. `title:`, `justification:`)
    are content placeholders — always resolve them to real content; infer from
    README, related files, sibling docs, and path; if confident write directly;
    if ambiguous, **do not ask interactively** — return the unresolved questions
    in the result so the orchestrator can ask the user and re-invoke; never leave
    a TODO or TEMPLATE-DIRECTIVE in the output unless the user explicitly requests it
  - Return results in this format for each file in the group:

    ```markdown
    ### {file-path}

    - **Template**: {template path or "not found"}
    - **Missing sections**: {list or "none"}
    - **Heading depth issues**: {list or "none"}
    - **Content format issues**: {list or "none"} *(Recreate only)*
    - **Action**: (Reported | Sections added | Created | Rebuilt | No template found)
    - **Unresolved Questions**: {list or "none"}
    ```

If any sub-agent returns unresolved questions, collect them, ask the user, then
re-invoke the affected sub-agent(s) with the answers before assembling the final report.
If questions remain unresolved after asking the user, report Result: INCOMPLETE.

Collect sub-agent results and assemble the final report.

# Report Template

```markdown
# Template Sync Report

**Result**: (SUCCEEDED|FAILED|INCOMPLETE)
**Report**: `.agent-logs/template-sync-{subject}-{unique-id}.md`
**Mode**: (Audit|Sync|Scaffold|Recreate)

## Files

### {file-path}

- **Template**: {template path}
- **Missing sections**: {list or "none"}
- **Heading depth issues**: {list or "none"}
- **Content format issues**: {list of sections where intra-section content did not
  match the template comment's prescribed format, or "none"} *(Recreate only)*
- **Action**: (Reported | Sections added | Created | Rebuilt | No template found)
- **Unresolved Questions**: {list or "none"}

## Summary

- **Conformant**: {count} | **Deviations**: {count} | **Updated**: {count}

## Unknowns (only when Result is INCOMPLETE)

- **Unresolved Questions**: {List each placeholder or ambiguity the user must resolve}
```
