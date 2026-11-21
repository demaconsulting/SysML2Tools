package PartTree {

    private import ScalarValues::Real;

    // The definitions a part tree is drawn from. Engine and Wheel are ordinary definitions;
    // Driver is only referred to by a reference part.
    part def Engine {
        attribute displacement : Real;
        part cylinders : Cylinder [4];
    }

    part def Cylinder;

    part def Wheel;

    part def Driver;

    // Vehicle owns parts, a reference part, and an attribute. The view below exposes Vehicle
    // and Engine only: each owned part becomes a rounded «part» node joined to its owner by a
    // filled-diamond composition edge (a hollow diamond and a dashed outline for the reference
    // part), the attribute stays a row of Vehicle, and a part's feature-typing edge (hollow
    // triangle with two dots) is drawn only to a definition that the view exposes (Engine,
    // not Wheel or Driver).
    part def Vehicle {
        attribute mass : Real;
        part engine : Engine;
        part wheels : Wheel [4] {
            part hub;
        }
        ref part driver : Driver;
    }

    // A named dependency shows its name on the dashed line; an unnamed one shows «depend».
    dependency fueling from Vehicle to Engine;

    view VehicleStructureGeneralView {
        expose Vehicle;
        expose Engine;
    }
}
