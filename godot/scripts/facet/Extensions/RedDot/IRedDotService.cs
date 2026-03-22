#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点树运行时服务接口。
    /// 负责聚合多来源红点条目，并向页面与 Lua 宿主提供受限查询和订阅能力。
    /// </summary>
    public interface IRedDotService
    {
        /// <summary>
        /// 当前是否已有可用红点提供者。
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 当前聚合树中已注册的路径数量。
        /// </summary>
        int RegisteredPathCount { get; }

        /// <summary>
        /// 注册新的红点提供者。
        /// </summary>
        void RegisterProvider(IRedDotProvider provider);

        /// <summary>
        /// 尝试读取指定路径的聚合红点状态。
        /// </summary>
        bool TryGetState(string path, out bool hasRedDot);

        /// <summary>
        /// 获取指定路径的聚合红点状态；若路径不存在则返回回退值。
        /// </summary>
        bool GetStateOrDefault(string path, bool fallback = false);

        /// <summary>
        /// 尝试读取指定路径的完整快照。
        /// </summary>
        bool TryGetSnapshot(string path, out RedDotNodeSnapshot snapshot);

        /// <summary>
        /// 获取当前全部已注册路径。
        /// </summary>
        IReadOnlyList<string> GetRegisteredPaths();

        /// <summary>
        /// 订阅指定路径的红点变更。
        /// 只有该路径自身快照发生变化时才会触发回调。
        /// </summary>
        IDisposable Subscribe(string path, Action<RedDotChange> listener);
    }
}
