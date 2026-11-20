package SysML2ToolsCore_LayoutInternalSubsystem {
    private import SysML2ToolsCore_ActionFlowViewLayoutStrategy::*;
    private import SysML2ToolsCore_BrowserViewLayoutStrategy::*;
    private import SysML2ToolsCore_ExposeScopeResolver::*;
    private import SysML2ToolsCore_GeneralViewLayoutStrategy::*;
    private import SysML2ToolsCore_GridViewLayoutStrategy::*;
    private import SysML2ToolsCore_InterconnectionViewLayoutStrategy::*;
    private import SysML2ToolsCore_LayeredPlacement::*;
    private import SysML2ToolsCore_LayoutWarnings::*;
    private import SysML2ToolsCore_SequenceViewLayoutStrategy::*;
    private import SysML2ToolsCore_StateTransitionViewLayoutStrategy::*;
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
