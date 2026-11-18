package ElevatorController {

    private import ScalarValues::Boolean;

    // The signals the controller accepts.
    item def CallReceived;
    item def Timeout;
    item def Shutdown;

    // An elevator control state machine; its guards read the Boolean conditions below.
    state def ElevatorStates {
        attribute doorsClear : Boolean;
        attribute doorsClosed : Boolean;
        attribute atFloor : Boolean;
        attribute doorsAreOpen : Boolean;
        attribute goingDown : Boolean;

        state idle;
        accept CallReceived if doorsClear do action dimLights then doorsClosing;
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
        transition first doorsOpen accept Timeout then doorsClosing;
        transition first doorsClosing if goingDown then movingDown;
        transition first doorsOpen accept Shutdown then done;
    }

    view def ElevatorStateTransitionView {}
}
