package CiCdPipeline {

    // A continuous-integration / deployment action flow with a quality gate branch.
    action def Pipeline {
        action checkout;
        action restoreDependencies;
        action build;
        action unitTest;
        action integrationTest;
        action securityScan;
        action qualityGate;
        action deployStaging;
        action smokeTest;
        action deployProduction;
        action rollback;
        action notifySuccess;
        action notifyFailure;

        first checkout then restoreDependencies;
        first restoreDependencies then build;
        first build then unitTest;
        first build then securityScan;
        first unitTest then integrationTest;
        first integrationTest then qualityGate;
        first securityScan then qualityGate;
        first qualityGate then deployStaging;
        first deployStaging then smokeTest;
        first smokeTest then deployProduction;
        first smokeTest then rollback;
        first deployProduction then notifySuccess;
        first rollback then notifyFailure;
    }

    view def PipelineActionFlowView {}
}
