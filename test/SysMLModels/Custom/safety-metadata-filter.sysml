package RobotArm {
    private import ScalarValues::Boolean;

    metadata def Safety {
        attribute isMandatory : Boolean;
    }

    metadata def Certified {
    }

    part def Actuator {
        @Safety {
            isMandatory = true;
        }
    }

    part def Gripper {
        @Safety {
            isMandatory = false;
        }
        @Certified;
    }

    part def Bracket;

    // Renders only the definitions carrying the Safety metadata annotation.
    view def SafetyPartsView {
        filter @Safety;
    }

    // Renders only the definitions whose Safety annotation is mandatory.
    view def MandatorySafetyPartsView {
        filter @Safety and (as Safety).isMandatory;
    }

    // Phase 2a: exercises bracket-form `expose <path>::**[<expr>]` scope narrowing end to end —
    // narrows to only the mandatory-safety definitions within RobotArm's containment subtree.
    view BracketExposeMandatorySafetyView {
        expose RobotArm::**[@Safety and (as Safety).isMandatory];
    }
}
