package MotorControllerRig {

    // ===== Port definitions =====
    port def PowerPort;
    port def EncoderPort;
    port def ThermalPort;

    // ===== Core part definitions =====
    part def Controller {
        port power : PowerPort;
        port encoderFeedback : EncoderPort;
        port thermalSense : ThermalPort;
    }

    part def Motor {
        port powerIn : PowerPort;
        port encoderOut : EncoderPort;
        port thermalOut : ThermalPort;
    }

    // Three independent, named-port connections between the same two parts: a motor
    // is wired to its controller by a power feed, an encoder feedback line, and a
    // thermal sense line, each its own distinct connection rather than a single bundled
    // link. This demonstrates that the interconnection layout keeps every parallel
    // connector between the same two boxes visually and structurally distinct, and
    // labels each end with its own SysML port name rather than merging them into one
    // shared route.
    part def MotorRig {
        part controller : Controller;
        part motor : Motor;

        connection powerLink connect controller.power to motor.powerIn;
        connection encoderLink connect motor.encoderOut to controller.encoderFeedback;
        connection thermalLink connect motor.thermalOut to controller.thermalSense;
    }

    view def MotorRigInterconnectionView {
        render asInterconnectionDiagram;
    }
}
