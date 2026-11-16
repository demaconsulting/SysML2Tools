package ChargingStation {

    part def Cable;
    part def Plug;

    // Charger owns two nested part usages that carry their own annotations. In a General
    // View each usage is its own rounded box, joined to Charger by a composition edge and
    // to its type by a feature-typing edge.
    part def Charger {

        // Documentation owned by a usage is drawn as a "doc" compartment inside that
        // usage's box. A comment owned by the same usage is drawn as a separate note
        // (folded corner) attached to the usage's box by a dashed line.
        part cable : Cable {
            doc /* Delivers up to 22 kW to the vehicle. */
            comment /* Rated for outdoor use. */
        }

        // Documentation alone adds only the "doc" compartment.
        part plug : Plug {
            doc /* Type 2 connector. */
        }
    }

    view def ChargerGeneralView {}
}
