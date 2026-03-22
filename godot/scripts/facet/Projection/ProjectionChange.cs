#nullable enable

using System;

namespace Sideline.Facet.Projection
{
    /// <summary>
    /// Projection 变更快照。
    /// 用于把一次写入或删除事件完整地传递给订阅方。
    /// </summary>
    public sealed class ProjectionChange
    {
        /// <summary>
        /// 创建一条 Projection 变更记录。
        /// </summary>
        public ProjectionChange(
            ProjectionKey key,
            ProjectionChangeKind kind,
            object? previousValue,
            object? currentValue,
            string? source,
            DateTimeOffset occurredAtUtc)
        {
            Key = key;
            Kind = kind;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            Source = source;
            OccurredAtUtc = occurredAtUtc;
        }

        /// <summary>
        /// 本次变更对应的 Projection 主键。
        /// </summary>
        public ProjectionKey Key { get; }

        /// <summary>
        /// 本次变更的类型。
        /// </summary>
        public ProjectionChangeKind Kind { get; }

        /// <summary>
        /// 变更前的旧值。
        /// 对于首次写入通常为空。
        /// </summary>
        public object? PreviousValue { get; }

        /// <summary>
        /// 变更后的新值。
        /// 对于删除事件通常为空。
        /// </summary>
        public object? CurrentValue { get; }

        /// <summary>
        /// 触发本次变更的来源说明。
        /// 一般用于日志排查与链路诊断。
        /// </summary>
        public string? Source { get; }

        /// <summary>
        /// 变更发生时间（UTC）。
        /// </summary>
        public DateTimeOffset OccurredAtUtc { get; }
    }
}
