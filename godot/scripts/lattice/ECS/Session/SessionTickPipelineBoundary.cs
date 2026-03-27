using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 当前 Session 的 Tick 管线模型。
    /// </summary>
    public enum SessionTickPipelineKind
    {
        /// <summary>
        /// 显式阶段驱动，结构性修改在 `Simulation` 后统一提交。
        /// </summary>
        PhasedStructuralCommit = 0
    }

    /// <summary>
    /// 当前 Tick 管线正式承诺的能力。
    /// </summary>
    [Flags]
    public enum SessionTickPipelineCapability
    {
        None = 0,

        /// <summary>
        /// 公开当前 Tick 阶段。
        /// </summary>
        ExplicitStageExposure = 1 << 0,

        /// <summary>
        /// 支持在 `Simulation` 阶段延迟结构性修改。
        /// </summary>
        DeferredStructuralChanges = 1 << 1,

        /// <summary>
        /// 结构性修改在 `StructuralCommit` 阶段统一生效。
        /// </summary>
        StructuralCommitStage = 1 << 2,

        /// <summary>
        /// 支持显式的 `Cleanup` 与 `HistoryCapture` 阶段。
        /// </summary>
        CleanupAndHistoryStages = 1 << 3,

        /// <summary>
        /// 允许在 `Simulation` 阶段直接修改已存在组件的数据字段。
        /// </summary>
        ImmediateComponentMutationDuringSimulation = 1 << 4
    }

    /// <summary>
    /// 当前 Tick 管线明确不承诺的能力。
    /// </summary>
    [Flags]
    public enum UnsupportedSessionTickPipelineCapability
    {
        None = 0,

        /// <summary>
        /// 不承诺结构性修改在 `Simulation` 阶段立即可见。
        /// </summary>
        ImmediateStructuralVisibilityDuringSimulation = 1 << 0,

        /// <summary>
        /// 不承诺运行期任意重排 Tick 阶段。
        /// </summary>
        RuntimeReorderedStages = 1 << 1,

        /// <summary>
        /// 不承诺每个系统拥有独立的结构提交阶段。
        /// </summary>
        PerSystemStructuralCommit = 1 << 2
    }

    /// <summary>
    /// 当前 Tick 正在执行的正式阶段。
    /// </summary>
    public enum SessionTickStage
    {
        Idle = 0,
        InputApply = 1,
        Simulation = 2,
        StructuralCommit = 3,
        Cleanup = 4,
        HistoryCapture = 5
    }

    /// <summary>
    /// Tick 管线边界描述。
    /// 用于明确当前主干对 Tick 阶段和结构性修改可见性的正式承诺。
    /// </summary>
    public sealed class SessionTickPipelineBoundary
    {
        public SessionTickPipelineBoundary(
            SessionTickPipelineKind pipelineKind,
            SessionTickPipelineCapability supportedCapabilities,
            UnsupportedSessionTickPipelineCapability unsupportedCapabilities)
        {
            PipelineKind = pipelineKind;
            SupportedCapabilities = supportedCapabilities;
            UnsupportedCapabilities = unsupportedCapabilities;
        }

        /// <summary>
        /// 当前 Tick 管线模型。
        /// </summary>
        public SessionTickPipelineKind PipelineKind { get; }

        /// <summary>
        /// 当前正式支持的 Tick 管线能力。
        /// </summary>
        public SessionTickPipelineCapability SupportedCapabilities { get; }

        /// <summary>
        /// 当前明确不承诺的 Tick 管线能力。
        /// </summary>
        public UnsupportedSessionTickPipelineCapability UnsupportedCapabilities { get; }

        public bool Supports(SessionTickPipelineCapability capability)
        {
            return (SupportedCapabilities & capability) == capability;
        }

        public bool ExplicitlyDoesNotSupport(UnsupportedSessionTickPipelineCapability capability)
        {
            return (UnsupportedCapabilities & capability) == capability;
        }
    }
}
