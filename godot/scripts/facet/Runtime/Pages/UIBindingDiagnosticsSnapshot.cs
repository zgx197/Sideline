#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 绑定作用域诊断快照。
    /// 供页面生命周期日志、调试面板和后续 Facet 工具页统一消费。
    /// </summary>
    public sealed class UIBindingDiagnosticsSnapshot
    {
        public UIBindingDiagnosticsSnapshot(
            string scopeId,
            int bindingCount,
            int refreshCount,
            string? lastRefreshReason,
            IReadOnlyList<UIBindingDescriptor> bindings)
        {
            ScopeId = scopeId;
            BindingCount = bindingCount;
            RefreshCount = refreshCount;
            LastRefreshReason = lastRefreshReason;
            Bindings = bindings;
        }

        public string ScopeId { get; }

        public int BindingCount { get; }

        public int RefreshCount { get; }

        public string? LastRefreshReason { get; }

        public IReadOnlyList<UIBindingDescriptor> Bindings { get; }
    }
}
