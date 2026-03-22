#nullable enable

using System;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Lua 控制器运行时句柄。
    /// 负责把脚本标识、版本、来源路径、控制器实例和桥接对象绑定在一起。
    /// </summary>
    public sealed class LuaControllerHandle
    {
        public LuaControllerHandle(
            string scriptId,
            string sourcePath,
            string versionToken,
            ILuaPageController controller,
            LuaApiBridge api)
        {
            ArgumentNullException.ThrowIfNull(scriptId);
            ArgumentNullException.ThrowIfNull(sourcePath);
            ArgumentNullException.ThrowIfNull(versionToken);
            ArgumentNullException.ThrowIfNull(controller);
            ArgumentNullException.ThrowIfNull(api);

            ScriptId = scriptId;
            SourcePath = sourcePath;
            VersionToken = versionToken;
            Controller = controller;
            Api = api;
        }

        public string ScriptId { get; }

        public string SourcePath { get; }

        public string VersionToken { get; }

        public ILuaPageController Controller { get; }

        public LuaApiBridge Api { get; }

        public void OnInit()
        {
            Controller.OnInit(Api);
        }

        public void OnShow()
        {
            Controller.OnShow(Api);
        }

        public void OnRefresh()
        {
            Controller.OnRefresh(Api);
        }

        public void OnHide()
        {
            Controller.OnHide(Api);
        }

        public void OnDispose()
        {
            Controller.OnDispose(Api);
        }
    }
}