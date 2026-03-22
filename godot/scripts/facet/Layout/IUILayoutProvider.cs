#nullable enable

using Godot;
using Sideline.Facet.Runtime;
using Sideline.Facet.UI;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// 统一布局提供者接口。
    /// 用于把不同布局来源转换成 Runtime 可消费的统一结果。
    /// </summary>
    public interface IUILayoutProvider
    {
        /// <summary>
        /// 当前提供者名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 判断是否支持当前页面定义。
        /// </summary>
        bool CanLoad(UIPageDefinition definition);

        /// <summary>
        /// 加载页面布局，并返回根节点与节点注册结果。
        /// </summary>
        UILayoutResult Load(UIPageDefinition definition, Node mountRoot);
    }
}