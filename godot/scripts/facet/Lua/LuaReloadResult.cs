#nullable enable

using System;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Lua 页面控制器热重载结果。
    /// </summary>
    public sealed class LuaReloadResult
    {
        public LuaReloadResult(
            bool reloaded,
            string pageId,
            string scriptId,
            string? oldVersionToken,
            string? newVersionToken,
            string reason,
            string pageState,
            string? errorMessage = null)
        {
            ArgumentNullException.ThrowIfNull(pageId);
            ArgumentNullException.ThrowIfNull(scriptId);
            ArgumentNullException.ThrowIfNull(reason);
            ArgumentNullException.ThrowIfNull(pageState);

            Reloaded = reloaded;
            PageId = pageId;
            ScriptId = scriptId;
            OldVersionToken = oldVersionToken;
            NewVersionToken = newVersionToken;
            Reason = reason;
            PageState = pageState;
            ErrorMessage = errorMessage;
        }

        public bool Reloaded { get; }

        public string PageId { get; }

        public string ScriptId { get; }

        public string? OldVersionToken { get; }

        public string? NewVersionToken { get; }

        public string Reason { get; }

        public string PageState { get; }

        public string? ErrorMessage { get; }
    }
}