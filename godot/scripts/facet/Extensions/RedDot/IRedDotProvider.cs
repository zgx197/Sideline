#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Extensions.RedDot
{
    /// <summary>
    /// 红点数据提供者。
    /// 负责把某一类业务或运行时状态转换成红点路径条目，并在数据变化时通知 RedDotService 重建聚合结果。
    /// </summary>
    public interface IRedDotProvider : IDisposable
    {
        /// <summary>
        /// 提供者唯一标识。
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// 当提供者输出的红点条目发生变化时触发。
        /// </summary>
        event Action? Changed;

        /// <summary>
        /// 获取当前提供者输出的全部红点条目。
        /// </summary>
        IReadOnlyList<RedDotStateEntry> GetEntries();
    }
}
