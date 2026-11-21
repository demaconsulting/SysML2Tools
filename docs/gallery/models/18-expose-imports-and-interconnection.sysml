// Two `expose` behaviors in one model:
//
// 1. A General View that exposes a package, `expose RoverSystem::*;`. An expose ignores visibility,
//    so it draws every member the package owns (public or private) and every member it brings in
//    through an import, whether the import is `public import` (a re-export) or `private import`
//    (a use). Only the members the imported packages make public come in.
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

// Brought in through RoverSystem's private import; its private member Fastener is not imported.
package RoverInternals {
    part def Bracket;
    private part def Fastener;
}

package RoverSystem {
    // Every member of RoverPropulsion and the one member Camera of RoverSensing.
    public import RoverPropulsion::*;
    public import RoverSensing::Camera;
    private import RoverInternals::*;

    part def RoverController;
    private part def RoverDiagnostics;
}

package RoverViews {

    // The RoverSystem frame holds the members it owns (RoverController and the private
    // RoverDiagnostics); the imported members belong to their own packages, so they are drawn
    // outside the frame.
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
