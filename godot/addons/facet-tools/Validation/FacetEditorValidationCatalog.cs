#nullable enable

using System.Collections.Generic;
using System.IO;

/// <summary>
/// Facet 编辑器主面板使用的静态校验目录。
/// 将文件系统检查从主面板脚本中剥离，避免 UI 热区承担装配规则本身。
/// </summary>
internal static class FacetEditorValidationCatalog
{
    public static List<FacetEditorValidationItem> Build(FacetEditorWorkspacePaths paths)
    {
        return new List<FacetEditorValidationItem>
        {
            CreateFileValidationItem(
                "Facet 主面板场景",
                paths.FacetMainScreenScenePath,
                "编辑器首页场景存在，可用于插件主屏创建。"),
            CreateFileValidationItem(
                "Facet 主面板脚本",
                paths.FacetMainScreenScriptPath,
                "编辑器首页脚本存在，且不应再承担运行时实验职责。"),
            CreateFileValidationItem(
                "Facet 插件配置",
                paths.PluginConfigPath,
                "Godot 可通过 plugin.cfg 正常加载 Facet 工具插件。"),
            CreateDirectoryValidationItem(
                "Facet 运行时宿主目录",
                paths.FacetRuntimeDirectoryPath,
                "运行时宿主目录存在，运行时逻辑与编辑器逻辑可继续分层整理。"),
            CreateDirectoryValidationItem(
                "运行时 UI 目录",
                paths.RuntimeUiDirectoryPath,
                "挂机窗口、地下城窗口等运行时调试落点目录存在。"),
            CreateLuaScriptsValidationItem(paths.LuaScriptsDirectoryPath),
            CreateFileValidationItem(
                "Facet 主说明文档",
                paths.FacetReadmePath,
                "Facet 长期设计说明存在，可作为编辑器入口与运行时边界的统一文档。"),
            CreateFileValidationItem(
                "Facet AI UI 工作流文档",
                paths.AiWorkflowPath,
                "Facet UI 相关工作流文档存在，可作为编辑期入口文档之一。"),
            CreateFileValidationItem(
                "程序集布局文档",
                paths.AssemblyLayoutPath,
                "程序集边界文档存在，可作为阶段 5 的边界冻结依据。"),
        };
    }

    private static FacetEditorValidationItem CreateFileValidationItem(string title, string path, string successDetail)
    {
        return File.Exists(path)
            ? new FacetEditorValidationItem(title, true, successDetail)
            : new FacetEditorValidationItem(title, false, $"缺少文件: {path}");
    }

    private static FacetEditorValidationItem CreateDirectoryValidationItem(string title, string path, string successDetail)
    {
        return Directory.Exists(path)
            ? new FacetEditorValidationItem(title, true, successDetail)
            : new FacetEditorValidationItem(title, false, $"缺少目录: {path}");
    }

    private static FacetEditorValidationItem CreateLuaScriptsValidationItem(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new FacetEditorValidationItem("Lua 脚本资源目录", false, $"缺少目录: {directoryPath}");
        }

        string[] scripts = Directory.GetFiles(directoryPath, "*.lua", SearchOption.TopDirectoryOnly);
        return scripts.Length > 0
            ? new FacetEditorValidationItem("Lua 脚本资源目录", true, $"检测到 {scripts.Length} 个 Lua 脚本资源。")
            : new FacetEditorValidationItem("Lua 脚本资源目录", false, $"目录存在但未检测到 Lua 脚本: {directoryPath}");
    }
}
