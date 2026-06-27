package Catalog {

    part def Vehicle;
    part def Car :> Vehicle;
    part def Truck :> Vehicle;
    part def SportsCar :> Car;

    package Components {
        part def Engine;
        part def Wheel;
        port def FuelPort;
    }

    // "Browser" in the name selects the browser (tree) layout.
    view def CatalogBrowserView {}

    // "Matrix" in the name selects the grid (relationship matrix) layout.
    view def SpecializationMatrixView {}
}
