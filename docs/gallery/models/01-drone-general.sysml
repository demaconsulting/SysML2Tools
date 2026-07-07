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
    // RacingMotor redefines the inherited maxThrust attribute to demonstrate
    // the redefinition crossbar marker (hollow-triangle-crossbar) rendered
    // for `:>>`/`redefines` relationships in the General View.
    part def RacingMotor :> Motor {
        attribute :>> maxThrust : Mass;
    }
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

    // A view-scoped rendering example: `expose` narrows the diagram to just
    // the Battery subsystem's containment subtree, instead of the whole
    // workspace. See docs/gallery/README.md section 1b and the user guide's
    // "Expose vs. Render" section for more detail.
    view BatterySubsystemView {
        expose Battery;
    }
}
