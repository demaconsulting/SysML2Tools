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
