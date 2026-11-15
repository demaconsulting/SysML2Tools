package Vehicle {

    // Two packages nested inside the Vehicle package. A General View draws each nested
    // package as a folder inside its owner's folder, instead of beside it.
    package Powertrain {
        part def Engine;

        // A specialization inside one nested package: both definitions share a folder.
        part def ElectricMotor :> Engine;
    }

    package Body {
        part def Chassis;
        part def SportsChassis :> Chassis;

        // A specialization across two sibling packages: Hybrid specializes Powertrain's
        // Engine from Body's folder, so the edge crosses between the two nested folders.
        part def Hybrid :> Powertrain::Engine;
    }

    // Car is owned by the outer Vehicle package and typed by definitions from both nested
    // packages. Its parts draw composition edges from the nested definitions out to the
    // owner-level Car box, crossing the nested folders' borders.
    part def Car {
        part engine : Powertrain::Engine;
        part chassis : Body::Chassis;
    }

    view def VehiclePackagesGeneralView {}
}
