---
name: sysml2tools
schema: 2
title: SysML2 Tools
summary: Built-in help of SysML2 Tools, which lints, renders, queries, and exports SysML v2 models.
---
{{tool}} reads SysML v2 textual models (`.sysml` files) and lints them,
renders their views as SVG or PNG diagrams, answers questions about the
model, and exports the resolved model as JSON. It runs on a developer
machine, in CI, and for AI agents that work on a model.

Each task is a command, such as `{{tool}} lint "**/*.sysml"`. `help`
must be the first argument and takes topic names, such as
`{{tool}} help workspaces`, and command names, such as
`{{tool}} help lint` or `{{tool}} help query describe`.
