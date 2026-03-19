#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Godot;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 默认 Godot 日志实现。
    /// </summary>
    public sealed class FacetLogger : IFacetLogger
    {
        private const string HistoryDirectoryName = "history";

        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private readonly bool _enableStructuredLogging;
        private readonly string _structuredLogPath;
        private readonly int _structuredLogHistoryLimit;
        private readonly int _bufferCapacity;
        private readonly Queue<FacetLogEntry> _buffer;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
        };

        private long _nextEventId;

        public FacetLogger(
            FacetLogLevel minimumLevel,
            bool enableStructuredLogging = true,
            string structuredLogPath = "user://logs/facet-structured.jsonl",
            int bufferCapacity = 256,
            int structuredLogHistoryLimit = 10,
            string? sessionId = null)
        {
            MinimumLevel = minimumLevel;
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
            _enableStructuredLogging = enableStructuredLogging;
            _structuredLogPath = structuredLogPath;
            _structuredLogHistoryLimit = Math.Max(1, structuredLogHistoryLimit);
            _bufferCapacity = Math.Max(32, bufferCapacity);
            _buffer = new Queue<FacetLogEntry>(_bufferCapacity);

            if (_enableStructuredLogging)
            {
                PrepareStructuredLogFile();
            }
        }

        /// <inheritdoc />
        public FacetLogLevel MinimumLevel { get; }

        /// <summary>
        /// 当前运行会话标识。
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 新日志入缓冲后的通知。
        /// </summary>
        public event Action<FacetLogEntry>? EntryLogged;

        /// <summary>
        /// 获取当前缓冲区内的最近日志快照。
        /// </summary>
        public IReadOnlyList<FacetLogEntry> GetBufferedEntries()
        {
            return _buffer.ToArray();
        }

        /// <inheritdoc />
        public void Log(FacetLogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            if (level < MinimumLevel)
            {
                return;
            }

            FacetLogEntry entry = FacetLogEntry.CreateNow(
                SessionId,
                Interlocked.Increment(ref _nextEventId),
                level,
                category,
                message,
                payload);

            BufferEntry(entry);
            WriteConsoleEntry(entry);
            WriteStructuredEntry(entry);
            NotifyEntryLogged(entry);
        }

        /// <inheritdoc />
        public void Trace(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            Log(FacetLogLevel.Trace, category, message, payload);
        }

        /// <inheritdoc />
        public void Debug(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            Log(FacetLogLevel.Debug, category, message, payload);
        }

        /// <inheritdoc />
        public void Info(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            Log(FacetLogLevel.Info, category, message, payload);
        }

        /// <inheritdoc />
        public void Warning(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            Log(FacetLogLevel.Warning, category, message, payload);
        }

        /// <inheritdoc />
        public void Error(string category, string message, IReadOnlyDictionary<string, object?>? payload = null)
        {
            Log(FacetLogLevel.Error, category, message, payload);
        }

        private void BufferEntry(FacetLogEntry entry)
        {
            if (_buffer.Count >= _bufferCapacity)
            {
                _buffer.Dequeue();
            }

            _buffer.Enqueue(entry);
        }

        private void WriteConsoleEntry(FacetLogEntry entry)
        {
            string formatted = FormatConsoleEntry(entry);

            switch (entry.Level)
            {
                case FacetLogLevel.Warning:
                    GD.PushWarning(formatted);
                    break;
                case FacetLogLevel.Error:
                    GD.PushError(formatted);
                    break;
                default:
                    GD.Print(formatted);
                    break;
            }
        }

        private string FormatConsoleEntry(FacetLogEntry entry)
        {
            string prefix = $"[Facet][{entry.Level}][{entry.Category}][S:{GetShortSessionId(entry.SessionId)}][E:{entry.EventId}]";
            if (!entry.HasPayload)
            {
                return $"{prefix} {entry.Message}";
            }

            string payloadJson = JsonSerializer.Serialize(entry.Payload, _jsonOptions);
            return $"{prefix} {entry.Message} Payload={payloadJson}";
        }

        private void PrepareStructuredLogFile()
        {
            try
            {
                string activePath = ProjectSettings.GlobalizePath(_structuredLogPath);
                string? logsDirectory = Path.GetDirectoryName(activePath);
                if (string.IsNullOrWhiteSpace(logsDirectory))
                {
                    return;
                }

                Directory.CreateDirectory(logsDirectory);
                ArchiveExistingLogIfNeeded(activePath, logsDirectory, "facet-structured", ".jsonl");
                CleanupHistory(logsDirectory, "facet-structured-*.jsonl");

                if (!File.Exists(activePath))
                {
                    File.WriteAllText(activePath, string.Empty, Utf8WithoutBom);
                }
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[Facet][Warning][Logging] Structured log prepare failed. Path={_structuredLogPath} Error={exception.Message}");
            }
        }

        private void WriteStructuredEntry(FacetLogEntry entry)
        {
            if (!_enableStructuredLogging)
            {
                return;
            }

            try
            {
                string globalizedPath = ProjectSettings.GlobalizePath(_structuredLogPath);
                string? directory = Path.GetDirectoryName(globalizedPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string serialized = JsonSerializer.Serialize(entry, _jsonOptions);
                File.AppendAllText(globalizedPath, serialized + System.Environment.NewLine, Utf8WithoutBom);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[Facet][Warning][Logging] Structured log write failed. Path={_structuredLogPath} Error={exception.Message}");
            }
        }

        private void NotifyEntryLogged(FacetLogEntry entry)
        {
            try
            {
                EntryLogged?.Invoke(entry);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[Facet][Warning][Logging] Structured log listener failed. Error={exception.Message}");
            }
        }

        private void ArchiveExistingLogIfNeeded(string activePath, string logsDirectory, string prefix, string extension)
        {
            FileInfo activeFile = new(activePath);
            if (!activeFile.Exists || activeFile.Length <= 0)
            {
                return;
            }

            string historyDirectory = Path.Combine(logsDirectory, HistoryDirectoryName);
            Directory.CreateDirectory(historyDirectory);

            string archiveFileName = $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{GetShortSessionId(SessionId)}{extension}";
            string archivePath = Path.Combine(historyDirectory, archiveFileName);
            File.Move(activePath, archivePath, false);
        }

        private void CleanupHistory(string logsDirectory, string searchPattern)
        {
            string historyDirectory = Path.Combine(logsDirectory, HistoryDirectoryName);
            if (!Directory.Exists(historyDirectory))
            {
                return;
            }

            FileInfo[] historyFiles = new DirectoryInfo(historyDirectory)
                .GetFiles(searchPattern, SearchOption.TopDirectoryOnly);

            Array.Sort(historyFiles, static (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));

            for (int index = _structuredLogHistoryLimit; index < historyFiles.Length; index++)
            {
                historyFiles[index].Delete();
            }
        }

        private static string GetShortSessionId(string sessionId)
        {
            if (sessionId.Length <= 8)
            {
                return sessionId;
            }

            return sessionId[..8];
        }
    }
}
