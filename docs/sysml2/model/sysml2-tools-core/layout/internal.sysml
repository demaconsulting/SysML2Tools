package SysML2ToolsCore {
    private import SoftwareItemMetadata::*;

    part def LayoutInternalSubsystem {
        doc /* Per-view layout strategies plus the shared helpers they use. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/sysml2-tools-core/layout/internal.md");
            verificationFiles = ("docs/verification/sysml2-tools-core/layout/internal.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-core/layout/internal.yaml");
        }

        part generalViewLayoutStrategy : GeneralViewLayoutStrategy;
        part interconnectionViewLayoutStrategy : InterconnectionViewLayoutStrategy;
        part stateTransitionViewLayoutStrategy : StateTransitionViewLayoutStrategy;
        part actionFlowViewLayoutStrategy : ActionFlowViewLayoutStrategy;
        part sequenceViewLayoutStrategy : SequenceViewLayoutStrategy;
        part gridViewLayoutStrategy : GridViewLayoutStrategy;
        part browserViewLayoutStrategy : BrowserViewLayoutStrategy;
        part layoutWarnings : LayoutWarnings;
        part exposeScopeResolver : ExposeScopeResolver;
        part layeredPlacement : LayeredPlacement;
    }
}
