package DesktopComputer {

    part def Cpu;
    part def Ram;
    part def Gpu;
    part def Ssd;
    part def PowerSupply;
    part def NetworkCard;
    part def Motherboard;

    // The motherboard interconnects all components.
    part def Workstation {
        part cpu : Cpu;
        part memory : Ram;
        part graphics : Gpu;
        part storage : Ssd;
        part psu : PowerSupply;
        part network : NetworkCard;
        part board : Motherboard;

        connection c1 connect board to cpu;
        connection c2 connect board to memory;
        connection c3 connect board to graphics;
        connection c4 connect board to storage;
        connection c5 connect board to network;
        connection c6 connect psu to board;
        connection c7 connect psu to graphics;
        connection c8 connect cpu to memory;
    }

    view def WorkstationInterconnectionView {}

    // View-scoped rendering: exposing just cpu and memory narrows the diagram to those
    // two parts and the connection between them (c8), dropping every connection with an
    // endpoint outside the exposed scope (board, graphics, storage, psu, network).
    view CoreLinkInterconnectionView {
        expose Workstation::cpu;
        expose Workstation::memory;
    }
}
