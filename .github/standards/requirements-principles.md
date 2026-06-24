---
name: Requirements Principles
description: Follow these standards when creating, reviewing, or evaluating requirements.
---

# Unidirectional Flow (MANDATORY)

Requirements flow strictly top-down - never in reverse:

```text
User/System Needs → Requirements → Design → Implementation
```

- **Requirements** express WHAT is needed - derived from user/system needs only
- **Design** expresses HOW requirements are satisfied - derived from requirements only

Anti-patterns that MUST NOT occur:

- Writing a requirement because a class, method, or module exists in the code
- Updating requirements to match an implementation decision already made
- Requirements that describe HOW something is built rather than WHAT it must do

# What Makes a Requirement

A requirement expresses **observable, testable behavior** - what the system must
do, not how it does it, so that compliance can be verified without reading
implementation code.

- **Valid**: "The parser shall report the line number of the first syntax error."
- **Not a requirement (design decision)**: "The parser shall use a `TokenStream` class."

A unit may use its own name freely - that is identity, not HOW. What is
forbidden is describing *internal construction*: class names, method signatures,
algorithms, or data structures.

# Requirements at Every Level (MANDATORY)

Every identified subsystem and unit MUST have its own requirements file because
reviewers must see what each item is responsible for satisfying and auditors must
be able to trace which items implement which requirements.

Requirements at each level decompose the parent requirement into the behavioral
responsibility of that software item, and links flow downward only -
unit requirements MUST NOT link upward to parent requirements:

```text
System requirement
  └─ Subsystem requirement (what this subsystem must do)
       └─ Unit requirement (what this unit must do)
```

Before writing a subsystem or unit requirement ask: *"Am I decomposing a parent
requirement into this item's responsibility, or describing what the code already does?"*
Decomposing a parent requirement is valid. Describing existing code is back-driving.

# Test Independence

- **Every requirement MUST link to at least one passing test** because untested
  requirements have no compliance evidence
- **Requirements MUST link to tests at their own level** - system requirements to
  system-level tests, subsystem requirements to subsystem-level tests, unit
  requirements to unit-level tests; linking across levels produces misleading
  compliance evidence
- **Tests MAY exist without a requirement** - corner-case, defensive, and regression
  tests are valid; never flag them as non-compliant

# Quality Gates

- [ ] All requirements describe observable behavior (WHAT), not implementation (HOW)
- [ ] No requirement was derived from or driven by existing design or code
- [ ] Every requirement links to at least one passing test
- [ ] Every identified subsystem has a requirements file
- [ ] Every identified software unit has a requirements file
- [ ] Subsystem and unit requirements decompose parent requirements top-down, not bottom-up from code
- [ ] Tests without a requirement are accepted as valid
