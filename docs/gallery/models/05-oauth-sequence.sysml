package OAuthLogin {

    // The participants and the payloads that travel between them.
    part def User;
    part def Browser;
    part def AuthServer;
    part def ResourceServer;
    part def AuditLog;

    item def Credentials;
    item def AuthorizationCode;
    item def AccessToken;

    // An OAuth 2.0 authorization-code login flow across four participants. The audit log is declared
    // but exchanges no message, so it still has a lifeline. The resource server is a reference part
    // (it lives outside the flow), which draws its lifeline header dashed.
    part def AuthorizationFlow {
        part user : User {
            event occurrence clickLogin;
            then event occurrence enterCredentials;
            then event occurrence seeApp;
        }
        part browser : Browser {
            event occurrence requestPage;
            then event occurrence redirectToAuth;
            then event occurrence sendCode;
            then event occurrence loadApp;
        }
        part authServer : AuthServer {
            event occurrence showLogin;
            then event occurrence issueCode;
            then event occurrence issueToken;
        }
        ref part resourceServer : ResourceServer {
            event occurrence validateToken;
            then event occurrence returnData;
        }
        part auditLog : AuditLog;

        // The messages are declared out of order on purpose: the diagram orders them by the events
        // above, so accessToken is drawn after exchangeCode although it is declared before it.
        message openApp from user.clickLogin to browser.requestPage;
        message redirect from browser.redirectToAuth to authServer.showLogin;
        message promptCredentials from authServer.showLogin to user.enterCredentials;
        message submitCredentials of Credentials from user.enterCredentials to authServer.issueCode;
        message authCode of AuthorizationCode from authServer.issueCode to browser.sendCode;
        message accessToken of AccessToken from authServer.issueToken to browser.loadApp;
        message exchangeCode of AuthorizationCode from browser.sendCode to authServer.issueToken;
        message fetchResource from browser.loadApp to resourceServer.validateToken;
        message resourceData from resourceServer.returnData to user.seeApp;
    }

    view def OAuthSequenceView {}

    // View-scoped rendering: exposing browser and authServer narrows the sequence to the
    // token-exchange leg of the flow, dropping every message with an endpoint on user or
    // resourceServer (openApp, promptCredentials, submitCredentials, fetchResource,
    // resourceData) while keeping the four messages exchanged between the two lifelines.
    view TokenExchangeSequenceView {
        expose AuthorizationFlow::browser;
        expose AuthorizationFlow::authServer;
    }
}
