#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面 Binding 作用域抽象。
    /// 负责承接节点与数据、节点与命令之间的绑定注册，并提供统一刷新入口。
    /// </summary>
    public interface IUIBindingScope : IDisposable
    {
        /// <summary>
        /// 当前绑定作用域标识。
        /// </summary>
        string ScopeId { get; }

        /// <summary>
        /// 当前已注册绑定数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 当前已执行刷新次数。
        /// </summary>
        int RefreshCount { get; }

        /// <summary>
        /// 注册文本绑定。
        /// </summary>
        void BindText(string key, Func<string?> valueFactory);

        /// <summary>
        /// 注册显隐绑定。
        /// </summary>
        void BindVisibility(string key, Func<bool> valueFactory);

        /// <summary>
        /// 注册可交互状态绑定。
        /// </summary>
        void BindInteractable(string key, Func<bool> valueFactory);

        /// <summary>
        /// 注册按钮命令绑定。
        /// </summary>
        void BindCommand(string key, Action handler);

        /// <summary>
        /// 创建组件级 Binding 作用域。
        /// 用于把页面中的局部区域拆成可复用、可独立诊断的子作用域。
        /// </summary>
        IUIComponentBindingScope CreateComponentScope(string componentId, IUIObjectResolver resolver);

        /// <summary>
        /// 注册简单列表绑定。
        /// </summary>
        void BindList(string key, Func<IReadOnlyList<string>> itemsFactory, string separator = "\n", string? emptyText = null);

        /// <summary>
        /// 注册复杂列表绑定。
        /// 通过容器节点与模板节点生成真实子项，并为每个子项建立独立组件级 Binding 作用域。
        /// </summary>
        IUIComplexListBinding<TItem> BindComplexList<TItem>(
            string containerKey,
            string templateKey,
            Func<IReadOnlyList<TItem>> itemsFactory,
            IUIComplexListAdapter<TItem> adapter,
            string? emptyStateKey = null);

        /// <summary>
        /// 刷新当前作用域中的全部绑定。
        /// 可附带原因字段，便于输出更清晰的诊断日志。
        /// </summary>
        void RefreshAll(string? reason = null);

        /// <summary>
        /// 获取当前绑定作用域的诊断快照。
        /// </summary>
        UIBindingDiagnosticsSnapshot GetDiagnosticsSnapshot();

        /// <summary>
        /// 清空当前作用域中的全部绑定。
        /// </summary>
        void Clear();
    }
}