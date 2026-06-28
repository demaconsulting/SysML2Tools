package ElevatorController {

    // An elevator control state machine.
    state def ElevatorStates {
        state idle;
        state doorsOpening;
        state doorsOpen;
        state doorsClosing;
        state movingUp;
        state movingDown;

        transition first idle if callReceived then doorsClosing;
        transition first doorsClosing if doorsClosed then movingUp;
        transition first movingUp if atFloor then doorsOpening;
        transition first movingDown if atFloor then doorsOpening;
        transition first doorsOpening if doorsAreOpen then doorsOpen;
        transition first doorsOpen if timeout then doorsClosing;
        transition first doorsClosing if goingDown then movingDown;
        transition first movingUp if idleTimeout then idle;
    }

    view def ElevatorStateTransitionView {}
}
