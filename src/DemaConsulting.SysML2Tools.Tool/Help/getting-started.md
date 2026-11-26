---
title: Getting started
summary: Install the tool, check that it works, and lint and render a first model.
aliases:
  - start
  - quickstart
  - examples
  - install
tags:
  - tutorial
order: 1
see-also:
  - commands
  - sysml-models
  - command-line
examples:
  - name: install-local
    kind: snippet
    language: shell
    save-as: install-local.sh
    description: Add the tool to the tool manifest of a repository.
    content: |
      dotnet new tool-manifest
      dotnet tool install --local DemaConsulting.SysML2Tools.Tool
  - name: ci-steps
    kind: snippet
    language: yaml
    save-as: ci-steps.yaml
    description: GitHub Actions steps that set up .NET and run the tool.
    content: |
      - name: Check out the repository
        uses: actions/checkout@v4
      - name: Set up .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.x
      - name: Restore the local .NET tools
        run: dotnet tool restore
      - name: Run {{tool}}
        run: dotnet {{tool}} lint --strict-unresolved "docs/**/*.sysml"
      - name: Render the views
        run: dotnet {{tool}} render "docs/**/*.sysml" --output docs/generated
  - name: version
    kind: command
    language: shell
    description: Print the version and exit.
    content: '{{tool}} --version'
  - name: validate
    kind: command
    language: shell
    description: Run the built-in self-tests. Exits 0 when every test passes.
    content: '{{tool}} --validate'
    expect: 'Failed: 0'
  - name: lint
    kind: command
    language: shell
    description: Check the sample model for errors.
    content: '{{tool}} lint vehicle.sysml'
    expect: 'lint: no errors found.'
  - name: render
    kind: command
    language: shell
    description: Draw the views of the sample model as SVG files.
    content: '{{tool}} render vehicle.sysml --output out'
    expect: 'Rendered 1 view(s).'
  - name: topic
    kind: command
    language: shell
    description: Print a help topic, here the card of the lint command.
    content: '{{tool}} help lint'
    expect: '{{tool}} lint [--strict-unresolved] <files...>'
---
{{tool}} {{version}} is a .NET tool.
It reads the SysML v2 files that you name, and writes only where you
ask: `render` writes diagrams (to the current folder by default),
and `--output`, `--log`, and `--results` name the other files it
writes. `--validate` uses a temporary folder.

1. **Install it.** The tool needs the .NET 10 SDK. It runs only on
   .NET 10, not on a later major version alone. For a repository or CI,
   install it as a local tool. Run the commands
   that [[#install-local]] prints once, without
   `dotnet new tool-manifest` when `.config/dotnet-tools.json` exists, and
   commit that file. In GitHub Actions, copy the steps that
   [[#ci-steps]] prints. They set up .NET 10 with
   `actions/setup-dotnet`, run `dotnet tool restore`, and then run
   `dotnet {{tool}}` to lint the model and render its views. For one
   machine, install it globally and run it as `{{tool}}`:

   ```bash
   dotnet tool install -g DemaConsulting.SysML2Tools.Tool
   ```

2. **Check it.** The command that [[#version]] prints shows the version.
   The command from [[#validate]] runs the built-in self-tests and
   exits 0 when all pass; see [[self-validation]].
3. **Lint a model.** Save the sample model as `vehicle.sysml`; the
   command that [[sysml-models#sample]] prints shows it.
   The command from [[#lint]] checks it and prints
   `lint: no errors found.`; see [[commands]] for every command.
4. **Render its views.** The command from [[#render]] writes
   `out/VehicleView.svg`; see [[views]].
5. **Ask about it.** [[model-queries]] shows how to find elements and
   follow their dependencies and requirements.

Print any help topic with `{{tool}} help <topic>`, and the card of a
command with `{{tool}} help <command>`, as in the command that
[[#topic]] prints. Add `--search <words>` to find a topic, and
`--json` to get any help answer as JSON.
