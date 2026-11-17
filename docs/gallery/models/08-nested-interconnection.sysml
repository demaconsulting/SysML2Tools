package ComputerSystem {

    port def PowerPort;
    port def SataPort;
    port def MainsPort;
    port def VideoPort;

    part def Cpu;
    part def Chipset;
    part def Ram;

    part def PowerSupply {
        out port boardRail : PowerPort;
        out port storageRail : PowerPort;
    }

    part def Storage {
        in port power : PowerPort;
        inout port sata : SataPort;
    }

    // Inner container: a motherboard with its own internal parts and connections. Its own
    // ports are drawn on its border when it is used as a part of the computer.
    part def Motherboard {
        in port power : PowerPort;
        inout port sata : SataPort;

        part cpu : Cpu;
        part chipset : Chipset;
        part ram : Ram;

        connection mc1 connect cpu to chipset;
        connection mc2 connect chipset to ram;
    }

    // Outer container: selected as the render root (3 connections beat Motherboard's 2).
    // The board part is typed by Motherboard, so its interior renders nested inside the board box.
    // The computer's own ports (mains and video) are drawn on the frame border; connectors
    // to a frame port are not drawn.
    part def Computer {
        in port mains : MainsPort;
        out port video : VideoPort;

        part board : Motherboard;
        part psu : PowerSupply;
        part storage : Storage;

        connection c1 connect psu.boardRail to board.power;
        connection c2 connect psu.storageRail to storage.power;
        connection c3 connect board.sata to storage.sata;
    }

    view def ComputerSystemInterconnectionView {
        render asInterconnectionDiagram;
    }
}
