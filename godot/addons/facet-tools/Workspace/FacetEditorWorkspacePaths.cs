#nullable enable

using System;
using System.IO;
using Godot;

/// <summary>
/// Facet 编辑器主面板使用的工作区路径快照。
/// 统一封装路径解析逻辑，避免 UI 控制器直接拼接项目目录结构。
/// </summary>
internal sealed class FacetEditorWorkspacePaths
{
    private FacetEditorWorkspacePaths(
        string projectRootPath,
        string docsDirectoryPath,
        string logsDirectoryPath,
        string facetToolsDirectoryPath,
        string facetRuntimeDirectoryPath,
        string runtimeUiDirectoryPath,
        string refactorPlanPath,
        string aiWorkflowPath,
        string assemblyLayoutPath,
        string workflowPath,
        string facetMainScreenScenePath,
        string facetMainScreenScriptPath,
        string pluginConfigPath,
        string luaScriptsDirectoryPath)
    {
        ProjectRootPath = projectRootPath;
        DocsDirectoryPath = docsDirectoryPath;
        LogsDirectoryPath = logsDirectoryPath;
        FacetToolsDirectoryPath = facetToolsDirectoryPath;
        FacetRuntimeDirectoryPath = facetRuntimeDirectoryPath;
        RuntimeUiDirectoryPath = runtimeUiDirectoryPath;
        RefactorPlanPath = refactorPlanPath;
        AiWorkflowPath = aiWorkflowPath;
        AssemblyLayoutPath = assemblyLayoutPath;
        WorkflowPath = workflowPath;
        FacetMainScreenScenePath = facetMainScreenScenePath;
        FacetMainScreenScriptPath = facetMainScreenScriptPath;
        PluginConfigPath = pluginConfigPath;
        LuaScriptsDirectoryPath = luaScriptsDirectoryPath;
    }

    public string ProjectRootPath { get; }

    public string DocsDirectoryPath { get; }

    public string LogsDirectoryPath { get; }

    public string FacetToolsDirectoryPath { get; }

    public string FacetRuntimeDirectoryPath { get; }

    public string RuntimeUiDirectoryPath { get; }

    public string RefactorPlanPath { get; }

    public string AiWorkflowPath { get; }

    public string AssemblyLayoutPath { get; }

    public string WorkflowPath { get; }

    public string FacetMainScreenScenePath { get; }

    public string FacetMainScreenScriptPath { get; }

    public string PluginConfigPath { get; }

    public string LuaScriptsDirectoryPath { get; }

    public static FacetEditorWorkspacePaths Create()
    {
        string godotRoot = Path.GetDirectoryName(ProjectSettings.GlobalizePath("res://project.godot"))
            ?? throw new InvalidOperationException("Godot project root path resolve failed.");

        string projectRootPath = Path.GetFullPath(Path.Combine(godotRoot, ".."));
        string docsDirectoryPath = Path.Combine(projectRootPath, "docs");

        return new FacetEditorWorkspacePaths(
            projectRootPath,
            docsDirectoryPath,
            ProjectSettings.GlobalizePath("user://logs"),
            Path.GetDirectoryName(ProjectSettings.GlobalizePath("res://addons/facet-tools/plugin.cfg"))
                ?? throw new InvalidOperationException("Facet tools directory resolve failed."),
            ProjectSettings.GlobalizePath("res://scripts/facet/Runtime"),
            ProjectSettings.GlobalizePath("res://scripts/ui"),
            Path.Combine(projectRootPath, "docs", "development", "FacetRefactorPlan.md"),
            Path.Combine(projectRootPath, "docs", "development", "FacetAIUIWorkflow.md"),
            Path.Combine(projectRootPath, "docs", "development", "AssemblyLayout.md"),
            Path.Combine(projectRootPath, "docs", "development", "Workflow.md"),
            ProjectSettings.GlobalizePath("res://addons/facet-tools/FacetMainScreen.tscn"),
            ProjectSettings.GlobalizePath("res://addons/facet-tools/FacetMainScreen.cs"),
            ProjectSettings.GlobalizePath("res://addons/facet-tools/plugin.cfg"),
            ProjectSettings.GlobalizePath("res://scripts/facet/LuaScripts"));
    }
}
