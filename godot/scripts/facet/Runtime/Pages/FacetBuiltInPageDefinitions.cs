#nullable enable

using System.Collections.Generic;
using Sideline.Facet.Lua;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 当前项目内置的页面定义集合。
    /// 阶段 4 先用 C# 对象描述页面元数据，后续可替换为 JSON 或其他外部来源。
    /// </summary>
    public static class FacetBuiltInPageDefinitions
    {
        public static IReadOnlyList<UIPageDefinition> CreateMainSceneDefinitions()
        {
            return new[]
            {
                new UIPageDefinition(
                    UIPageIds.Idle,
                    UIPageLayoutType.ExistingNode,
                    "IdlePanel",
                    layer: "main",
                    cachePolicy: UIPageCachePolicy.Reuse,
                    controllerScript: FacetLuaScriptIds.IdleRuntimeController),
                new UIPageDefinition(
                    UIPageIds.Dungeon,
                    UIPageLayoutType.ExistingNode,
                    "DungeonPanel",
                    layer: "main",
                    cachePolicy: UIPageCachePolicy.Reuse,
                    controllerScript: FacetLuaScriptIds.DungeonRuntimeController),
            };
        }
    }
}