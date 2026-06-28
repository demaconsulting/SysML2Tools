package PowerSystemModel {

    part def Engine;
    part def Transmission;
    part def Driveshaft;
    part def Differential;
    part def Wheel;

    // PowerSystem decomposes into connected parts forming a drivetrain chain.
    part def PowerSystem {
        part engine : Engine;
        part transmission : Transmission;
        part driveshaft : Driveshaft;
        part differential : Differential;
        part wheelLeft : Wheel;
        part wheelRight : Wheel;

        connection c1 connect engine to transmission;
        connection c2 connect transmission to driveshaft;
        connection c3 connect driveshaft to differential;
        connection c4 connect differential to wheelLeft;
        connection c5 connect differential to wheelRight;
    }

    // The view name contains "Interconnection" to select the interconnection layout.
    view def PowerSystemInterconnectionView {}
}
