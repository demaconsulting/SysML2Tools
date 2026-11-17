package DesktopComputer {

    port def SataPort;

    attribute def Frequency;
    item def Firmware;
    part def Display;

    part def Cpu;
    part def Ram;
    part def Gpu;

    part def Ssd {
        inout port sata : SataPort;
    }

    part def PowerSupply;
    part def NetworkCard;

    part def Motherboard {
        inout port sata : SataPort;
    }

    // The motherboard interconnects all components. The cpu documents itself and shows an
    // attribute compartment, memory is a two-element part, monitor is a reference part
    // (dashed outline), and firmware is an item rather than a part.
    part def Workstation {
        part cpu : Cpu {
            doc /* Executes the program instructions. */
            attribute clock : Frequency;
        }
        part memory : Ram [2];
        part graphics : Gpu;
        part storage : Ssd;
        part psu : PowerSupply;
        part network : NetworkCard;
        part board : Motherboard;
        ref part monitor : Display;
        item firmware : Firmware;

        connection c1 connect board to cpu;
        connection c2 connect board to memory;
        connection c3 connect board to graphics;
        interface connect board.sata to storage.sata;
        connection c5 connect board to network;
        connection c6 connect psu to board;
        connection c7 connect psu to graphics;
        connection c8 connect cpu to memory;
        connection c9 connect graphics to monitor;
        connection c10 connect board to firmware;
    }

    view def WorkstationInterconnectionView {
        render asInterconnectionDiagram;
    }

    // View-scoped rendering: exposing just cpu and memory narrows the diagram to those
    // two parts and the connection between them (c8), dropping every connection with an
    // endpoint outside the exposed scope (board, graphics, storage, psu, network).
    view CoreLinkInterconnectionView {
        render asInterconnectionDiagram;
        expose Workstation::cpu;
        expose Workstation::memory;
    }
}
