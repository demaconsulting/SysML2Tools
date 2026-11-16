package Heater {

    // A small state machine that shows the state notation: entry / do / exit compartments,
    // "trigger [guard] / effect" transition labels, an initial state and a final state.
    state def HeaterStates {
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
        transition first standby accept heat if tempLow do action beep then heating;
        transition first heating accept tempReached then cooling;
        transition first cooling accept cooled then standby;
        transition first cooling accept unplug then done;
    }

    view def HeaterStateView {}
}
