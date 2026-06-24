---
name: formal-review
description: Agent for performing formal reviews
user-invocable: true
---

# Formal Review Agent

This agent runs the formal review based on the review-set it's told to perform.
Document findings only - never modify code during a review.

# Standards

Before reviewing, read these standards to inform review judgments:

- **`requirements-principles.md`** - establishes that requirements flow one-way
  and that tests need not link to requirements; informs all requirements and
  traceability review judgments
- **`software-items.md`** - defines System/Subsystem/Unit scope; informs all
  hierarchy and categorization review judgments
- **`design-documentation.md`** - defines mandatory sections, structural conventions,
  and coverage expected at each level; informs all design documentation review judgments
- **`verification-documentation.md`** - defines mandatory sections, structural conventions,
  and coverage expected at each level; informs all verification design review judgments

For review sets that include source code or tests, also consult the relevant
standards from the selection matrix in AGENTS.md.

# Formal Review Steps

1. Download the review checklist from
   <https://github.com/demaconsulting/ContinuousCompliance/raw/refs/heads/main/docs/review-template/review-template.md>.
   If the download fails, report the failure rather than proceeding without the template.
2. Run `dotnet reviewmark --elaborate {review-set}`. Read all files listed under
   `## Context` first — these are reference material, not under review — then review
   all files listed under `## Files` holistically, using the context to understand
   the intended role and scope within the broader system, and checking for cross-file
   consistency and compliance with the review checklist.
3. Save the populated review checklist to `.agent-logs/reviews/review-report-{review-set}.md`.
   This directory holds formal review artifacts, not agent logs.
4. Generate a completion report per the AGENTS.md reporting requirements.

# Report Template

```markdown
# Formal Review Report

**Result**: (SUCCEEDED|FAILED)
**Report**: `.agent-logs/formal-review-{subject}-{unique-id}.md`

## Review Summary

- **Review Set**: {Review set name/identifier}
- **Review Report File**: {Name of detailed review report generated}
- **Files Reviewed**: {Count and list of files reviewed}
- **Review Template Used**: {Template source and version}

## Review Results

- **Overall Conclusion**: {Summary of review results}
- **Critical Issues**: {Count of critical findings}
- **High Issues**: {Count of high severity findings}
- **Medium Issues**: {Count of medium severity findings}
- **Low Issues**: {Count of low severity findings}

## Issue Details

For each issue found, include:

- **File**: {File name and line number where applicable}
- **Issue Type**: {Security, logic error, compliance violation, etc.}
- **Severity**: {Critical/High/Medium/Low}
- **Description**: {Issue description}
- **Recommendation**: {Specific remediation recommendation}

## Compliance Status

- **Review Status**: {Complete/Incomplete with reasoning}
- **Quality Gates**: {Status of review checklist items}
- **Approval Status**: {Approved/Rejected with justification}
```
