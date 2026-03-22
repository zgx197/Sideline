#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 组件级 Binding 作用域接口。
    /// 后续用于把页面中的复合子区域拆成可复用的绑定单元，而不是让整页共享一个巨大作用域。
    /// </summary>
    public interface IUIComponentBindingScope : IUIBindingScope
    {
        /// <summary>
        /// 当前组件逻辑标识。
        /// </summary>
        string ComponentId { get; }

        /// <summary>
        /// 父级 Binding 作用域标识。
        /// </summary>
        string ParentScopeId { get; }
    }
}
