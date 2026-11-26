---
title: SysML v2 models
summary: The SysML v2 text that the tool reads, with a complete sample model.
aliases:
  - sysml
  - models
  - language
  - kerml
  - sample
tags:
  - reference
order: 5
see-also:
  - workspaces
  - views
  - model-queries
examples:
  - name: sample
    kind: source
    language: sysml
    save-as: vehicle.sysml
    description: A small car model that the examples of this help use.
    content: |
      package Vehicle {
          doc /* A small car model for the help examples. */
          private import ScalarValues::*;

          metadata def SoftwareInfo {
              attribute owner : String;
          }

          port def FuelPort;
          part def Machine;
          part def Engine {
              port fuelIn : FuelPort;
          }
          part def Tank {
              port fuelOut : FuelPort;
          }
          part def Wheel;

          part def Car :> Machine {
              doc /* A car with an engine, a tank, and four wheels. */
              @SoftwareInfo { owner = "drivetrain team"; }
              attribute mass : Real;
              part engine : Engine;
              part tank : Tank;
              part wheels : Wheel[4];
              connection fuelLine connect tank.fuelOut to engine.fuelIn;
          }

          state def Ignition {
              entry; then off;
              state off;
              state on;
              transition turnOn first off then on;
              transition turnOff first on then off;
          }

          requirement def MassLimit {
              doc /* The car shall weigh less than 1500 kg. */
          }
          requirement carMass : MassLimit;
          satisfy carMass by Car;

          part def Garage;
          dependency Garage to Car;

          view VehicleView {
              expose Vehicle::*;
          }
      }
  - name: lint-sample
    kind: command
    language: shell
    description: Check the sample, with unresolved references as errors.
    content: '{{tool}} lint --strict-unresolved vehicle.sysml'
    expect: 'lint: no errors found.'
  - name: describe-sample
    kind: command
    language: shell
    description: Describe the part definition Car of the sample.
    content: '{{tool}} query describe -e Vehicle::Car vehicle.sysml'
    expect: '- Metadata SoftwareInfo.owner: drivetrain team'
---
{{tool}} reads the SysML v2 textual notation in `.sysml` files and
KerML in `.kerml` files, and checks names by the SysML v2 rules. The
sample that [[#sample]] prints is a complete model; the other cards of
this help use it as `vehicle.sysml`. It shows the common parts of the
language:

- a `package` with a `doc` comment, and a `private import` of a
  standard library package (`ScalarValues`, for `Real` and `String`);
- a `metadata def` and its use, `@SoftwareInfo { owner = ...; }`, which
  [[commands/query/describe]] reports as a `Metadata` line;
- `port def`, `part def`, and their usages, a specialization (`:>`), a
  typing (`:`), and a multiplicity (`[4]`);
- a `connection` that `connect`s two ports through feature chains;
- a `state def` with an entry transition and named transitions;
- a `requirement def`, a requirement, and `satisfy`;
- an explicit `dependency`, which `query` reports like a reference;
- a `view` that `expose`s the package members; see [[views]].

The tool also reads items, actions, interfaces, flows, bindings,
`verify` and `allocate`, aliases, redefinition (`:>>`), `comment`
elements, and view definitions. Names follow the SysML v2 rules:
`private` members and imports are not visible from other packages, and
an import applies to the package that declares it.

Check a model with `lint` ([[#lint-sample]]); `--strict-unresolved`
makes every name that does not resolve an error, as SysML v2 requires.
See [[workspaces]] for how the files of a model are loaded together.
