#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Facet 内置 Lua 脚本标识。
    /// 当前直接使用 Godot 资源路径作为脚本标识，便于真实脚本执行与热重载检测。
    /// </summary>
    public static class FacetLuaScriptIds
    {
        public const string IdleRuntimeController = "res://scripts/facet/LuaScripts/idle_runtime.lua";

        public const string DungeonRuntimeController = "res://scripts/facet/LuaScripts/dungeon_runtime.lua";

        public static IReadOnlyCollection<string> All { get; } = new[]
        {
            IdleRuntimeController,
            DungeonRuntimeController,
        };
    }
}