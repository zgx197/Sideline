#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 复杂列表项绑定适配器。
    /// 后续复杂列表不再只把条目拼成纯文本，而是为每个条目分配独立子作用域并执行项级绑定。
    /// </summary>
    public interface IUIComplexListAdapter<TItem>
    {
        /// <summary>
        /// 获取列表项的稳定键。
        /// 用于后续做增量复用、排序调整和最小重建。
        /// </summary>
        string GetItemKey(TItem item, int index);

        /// <summary>
        /// 当列表项首次创建或需要重绑时执行项级绑定。
        /// </summary>
        void BindItem(IUIComponentBindingScope itemScope, TItem item, int index);
    }

    /// <summary>
    /// 复杂列表 Binding 句柄。
    /// 第一阶段先定义接口边界，后续再接入真实的节点模板与增量刷新实现。
    /// </summary>
    public interface IUIComplexListBinding<TItem> : IDisposable
    {
        /// <summary>
        /// 当前列表绑定的目标键。
        /// </summary>
        string Key { get; }

        /// <summary>
        /// 当前已渲染条目数量。
        /// </summary>
        int ItemCount { get; }

        /// <summary>
        /// 当前列表绑定的稳定条目键集合。
        /// </summary>
        IReadOnlyList<string> ItemKeys { get; }

        /// <summary>
        /// 刷新列表绑定。
        /// </summary>
        void Refresh(string? reason = null);
    }
}
