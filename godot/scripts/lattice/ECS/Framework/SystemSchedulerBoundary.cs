using System;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 当前 `SystemScheduler` 的正式调度模型类型。
    /// </summary>
    public enum SystemSchedulerKind
    {
        /// <summary>
        /// 单线程、平面、按注册顺序执行的最小系统调度模型。
        /// </summary>
        FlatOrdered = 0,

        /// <summary>
        /// 单线程、平面、按 phase 再按注册顺序执行的轻量系统调度模型。
        /// </summary>
        FlatPhasedOrdered = 1
    }

    /// <summary>
    /// 当前 `SystemScheduler` 公开承诺的调度能力。
    /// </summary>
    [Flags]
    public enum SystemSchedulerCapability
    {
        None = 0,

        /// <summary>
        /// 支持按注册顺序稳定执行系统。
        /// </summary>
        OrderedExecution = 1 << 0,

        /// <summary>
        /// 支持显式初始化与关闭生命周期。
        /// </summary>
        ExplicitLifecycle = 1 << 1,

        /// <summary>
        /// 支持在初始化前静态注册系统集合。
        /// </summary>
        StaticRegistrationBeforeInitialize = 1 << 2,

        /// <summary>
        /// 支持轻量 phase 执行语义。
        /// </summary>
        PhasedExecution = 1 << 3,

        /// <summary>
        /// 支持对系统作者契约进行显式校验与执行期守卫。
        /// </summary>
        AuthoringContractValidation = 1 << 4
    }

    /// <summary>
    /// 当前 `SystemScheduler` 明确不承诺的调度能力。
    /// </summary>
    [Flags]
    public enum UnsupportedSystemSchedulerCapability
    {
        None = 0,

        /// <summary>
        /// 不支持初始化后的动态系统增删。
        /// </summary>
        RuntimeMutation = 1 << 0,

        /// <summary>
        /// 不支持 phase / stage 调度。
        /// 该标记保留用于描述旧边界或其他不支持 phase 的调度器。
        /// </summary>
        SystemPhase = 1 << 1,

        /// <summary>
        /// 不支持系统 enable / disable 开关。
        /// </summary>
        EnableDisable = 1 << 2,

        /// <summary>
        /// 不支持显式依赖排序图。
        /// </summary>
        DependencyOrdering = 1 << 3,

        /// <summary>
        /// 不支持 `SystemGroup` 一类的层级分组。
        /// </summary>
        HierarchicalGrouping = 1 << 4,

        /// <summary>
        /// 不支持多线程系统调度。
        /// </summary>
        ThreadedExecution = 1 << 5
    }

    /// <summary>
    /// `SystemScheduler` 的正式调度边界描述。
    /// 用于把“当前系统层正式支持什么、不支持什么”显式暴露在代码层。
    /// </summary>
    public sealed class SystemSchedulerBoundary
    {
        public SystemSchedulerBoundary(
            SystemSchedulerKind schedulerKind,
            SystemSchedulerCapability supportedCapabilities,
            UnsupportedSystemSchedulerCapability unsupportedCapabilities)
        {
            SchedulerKind = schedulerKind;
            SupportedCapabilities = supportedCapabilities;
            UnsupportedCapabilities = unsupportedCapabilities;
        }

        /// <summary>
        /// 当前调度模型类型。
        /// </summary>
        public SystemSchedulerKind SchedulerKind { get; }

        /// <summary>
        /// 当前正式支持的调度能力。
        /// </summary>
        public SystemSchedulerCapability SupportedCapabilities { get; }

        /// <summary>
        /// 当前明确不承诺的调度能力。
        /// </summary>
        public UnsupportedSystemSchedulerCapability UnsupportedCapabilities { get; }

        public bool Supports(SystemSchedulerCapability capability)
        {
            return (SupportedCapabilities & capability) == capability;
        }

        public bool ExplicitlyDoesNotSupport(UnsupportedSystemSchedulerCapability capability)
        {
            return (UnsupportedCapabilities & capability) == capability;
        }
    }
}
