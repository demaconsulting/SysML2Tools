package MotorControllerRig {

    // ===== Port definitions =====
    port def PowerPort;
    port def EncoderPort;
    port def ThermalPort;
    port def StatusPort;
    port def ShaftPort;
    port def MountPort;

    item def Torque;

    // ===== Core part definitions =====
    // Every port declares its direction, so the diagram draws an in, out, or inout arrow in
    // the port square. The controller's status port is declared but wired to nothing, and
    // the motor's mount is a reference port (dashed outline).
    part def Controller {
        out port power : PowerPort;
        in port encoderFeedback : EncoderPort;
        in port thermalSense : ThermalPort;
        out port status : StatusPort;
    }

    // The motor's power input is typed by the conjugate of the controller's power port, so
    // its label reads ~PowerPort.
    part def Motor {
        in port powerIn : ~PowerPort;
        out port encoderOut : EncoderPort;
        out port thermalOut : ThermalPort;
        out port shaft : ShaftPort;
        ref port mount : MountPort;
    }

    part def Load {
        in port shaftIn : ShaftPort;
        inout port base : MountPort;
    }

    // One connector of every kind an interconnection view draws, between the same parts:
    // - powerLink is a connection whose ends declare a role name and a multiplicity
    //   (shown above and below the line end),
    // - encoderLink is a plain connection,
    // - thermalLink is a binding connector (labeled =),
    // - the anonymous flow is a flow connector (filled arrowhead, labeled with its payload),
    // - the anonymous interface is an interface connector (labeled «interface»).
    part def MotorRig {
        part controller : Controller;
        part motor : Motor;
        part load : Load;

        connection powerLink connect [1] supply ::> controller.power to [1] drive ::> motor.powerIn;
        connection encoderLink connect motor.encoderOut to controller.encoderFeedback;
        binding thermalLink bind motor.thermalOut = controller.thermalSense;
        flow of Torque from motor.shaft to load.shaftIn;
        interface connect motor.mount to load.base;
    }

    view def MotorRigInterconnectionView {
        render asInterconnectionDiagram;
    }
}
