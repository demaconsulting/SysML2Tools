package TypedVehicle {

    // Two packages nested inside TypedVehicle. A General View draws each nested package as a
    // folder inside its owner's folder. A folder that holds content shows its name in the
    // folder's tab.
    package Powertrain {
        part def Engine;
        part def ElectricMotor :> Engine;
    }

    package Body {
        part def Chassis;
        part def SportsChassis :> Chassis;
    }

    // Car's two part usages are typed by definitions from the nested packages, so each
    // feature-typing edge leaves its usage and ends on the definition inside a nested folder.
    part def Car {
        part engine : Powertrain::Engine;
        part chassis : Body::Chassis;
    }

    view def VehicleTypingGeneralView {}
}
