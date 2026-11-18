package Heater {

    private import ScalarValues::Boolean;

    // The signals the heater accepts.
    item def Heat;
    item def TempReached;
    item def Cooled;
    item def Unplug;

    // A small state machine that shows the state notation: entry / do / exit compartments,
    // "trigger [guard] / effect" transition labels, an initial state and a final state.
    state def HeaterStates {
        attribute tempLow : Boolean;

        state standby {
            entry action fanOff;
        }
        state heating {
            entry action startFan;
            do action regulate;
            exit action stopFan;
        }

        state cooling;

        first start then standby;
        transition first standby accept Heat if tempLow do action beep then heating;
        transition first heating accept TempReached then cooling;
        transition first cooling accept Cooled then standby;
        transition first cooling accept Unplug then done;
    }

    view def HeaterStateView {}
}
