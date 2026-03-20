#nullable enable

using System;
using System.IO;
using System.Text;
using Godot;

/// <summary>
/// Facet 编辑器侧诊断日志工具。
/// 用于记录插件生命周期、布局异常与运行时捕获不到的编辑器行为问题。
/// </summary>
public static class FacetEditorDiagnostics
{
    /// <summary>
    /// 当前活动编辑器日志文件名。
    /// </summary>
    private const string ActiveLogFileName = "facet-editor.log";

    /// <summary>
    /// 历史日志归档目录名。
    /// </summary>
    private const string HistoryDirectoryName = "history";

    /// <summary>
    /// 最多保留的历史编辑器日志份数。
    /// </summary>
    private const int HistoryLimit = 10;

    /// <summary>
    /// 统一使用 UTF-8 无 BOM，避免 Godot 与外部工具读取时出现编码歧义。
    /// </summary>
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    /// <summary>
    /// 文件写入同步锁，避免多个编辑器回调同时写日志导致内容交错。
    /// </summary>
    private static readonly object SyncRoot = new();

    /// <summary>
    /// 是否已经完成日志目录与活动文件准备。
    /// </summary>
    private static bool _prepared;

    /// <summary>
    /// 写入一条信息级诊断日志。
    /// </summary>
    public static void Info(string category, string message)
    {
        Write("INFO", category, message, null);
    }

    /// <summary>
    /// 写入一条警告级诊断日志。
    /// </summary>
    public static void Warning(string category, string message)
    {
        Write("WARNING", category, message, null);
    }

    /// <summary>
    /// 写入一条错误级诊断日志，并在需要时附带异常详情。
    /// </summary>
    public static void Error(string category, string message, Exception? exception = null)
    {
        Write("ERROR", category, message, exception);
    }

    /// <summary>
    /// 统一格式化并分发日志到文件与 Godot 控制台。
    /// 即使文件写入失败，也尽量保证控制台侧仍然能看到问题。
    /// </summary>
    private static void Write(string level, string category, string message, Exception? exception)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        string line = $"[{timestamp}] [{level}] [{category}] {message}";
        if (exception != null)
        {
            line += $"{System.Environment.NewLine}{exception}";
        }

        try
        {
            lock (SyncRoot)
            {
                EnsurePrepared();
                string logPath = GetActiveLogPath();
                File.AppendAllText(logPath, line + System.Environment.NewLine, Utf8WithoutBom);
            }
        }
        catch
        {
            // 编辑器诊断日志不能反向影响插件运行，因此文件写失败时静默降级到控制台输出。
        }

        switch (level)
        {
            case "ERROR":
                GD.PushError(line);
                break;
            case "WARNING":
                GD.PushWarning(line);
                break;
            default:
                GD.Print(line);
                break;
        }
    }

    /// <summary>
    /// 准备日志目录、活动文件与历史归档环境。
    /// 第一次写日志前只执行一次。
    /// </summary>
    private static void EnsurePrepared()
    {
        if (_prepared)
        {
            return;
        }

        string logsDirectory = ProjectSettings.GlobalizePath("user://logs");
        Directory.CreateDirectory(logsDirectory);

        string activeLogPath = Path.Combine(logsDirectory, ActiveLogFileName);
        FileInfo activeLogFile = new(activeLogPath);
        if (activeLogFile.Exists && activeLogFile.Length > 0)
        {
            string historyDirectory = Path.Combine(logsDirectory, HistoryDirectoryName);
            Directory.CreateDirectory(historyDirectory);

            string archiveFileName = $"facet-editor-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log";
            string archivePath = Path.Combine(historyDirectory, archiveFileName);
            File.Move(activeLogPath, archivePath, false);
        }

        CleanupHistory(logsDirectory);

        if (!File.Exists(activeLogPath))
        {
            File.WriteAllText(activeLogPath, string.Empty, Utf8WithoutBom);
        }

        _prepared = true;
    }

    /// <summary>
    /// 清理超出保留策略的历史编辑器日志。
    /// </summary>
    private static void CleanupHistory(string logsDirectory)
    {
        string historyDirectory = Path.Combine(logsDirectory, HistoryDirectoryName);
        if (!Directory.Exists(historyDirectory))
        {
            return;
        }

        FileInfo[] historyFiles = new DirectoryInfo(historyDirectory).GetFiles("facet-editor-*.log", SearchOption.TopDirectoryOnly);
        Array.Sort(historyFiles, static (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));

        for (int index = HistoryLimit; index < historyFiles.Length; index++)
        {
            historyFiles[index].Delete();
        }
    }

    /// <summary>
    /// 返回当前活动编辑器日志的绝对路径。
    /// </summary>
    private static string GetActiveLogPath()
    {
        return Path.Combine(ProjectSettings.GlobalizePath("user://logs"), ActiveLogFileName);
    }
}
