package ElevatorController {

    // An elevator control state machine.
    state def ElevatorStates {
        state idle;
        accept callReceived if doorsClear do action dimLights then doorsClosing;
        state doorsOpening {
            entry action openDoors;
            do action holdOpen;
            exit action chime;
        }
        state doorsOpen;
        state doorsClosing {
            entry action sounder;
        }
        state movingUp;
        state movingDown;

        first start then idle;
        transition first doorsClosing if doorsClosed then movingUp;
        transition first movingUp if atFloor then doorsOpening;
        transition first movingDown if atFloor then doorsOpening;
        transition first doorsOpening if doorsAreOpen then doorsOpen;
        transition first doorsOpen if timeout then doorsClosing;
        transition first doorsClosing if goingDown then movingDown;
        transition first movingUp if idleTimeout then idle;
        transition first doorsOpen accept shutdown then done;
    }

    view def ElevatorStateTransitionView {}
}
