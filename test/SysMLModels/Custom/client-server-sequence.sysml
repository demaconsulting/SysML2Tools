package ClientServerProtocol {

    part def Protocol {
        part client {
            event occurrence sendRequest;
            then event occurrence getResponse;
        }

        part server {
            event occurrence getRequest;
            then event occurrence sendResponse;
        }

        message request from client.sendRequest to server.getRequest;
        message response from server.sendResponse to client.getResponse;
        message acknowledge from client.sendRequest to server.getRequest;
    }

    // "Sequence" in the name selects the sequence layout.
    view def ProtocolSequenceView {}
}
