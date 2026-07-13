package MissionControl {

    // A three-level-deep containment hierarchy, entirely through nested packages and
    // definitions (never through usage/type references), so `expose`'s qualified-name-prefix
    // scoping applies exactly as intended at every level.
    package GroundSegment {
        package RadioNetwork {
            part def Uplink;
            part def Downlink;
        }

        // OperatorConsole owns its own nested definitions, so a MembershipExact vs.
        // MembershipRecursive expose targeting it directly (rather than the package) has
        // real, visible content to include or omit.
        part def OperatorConsole {
            part def DisplayPanel;
            part def CommsHandset;
        }

        // A second direct child of GroundSegment (a leaf, no nested definitions of its
        // own) so the NamespaceDirectChildren view has more than one box to show —
        // distinguishing it from a MembershipExact expose of OperatorConsole alone.
        part def ThermalRegulator;
    }

    package SpaceSegment {
        part def Payload;
    }

    view def MissionControlGeneralView {}

    // ===== The four `expose` recursion forms, side by side on the same model =====
    //
    // See the user guide's "Expose Recursion Semantics" section for the full explanation.
    // All four views below scope the same nested model differently, purely by varying the
    // expose statement's recursion form:

    // `expose <path>;` (MembershipExact) — exposes only the named element itself:
    // OperatorConsole's own box, with neither DisplayPanel nor CommsHandset. The single
    // most restrictive form.
    view OperatorConsoleExactView {
        expose GroundSegment::OperatorConsole;
    }

    // `expose <path>::**;` (MembershipRecursive) — exposes the named element and its
    // entire containment subtree, recursively: OperatorConsole plus its nested
    // DisplayPanel and CommsHandset definitions.
    view OperatorConsoleDeepView {
        expose GroundSegment::OperatorConsole::**;
    }

    // `expose <path>::*;` (NamespaceDirectChildren) — exposes only the direct members of
    // the named namespace, one level deep. GroundSegment itself is NOT included; its
    // direct children OperatorConsole and ThermalRegulator are, but OperatorConsole's own
    // nested DisplayPanel and CommsHandset (two levels below GroundSegment) are not, and
    // neither are RadioNetwork's nested Uplink/Downlink (also two levels below).
    view GroundSegmentDirectChildrenView {
        expose GroundSegment::*;
    }

    // `expose <path>::*::**;` (NamespaceRecursive) — exposes every descendant of the
    // named namespace, recursively, but (unlike MembershipRecursive) still excludes the
    // namespace itself. OperatorConsole, DisplayPanel, CommsHandset, ThermalRegulator,
    // Uplink, and Downlink are all in scope; GroundSegment's own folder is not.
    view GroundSegmentRecursiveView {
        expose GroundSegment::*::**;
    }
}
