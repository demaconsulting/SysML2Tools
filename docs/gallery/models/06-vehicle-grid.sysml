package VehicleTaxonomy {

    // A vehicle classification hierarchy rendered as a specialization matrix.
    part def Vehicle;

    part def LandVehicle :> Vehicle;
    part def WaterVehicle :> Vehicle;
    part def AirVehicle :> Vehicle;

    part def Car :> LandVehicle;
    part def Truck :> LandVehicle;
    part def Motorcycle :> LandVehicle;

    part def Boat :> WaterVehicle;
    part def Submarine :> WaterVehicle;

    part def Airplane :> AirVehicle;
    part def Helicopter :> AirVehicle;

    part def SportsCar :> Car;
    part def Sedan :> Car;
    part def PickupTruck :> Truck;

    view def TaxonomyMatrixView {}
}
