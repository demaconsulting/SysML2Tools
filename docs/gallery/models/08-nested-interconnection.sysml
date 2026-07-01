package ComputerSystem {

    part def Cpu;
    part def Chipset;
    part def Ram;
    part def PowerSupply;
    part def Storage;

    // Inner container: a motherboard with its own internal parts and connections.
    part def Motherboard {
        part cpu : Cpu;
        part chipset : Chipset;
        part ram : Ram;

        connection mc1 connect cpu to chipset;
        connection mc2 connect chipset to ram;
    }

    // Outer container: selected as the render root (3 connections beats Motherboard's 2).
    // The board part is typed by Motherboard, so its interior renders nested inside the board box.
    part def Computer {
        part board : Motherboard;
        part psu : PowerSupply;
        part storage : Storage;

        connection c1 connect psu to board;
        connection c2 connect psu to storage;
        connection c3 connect board to storage;
    }

    view def ComputerSystemInterconnectionView {}
}
