#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面统一生命周期接口。
    /// 当前阶段先由 C# 页面脚本实现，后续 Lua 控制器也应对齐到同一套生命周期语义。
    /// </summary>
    public interface IUIPageLifecycle
    {
        void OnPageInitialize(UIContext context);

        void OnPageShow(UIContext context);

        void OnPageRefresh(UIContext context);

        void OnPageHide(UIContext context);

        void OnPageDispose(UIContext context);
    }
}