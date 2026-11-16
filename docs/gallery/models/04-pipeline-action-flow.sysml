package CiCdPipeline {

    // The build step the pipeline runs, so an action can be typed by it.
    action def BuildStep;

    // A continuous-integration / deployment action flow with a quality gate branch.
    action def Pipeline {
        first start then checkout;

        action checkout;
        then restoreDependencies;

        action restoreDependencies;
        action build : BuildStep;
        first restoreDependencies then build;
        first build then buildFork;

        fork buildFork;
        then unitTest;
        then securityScan;

        action unitTest;
        then integrationTest;

        action integrationTest;
        then testJoin;

        action securityScan;
        then testJoin;

        join testJoin;
        then qualityGate;

        action qualityGate;
        then deployStaging;

        action deployStaging;
        then awaitApproval;

        action awaitApproval accept ApprovalSignal;
        then smokeTest;

        action smokeTest;
        then decide smokeDecision;
        if smokePassed then deployRelease;
        else revertRelease;

        action deployRelease;
        then notifySuccess;

        action notifySuccess send releaseNote to team;
        then done;

        action revertRelease;
        then action abort terminate;
    }

    view def PipelineActionFlowView {}
}
