package SysML2ToolsCore_LayoutInternalSubsystem {
    public import SysML2ToolsCore_ActionFlowViewLayoutStrategy::*;
    public import SysML2ToolsCore_BrowserViewLayoutStrategy::*;
    public import SysML2ToolsCore_ExposeScopeResolver::*;
    public import SysML2ToolsCore_GeneralViewLayoutStrategy::*;
    public import SysML2ToolsCore_GridViewLayoutStrategy::*;
    public import SysML2ToolsCore_InterconnectionViewLayoutStrategy::*;
    public import SysML2ToolsCore_LayeredPlacement::*;
    public import SysML2ToolsCore_LayoutWarnings::*;
    public import SysML2ToolsCore_SequenceViewLayoutStrategy::*;
    public import SysML2ToolsCore_StateTransitionViewLayoutStrategy::*;
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
