package AvionicsSystem {

    package Navigation {
        part def InertialUnit;
        part def GpsReceiver;
        part def AirDataComputer;
        package Sensors {
            part def Accelerometer;
            part def Gyroscope;
            part def Magnetometer;
        }
    }

    package FlightControls {
        part def AutopilotComputer;
        part def Actuator;
        port def ControlPort;
        package Surfaces {
            part def Aileron;
            part def Elevator;
            part def Rudder;
        }
    }

    package Communications {
        part def RadioTransceiver;
        part def Transponder;
        interface def DataLink;
    }

    package Displays {
        part def PrimaryFlightDisplay;
        part def NavigationDisplay;
    }

    view def AvionicsBrowserView {}
}
