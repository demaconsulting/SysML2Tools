package VehicleModel {

    part def Engine;

    part def Wheel;

    port def FuelPort;

    // Vehicle aggregates several usages: attributes, a port, and nested parts.
    part def Vehicle {
        attribute mass : Real;
        attribute maxSpeed : Real;
        port fuelInlet : FuelPort;
        part engine : Engine;
        part wheels : Wheel[4];
    }

    // SportsCar specializes Vehicle to exercise a subclassification edge.
    part def SportsCar :> Vehicle {
        attribute topSpeed : Real;
    }

    view def VehicleStructureView {}
}
