#nullable enable

using System;
using System.IO;
using System.Text;
using Godot;

public static class FacetEditorDiagnostics
{
    private const string ActiveLogFileName = "facet-editor.log";
    private const string HistoryDirectoryName = "history";
    private const int HistoryLimit = 10;

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly object SyncRoot = new();

    private static bool _prepared;

    public static void Info(string category, string message)
    {
        Write("INFO", category, message, null);
    }

    public static void Warning(string category, string message)
    {
        Write("WARNING", category, message, null);
    }

    public static void Error(string category, string message, Exception? exception = null)
    {
        Write("ERROR", category, message, exception);
    }

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

    private static string GetActiveLogPath()
    {
        return Path.Combine(ProjectSettings.GlobalizePath("user://logs"), ActiveLogFileName);
    }
}
