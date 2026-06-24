---
name: developer
description: Comprehensive development agent for code, documentation, and requirements across multiple languages
user-invocable: true
---

# Developer Agent

Perform software development tasks by determining and applying appropriate standards from `.github/standards/`.

# Standards-Based Workflow

1. **Analyze the request** to identify scope: languages, file types, requirements, testing, reviews
2. **Read relevant standards** using the selection matrix in AGENTS.md
3. **Pre-flight verification** before making any changes:
   - List files that will be created, modified, or deleted
   - For each file to be **created**, check whether a counterpart exists in the
     template (URL in the `# Reference Template` section of `AGENTS.md`).
     If one exists, fetch it as the starting point; adjust placeholder names and heading
     depth to match the target path before writing the file
   - For each modified file, identify which companion artifacts need updating
     (requirements, design docs, verification docs, tests, review-sets, README.md, user guides)
   - Include companion artifact updates in the work plan
4. **Execute work** following standards requirements and quality checks
5. **Formatting**: Run `pwsh ./fix.ps1` to silently apply all
   available auto-fixers (dotnet format, markdown, YAML) before committing
6. **Build and test** (code changes only): Run `pwsh ./build.ps1` and confirm it
   passes - report FAILED if the build or any tests fail
7. **Generate completion report** per the AGENTS.md reporting requirements - save to
   `.agent-logs/{agent-name}-{subject}-{unique-id}.md` and return the summary to the caller

# Report Template

```markdown
# Developer Agent Report

**Result**: (SUCCEEDED|FAILED)
**Report**: `.agent-logs/developer-{subject}-{unique-id}.md`

- **Files Modified**: {List of files created/modified/deleted}
- **Languages Detected**: {Languages identified}
- **Standards Applied**: {Standards files consulted}

## Tooling Executed

- **Language Tools**: {Compilers, formatters, and build tools used}
- **Compliance Tools**: {ReqStream, ReviewMark tools used}
- **Validation Results**: {Tool execution results}

## Compliance Status

- **Quality Checks**: {Standards quality checks status}
- **Issues Resolved**: {Any problems encountered and resolved}
```
