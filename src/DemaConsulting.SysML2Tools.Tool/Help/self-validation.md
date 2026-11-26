---
title: Self-validation
summary: Prove that the installed tool works, and save the evidence as test results.
aliases:
  - validate
tags:
  - reference
order: 8
see-also:
  - command-line
examples:
  - name: run
    kind: command
    language: shell
    description: Run the self-tests. Prints one line per test and exits 0 when all pass.
    content: '{{tool}} --validate'
    expect: |
      SysML2Tools_LintSelfTest - Passed
      Failed: 0
  - name: trx
    kind: command
    language: shell
    description: Also write the results as a TRX file.
    content: '{{tool}} --validate --results validation.trx'
  - name: junit
    kind: command
    language: shell
    description: Write the results as JUnit XML instead.
    content: '{{tool}} --validate --results validation.xml'
  - name: depth
    kind: command
    language: shell
    description: Start the report at heading level 2 to embed it in a larger document.
    content: '{{tool}} --validate --depth 2'
    expect: '## '
  - name: bad-extension
    kind: command
    language: shell
    description: Any other extension is an error. Exits 1.
    content: '{{tool}} --validate --results validation.txt'
    exit: 1
    expect: 'Unsupported results file format'
---
`--validate` runs a short set of tests of the tool itself in a temporary
folder: it shows the version and the usage, lints a model, renders
declared and on-demand views as SVG and PNG, and exports a model, so
that a regulated project can show that the installed version works on
its build machine.

**Report.** The output is Markdown. It starts with a heading and a table of
the tool version, machine name, operating system, .NET runtime, and time
stamp. Each test prints one line marked `Passed`, or `Failed` with a
reason, and the report ends with `Total Tests`, `Passed`, and `Failed`
lines. The exit code is 0 only when every test passes. `--depth` sets the
level of the first heading, as [[#depth]] shows.

**Results files.** `--results <file>` also writes the outcome; the
extension chooses the format. `.trx` writes a Visual Studio TRX file and
`.xml` writes JUnit XML, both readable by CI test-report tasks. Any other
extension is an error after the tests ran ([[#bad-extension]]).
The folder of the file must exist.

**Evidence.** The tests have stable names, such as
`TemplateTool_VersionDisplay` and `SysML2Tools_LintSelfTest`, so a
requirements file can list them as the tests that prove the tool works
in its environment.
