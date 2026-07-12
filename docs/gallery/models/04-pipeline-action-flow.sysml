package CiCdPipeline {

    // A continuous-integration / deployment action flow with a quality gate branch.
    action def Pipeline {
        action checkout;
        then restoreDependencies;

        action restoreDependencies;
        action build;
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
        action deployStaging;
        action smokeTest;
        action deployProduction;
        action rollback;
        action notifySuccess;
        action notifyFailure;

        first qualityGate then deployStaging;
        first deployStaging then smokeTest;
        first smokeTest then deployProduction;
        first smokeTest then rollback;
        first deployProduction then notifySuccess;
        first rollback then notifyFailure;
    }

    view def PipelineActionFlowView {}
}
