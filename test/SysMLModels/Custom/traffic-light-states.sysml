package TrafficControl {

    // A three-state traffic-light cycle with guarded transitions.
    state def TrafficLight {
        state stop;
        state go;
        state caution;

        transition first stop if timerExpired then go;
        transition first go if timerExpired then caution;
        transition first caution if timerExpired then stop;
    }

    // The view name contains "StateTransition" to select the state-transition layout.
    view def TrafficStateTransitionView {}
}
