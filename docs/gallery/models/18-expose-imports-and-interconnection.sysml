// Two `expose` behaviors in one model:
//
// 1. A General View that exposes a package which re-exports the members of its unit packages
//    through `public import`. `expose RoverSystem::*;` draws the members the package owns and the
//    members it re-exports; a `private import` is not a re-export, so RoverInternals stays out.
// 2. Interconnection Views that expose a part definition (`expose Rover;`) and a part usage
//    (`expose rover;`), each drawn with the parts, ports and connection inside its frame.

package RoverPropulsion {
    part def Motor;
    part def Gearbox;
}

package RoverSensing {
    part def Camera;
    part def Lidar;
}

// Used by RoverSystem but not re-exported: its private import keeps Bracket out of the view.
package RoverInternals {
    part def Bracket;
}

package RoverSystem {
    // Re-exports every member of RoverPropulsion and the one member Camera of RoverSensing.
    public import RoverPropulsion::*;
    public import RoverSensing::Camera;
    private import RoverInternals::*;

    part def RoverController;
}

package RoverViews {

    // The RoverSystem frame holds the member it owns (RoverController); the re-exported members
    // belong to their own packages, so they are drawn outside the frame.
    view ImportedUnitsGeneralView {
        expose RoverSystem;
        expose RoverSystem::*;
    }
}

package RoverStructure {

    port def PowerPort;

    part def Battery {
        inout port power : PowerPort;
    }

    part def DriveUnit {
        inout port power : PowerPort;
    }

    part def Mast;

    // Exposing Rover draws it with its own parts and the interface between their power ports;
    // the port `charge` sits on its border.
    part def Rover {
        inout port charge : PowerPort;
        part battery : Battery;
        part drive : DriveUnit;
        part mast : Mast;
        interface connect battery.power to drive.power;
        connection c1 connect drive to mast;
    }

    part rover : Rover;

    view RoverInterconnectionView {
        render Views::asInterconnectionDiagram;
        expose Rover;
    }

    // Exposing the usage draws the usage `rover : Rover` with the structure its type provides.
    view RoverUsageInterconnectionView {
        render Views::asInterconnectionDiagram;
        expose rover;
    }
}
