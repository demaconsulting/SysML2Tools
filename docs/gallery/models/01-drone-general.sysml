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

    // An enum def with literal values, demonstrating the "enum values"
    // compartment populated from AstBuilder's enum-literal capture.
    enum def FlightMode {
        enum idle;
        enum manual;
        enum autonomous;
    }

    // ===== Core part definitions =====
    part def Battery {
        doc /* Rechargeable lithium-polymer battery pack supplying the drone's
             * flight controller, motors, and sensor bus via the PowerPort
             * interface. Demonstrates the BoxShape.Note annotation rendering. */
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

        // A connect relationship between two sibling parts' ports, demonstrating
        // the plain solid line (no end marker) used for `connect`.
        connect controller.power to battery.output;
    }

    // RacingDrone subsets Drone's "motors" feature with its own "frontMotors"
    // usage, demonstrating the dashed hollow-triangle marker used for
    // `subsets`/`:>` feature subsetting (distinct from redefinition).
    part def RacingDrone :> Drone {
        part frontMotors : Motor[2] subsets motors;
    }

    // ===== A requirement =====
    // A subject and a require constraint, demonstrating the «subject» and
    // «require constraint» stereotype-titled compartments.
    requirement def FlightTimeRequirement {
        subject drone : Drone;
        attribute minFlightTimeMinutes : Mass;
        require constraint { drone.totalMass <= 2500 }
    }

    // An allocate relationship from the requirement to the part it is
    // allocated to, demonstrating the dashed open-chevron line with the
    // «allocate» stereotype label.
    allocate FlightTimeRequirement to Battery;

    // A standalone dependency declaration, demonstrating the dashed
    // open-chevron line (no label) used for a general OMG dependency.
    dependency FlightController to Battery;

    view def DroneGeneralView {}

    // A view-scoped rendering example: `expose` narrows the diagram to just
    // the Battery subsystem's containment subtree, instead of the whole
    // workspace. See docs/gallery/README.md section 1b and the user guide's
    // "Expose vs. Render" section for more detail.
    view BatterySubsystemView {
        expose Battery;
    }
}
