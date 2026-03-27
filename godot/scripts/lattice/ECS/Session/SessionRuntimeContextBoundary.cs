using System;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 当前运行时上下文正式推荐的归属模型。
    /// </summary>
    public enum SessionRuntimeContextKind
    {
        /// <summary>
        /// 以类型为键挂载显式声明的运行时共享服务。
        /// </summary>
        TypedRuntimeSharedServices = 0
    }

    /// <summary>
    /// 当前运行时上下文正式承诺的能力。
    /// </summary>
    [Flags]
    public enum SessionRuntimeContextCapability
    {
        None = 0,

        /// <summary>
        /// 运行时共享对象按类型注册和读取。
        /// </summary>
        TypedSharedLookup = 1 << 0,

        /// <summary>
        /// 共享对象必须显式声明为 runtime shared service。
        /// </summary>
        ExplicitRuntimeSharedServiceRegistration = 1 << 1,

        /// <summary>
        /// 共享对象可以由上下文托管释放。
        /// </summary>
        OwnedSharedObjectLifetime = 1 << 2,

        /// <summary>
        /// 上下文稳定暴露运行时元信息。
        /// </summary>
        RuntimeMetadataExposure = 1 << 3
    }

    /// <summary>
    /// 当前运行时上下文明确不承诺的能力。
    /// </summary>
    [Flags]
    public enum UnsupportedSessionRuntimeContextCapability
    {
        None = 0,

        /// <summary>
        /// 不允许把上下文当成任意对象包。
        /// </summary>
        ArbitraryObjectBag = 1 << 0,

        /// <summary>
        /// 不允许把可回滚状态或历史载体塞进上下文。
        /// </summary>
        RollbackStateStorage = 1 << 1,

        /// <summary>
        /// 不允许把 gameplay 状态通信主链路挂到上下文。
        /// </summary>
        GameplayStateExchange = 1 << 2,

        /// <summary>
        /// 不提供 string key / 名字查找式 service locator。
        /// </summary>
        StringKeyedServiceLocator = 1 << 3
    }

    /// <summary>
    /// SessionRuntimeContext 的正式边界描述。
    /// </summary>
    public sealed class SessionRuntimeContextBoundary
    {
        public SessionRuntimeContextBoundary(
            SessionRuntimeContextKind contextKind,
            SessionRuntimeContextCapability supportedCapabilities,
            UnsupportedSessionRuntimeContextCapability unsupportedCapabilities)
        {
            ContextKind = contextKind;
            SupportedCapabilities = supportedCapabilities;
            UnsupportedCapabilities = unsupportedCapabilities;
        }

        /// <summary>
        /// 当前上下文归属模型。
        /// </summary>
        public SessionRuntimeContextKind ContextKind { get; }

        /// <summary>
        /// 当前正式支持的上下文能力。
        /// </summary>
        public SessionRuntimeContextCapability SupportedCapabilities { get; }

        /// <summary>
        /// 当前明确不承诺的上下文能力。
        /// </summary>
        public UnsupportedSessionRuntimeContextCapability UnsupportedCapabilities { get; }

        public bool Supports(SessionRuntimeContextCapability capability)
        {
            return (SupportedCapabilities & capability) == capability;
        }

        public bool ExplicitlyDoesNotSupport(UnsupportedSessionRuntimeContextCapability capability)
        {
            return (UnsupportedCapabilities & capability) == capability;
        }
    }

    /// <summary>
    /// 允许挂入 SessionRuntimeContext 的正式运行时共享服务标记。
    /// 该标记用于把上下文约束在 runtime service 归属面，而不是任意对象包。
    /// </summary>
    public interface ISessionRuntimeSharedService
    {
    }
}
