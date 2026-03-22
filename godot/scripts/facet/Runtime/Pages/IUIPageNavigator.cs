#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 页面导航器抽象。
    /// 为 Lua 控制器等运行时能力提供受限路由入口，而不是直接暴露完整 UIManager。
    /// </summary>
    public interface IUIPageNavigator
    {
        string? CurrentPageId { get; }

        bool CanGoBack { get; }

        int BackStackDepth { get; }

        void Open(string pageId, IReadOnlyDictionary<string, object?>? arguments = null, bool pushHistory = true);

        bool GoBack();
    }
}