#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面对象解析器抽象。
    /// 阶段 6 先用来承接 Godot 节点注册表，后续也可扩展到模板生成控件或其他视图对象。
    /// </summary>
    public interface IUIObjectResolver
    {
        /// <summary>
        /// 获取当前已注册的全部对象键。
        /// </summary>
        IReadOnlyCollection<string> GetRegisteredKeys();

        /// <summary>
        /// 尝试按键解析对象。
        /// </summary>
        bool TryResolve(string key, out object? value);

        /// <summary>
        /// 按键获取必须存在的对象。
        /// </summary>
        object GetRequired(string key);

        /// <summary>
        /// 基于指定子树根键创建局部解析器。
        /// </summary>
        IUIObjectResolver CreateSubtreeResolver(string rootKey);
    }
}
