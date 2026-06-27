package OAuthLogin {

    // An OAuth 2.0 authorization-code login flow across four participants.
    part def AuthorizationFlow {
        part user {
            event occurrence clickLogin;
            then event occurrence enterCredentials;
            then event occurrence seeApp;
        }
        part browser {
            event occurrence requestPage;
            then event occurrence redirectToAuth;
            then event occurrence sendCode;
            then event occurrence loadApp;
        }
        part authServer {
            event occurrence showLogin;
            then event occurrence issueCode;
            then event occurrence issueToken;
        }
        part resourceServer {
            event occurrence validateToken;
            then event occurrence returnData;
        }

        message openApp from user.clickLogin to browser.requestPage;
        message redirect from browser.redirectToAuth to authServer.showLogin;
        message promptCredentials from authServer.showLogin to user.enterCredentials;
        message submitCredentials from user.enterCredentials to authServer.issueCode;
        message authCode from authServer.issueCode to browser.sendCode;
        message exchangeCode from browser.sendCode to authServer.issueToken;
        message accessToken from authServer.issueToken to browser.loadApp;
        message fetchResource from browser.loadApp to resourceServer.validateToken;
        message resourceData from resourceServer.returnData to user.seeApp;
    }

    view def OAuthSequenceView {}
}
