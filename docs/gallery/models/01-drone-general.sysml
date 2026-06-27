package QuadcopterDrone {

    // ===== Interface and port definitions =====
    interface def PowerBus;
    interface def DataBus;
    port def PowerPort;
    port def TelemetryPort;
    port def MotorControlPort;
    port def SensorPort;

    // ===== Attribute and enumeration definitions =====
    attribute def Mass;
    attribute def Voltage;
    enum def FlightMode;

    // ===== Core part definitions =====
    part def Battery {
        attribute capacity : Voltage;
        port output : PowerPort;
    }

    part def FlightController {
        attribute mode : FlightMode;
        port power : PowerPort;
        port telemetry : TelemetryPort;
        port motors : MotorControlPort;
        port sensors : SensorPort;
    }

    part def Motor {
        attribute maxThrust : Mass;
        port control : MotorControlPort;
    }

    part def Propeller;

    part def ImuSensor {
        port data : SensorPort;
    }

    part def GpsSensor {
        port data : SensorPort;
    }

    part def Frame;

    // ===== Specializations =====
    part def RacingMotor :> Motor;
    part def EnduranceBattery :> Battery;

    // ===== The integrated drone =====
    part def Drone {
        attribute totalMass : Mass;
        part airframe : Frame;
        part battery : Battery;
        part controller : FlightController;
        part motors : Motor[4];
        part propellers : Propeller[4];
        part imu : ImuSensor;
        part gps : GpsSensor;
    }

    // ===== A requirement =====
    requirement def FlightTimeRequirement;

    view def DroneGeneralView {}
}
