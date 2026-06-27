// Test fixture: two nested packages each containing several part defs and one top-level view def.
// Used to verify rendering of multi-package workspaces with a declared view.

package SystemA {

    view def SystemAView {}

    package Subsystem1 {
        part def ComponentA {}
        part def ComponentB {}
        part def ComponentC {}
        part def ComponentD {}
    }

    package Subsystem2 {
        part def WidgetX {}
    }
}
