---
name: Coding Principles
description: Follow these standards when developing any software code.
---

# Core Principles

## Literate Coding

All code MUST follow literate programming principles:

- **Intent Documentation**: Function and method documentation (XmlDoc, Doxygen,
  JSDoc, etc.) MUST explain WHY the function exists and its design purpose -
  not just restate what it does - because reviewers must verify implementation
  matches design intent without reading the full codebase
- **Logical Separation**: Complex functions use block comments to separate and
  describe logical steps within the implementation
- **Full Symbol Documentation**: ALL symbols have comprehensive documentation —
  not just the public interface, because reviewers and auditors must verify every
  implementation detail. Access-level specifics vary by language; see the language-specific standard.
- **Clarity Over Cleverness**: Code should be immediately understandable by team members

## API Documentation

Good API documentation enables consumers, reviewers, and agents to use an
interface correctly without reading the implementation:

- **Self-Contained**: Each member's documentation must be fully understandable
  in isolation - consumers must not need to read the implementation to call it
  correctly
- **Intent-Focused**: Explain WHY the member exists and WHAT problem it solves,
  not just restate the name - this lets reviewers verify the implementation
  matches design intent
- **Parameter and Return Contracts**: Document valid ranges, null handling, and
  boundary cases - agents and consumers rely on these contracts to call the API
  correctly
- **Error Conditions**: Document every exception or error code, the condition
  that triggers it, and how the caller should respond - undocumented errors
  cannot be handled correctly
- **Side Effects**: Document I/O, state mutation, resource allocation, or
  network calls - hidden side effects cause integration bugs that are hard to
  diagnose
- **Thread Safety**: State whether the API is safe for concurrent use - missing
  this forces consumers to read the implementation or risk data races

## Universal Code Architecture Principles

### Design Patterns

- **Single Responsibility**: Functions with focused, testable purposes
- **Dependency Injection**: External dependencies injected for testing
- **Pure Functions**: Minimize side effects and hidden state
- **Clear Interfaces**: Well-defined API contracts
- **Separation of Concerns**: Business logic separate from infrastructure
- **Repository Structure Adherence**: Analyze existing directory conventions
  before creating files; place new files consistent with established patterns

### Compliance-Ready Code Structure

- **Documentation Standards**: Language-appropriate documentation required on ALL members
- **Error Handling**: Comprehensive error cases with appropriate exception handling and logging
- **Configuration**: Externalize settings for different compliance environments
- **Resource Management**: Proper resource cleanup using language-appropriate patterns

# Quality Gates

## Code Quality Standards

- [ ] Zero compiler warnings (use language-specific warning-as-error flags)
- [ ] All code follows literate programming style
- [ ] Language-appropriate documentation complete on all members
- [ ] Passes static analysis (language-specific tools)

## Universal Anti-Patterns

- **Skip Literate Coding**: Don't skip literate programming comments
- **Ignore Compiler Warnings**: Don't ignore compiler warnings
- **Hidden Dependencies**: Don't create untestable code with hidden dependencies
- **Hidden Functionality**: Don't implement functionality without requirement
  traceability because untraced functionality cannot be validated during audits
- **Monolithic Functions**: Don't write monolithic functions with multiple responsibilities
- **Overcomplicated Solutions**: Don't make solutions more complex than necessary
- **Premature Optimization**: Don't optimize for performance before establishing correctness
- **Copy-Paste Programming**: Don't duplicate logic - extract common functionality into reusable components
- **Magic Numbers**: Don't use unexplained constants - either name them or add clear comments

# Language-Specific Implementation

For each detected language, read `{language}-language.md` from `.github/standards/`
and apply its standards, tooling, and conventions.
