package RobotArm {

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
}
