package DriveSystem {

    // An abstract definition: its keyword line reads «abstract part def» and its name is
    // drawn in italics.
    abstract part def Motor;

    // A definition-to-definition specialization: solid line, hollow triangle at the
    // general definition.
    part def InductionMotor :> Motor;

    // DriveUnit owns three usages. A General View draws each part usage as its own rounded
    // box, joined to its owner by a composition edge (filled diamond at the owner) and to
    // its type by a feature-typing edge (hollow triangle with two dots at the type).
    part def DriveUnit {

        // A part usage with a multiplicity, typed by the abstract definition.
        part motors : Motor [2];

        // A part usage that owns documentation and a comment: the documentation is drawn
        // as a "doc" compartment inside the usage's box; the comment is drawn as a note
        // attached to the box by a dashed line.
        part main : Motor {
            doc /* The primary motor, rated 120 kW. */
            comment /* Replaced by an induction motor in RearDrive. */
        }

        // A reference usage: drawn with a dashed outline and the «ref part» keyword, and
        // joined to its owner by a hollow diamond.
        ref part spare : InductionMotor;
    }

    // RearDrive specializes DriveUnit and narrows two of the inherited usages. Neither
    // repeats a type, so each inherits the type of the usage it narrows:
    //   :>   subsetting    - hollow triangle at the subsetted usage
    //   :>>  redefinition  - hollow triangle with a crossbar at the redefined usage
    part def RearDrive :> DriveUnit {
        part frontMotor :> motors;
        part mainMotor :>> main;
    }

    view def DriveUnitGeneralView {}
}
