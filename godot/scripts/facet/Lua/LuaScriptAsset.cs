#nullable enable

using System;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Lua 脚本快照。
    /// 用于把脚本标识、实际来源路径、源码文本和版本标记统一交给宿主处理。
    /// </summary>
    public sealed class LuaScriptAsset
    {
        public LuaScriptAsset(string scriptId, string sourcePath, string sourceCode, string versionToken)
        {
            ArgumentNullException.ThrowIfNull(scriptId);
            ArgumentNullException.ThrowIfNull(sourcePath);
            ArgumentNullException.ThrowIfNull(sourceCode);
            ArgumentNullException.ThrowIfNull(versionToken);

            ScriptId = scriptId;
            SourcePath = sourcePath;
            SourceCode = sourceCode;
            VersionToken = versionToken;
        }

        public string ScriptId { get; }

        public string SourcePath { get; }

        public string SourceCode { get; }

        public string VersionToken { get; }
    }
}