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
    /// Facet 榛樿 Godot 鏃ュ織瀹炵幇銆?    /// 鍚屾椂璐熻矗缁撴瀯鍖栨棩蹇椼€丟odot 鎺у埗鍙拌緭鍑哄拰绾枃鏈暅鍍忔棩蹇椼€?    /// </summary>
    public sealed class FacetLogger : IFacetLogger
    {
        private const string HistoryDirectoryName = "history";

        private static readonly UTF8Encoding Utf8WithoutBom = new(false);
        private static readonly UTF8Encoding Utf8WithBom = new(true);
        private static readonly byte[] Utf8Bom = Encoding.UTF8.GetPreamble();
        private const int MaxFileWriteAttempts = 3;

        private readonly bool _enableStructuredLogging;
        private readonly string _structuredLogPath;
        private readonly int _structuredLogHistoryLimit;
        private readonly bool _enableConsoleMirrorLogging;
        private readonly string _consoleMirrorLogPath;
        private readonly int _consoleMirrorLogHistoryLimit;
        private readonly int _bufferCapacity;
        private readonly Queue<FacetLogEntry> _buffer;
        private readonly object _fileWriteLock = new();
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
            bool enableConsoleMirrorLogging = true,
            string consoleMirrorLogPath = "user://logs/facet-console.log",
            int consoleMirrorLogHistoryLimit = 10,
            string? sessionId = null)
        {
            MinimumLevel = minimumLevel;
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
            _enableStructuredLogging = enableStructuredLogging;
            _structuredLogPath = structuredLogPath;
            _structuredLogHistoryLimit = Math.Max(1, structuredLogHistoryLimit);
            _enableConsoleMirrorLogging = enableConsoleMirrorLogging;
            _consoleMirrorLogPath = consoleMirrorLogPath;
            _consoleMirrorLogHistoryLimit = Math.Max(1, consoleMirrorLogHistoryLimit);
            _bufferCapacity = Math.Max(32, bufferCapacity);
            _buffer = new Queue<FacetLogEntry>(_bufferCapacity);

            if (_enableStructuredLogging)
            {
                PrepareStructuredLogFile();
            }

            if (_enableConsoleMirrorLogging)
            {
                PrepareConsoleMirrorLogFile();
            }
        }

        /// <inheritdoc />
        public FacetLogLevel MinimumLevel { get; }

        /// <summary>
        /// 褰撳墠杩愯浼氳瘽鏍囪瘑銆?        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 鏂版棩蹇楀叆缂撳啿鍚庣殑閫氱煡銆?        /// </summary>
        public event Action<FacetLogEntry>? EntryLogged;

        /// <summary>
        /// 鑾峰彇褰撳墠缂撳啿鍖哄唴鐨勬渶杩戞棩蹇楀揩鐓с€?        /// </summary>
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
            WriteConsoleMirrorEntry(entry);
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
            FacetPlainTextLogEncoding.EnsureGodotLogUtf8Bom();
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

        private void WriteConsoleMirrorEntry(FacetLogEntry entry)
        {
            if (!_enableConsoleMirrorLogging)
            {
                return;
            }

            try
            {
                string globalizedPath = ProjectSettings.GlobalizePath(_consoleMirrorLogPath);
                string? directory = Path.GetDirectoryName(globalizedPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string line = FormatConsoleMirrorEntry(entry);
                AppendLineWithSharing(globalizedPath, line, writeBomWhenFileEmpty: true);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[Facet][Warning][Logging] Console mirror write failed. Path={_consoleMirrorLogPath} Error={exception.Message}");
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

        private string FormatConsoleMirrorEntry(FacetLogEntry entry)
        {
            string prefix = $"[{entry.TimestampUtc:O}] [Facet][{entry.Level}][{entry.Category}][S:{GetShortSessionId(entry.SessionId)}][E:{entry.EventId}]";
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
                PrepareLogFile(_structuredLogPath, "facet-structured", ".jsonl", _structuredLogHistoryLimit, writeBom: false);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[Facet][Warning][Logging] Structured log prepare failed. Path={_structuredLogPath} Error={exception.Message}");
            }
        }

        private void PrepareConsoleMirrorLogFile()
        {
            try
            {
                PrepareLogFile(_consoleMirrorLogPath, "facet-console", ".log", _consoleMirrorLogHistoryLimit, writeBom: true);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[Facet][Warning][Logging] Console mirror prepare failed. Path={_consoleMirrorLogPath} Error={exception.Message}");
            }
        }

        private void PrepareLogFile(string logPath, string archivePrefix, string extension, int historyLimit, bool writeBom)
        {
            string activePath = ProjectSettings.GlobalizePath(logPath);
            string? logsDirectory = Path.GetDirectoryName(activePath);
            if (string.IsNullOrWhiteSpace(logsDirectory))
            {
                return;
            }

            Directory.CreateDirectory(logsDirectory);
            ArchiveExistingLogIfNeeded(activePath, logsDirectory, archivePrefix, extension);
            CleanupHistory(logsDirectory, $"{archivePrefix}-*{extension}", historyLimit);

            if (!File.Exists(activePath))
            {
                File.WriteAllText(activePath, string.Empty, writeBom ? Utf8WithBom : Utf8WithoutBom);
                return;
            }

            if (writeBom)
            {
                EnsureUtf8Bom(activePath);
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
                AppendLineWithSharing(globalizedPath, serialized, writeBomWhenFileEmpty: false);
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

        private static void EnsureUtf8Bom(string path)
        {
            using FileStream stream = new(path, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite);
            if (stream.Length == 0)
            {
                stream.Write(Utf8Bom, 0, Utf8Bom.Length);
                stream.Flush();
                return;
            }

            if (stream.Length >= Utf8Bom.Length)
            {
                Span<byte> header = stackalloc byte[3];
                int read = stream.Read(header);
                if (read == Utf8Bom.Length &&
                    header[0] == Utf8Bom[0] &&
                    header[1] == Utf8Bom[1] &&
                    header[2] == Utf8Bom[2])
                {
                    return;
                }
            }

            stream.Position = 0;
            byte[] existingBytes = new byte[stream.Length];
            _ = stream.Read(existingBytes, 0, existingBytes.Length);

            stream.Position = 0;
            stream.SetLength(0);
            stream.Write(Utf8Bom, 0, Utf8Bom.Length);
            stream.Write(existingBytes, 0, existingBytes.Length);
            stream.Flush();
        }

        private void AppendLineWithSharing(string path, string line, bool writeBomWhenFileEmpty)
        {
            byte[] lineBytes = Utf8WithoutBom.GetBytes(line + System.Environment.NewLine);
            IOException? lastException = null;

            lock (_fileWriteLock)
            {
                for (int attempt = 1; attempt <= MaxFileWriteAttempts; attempt++)
                {
                    try
                    {
                        using FileStream stream = new(path, FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

                        if (writeBomWhenFileEmpty && stream.Length == 0)
                        {
                            stream.Write(Utf8Bom, 0, Utf8Bom.Length);
                        }

                        stream.Seek(0, SeekOrigin.End);
                        stream.Write(lineBytes, 0, lineBytes.Length);
                        stream.Flush();
                        return;
                    }
                    catch (IOException exception) when (attempt < MaxFileWriteAttempts)
                    {
                        lastException = exception;
                        Thread.Sleep(5 * attempt);
                    }
                    catch (IOException exception)
                    {
                        lastException = exception;
                        break;
                    }
                }
            }

            throw lastException ?? new IOException($"AppendLineWithSharing failed: {path}");
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

        private void CleanupHistory(string logsDirectory, string searchPattern, int historyLimit)
        {
            string historyDirectory = Path.Combine(logsDirectory, HistoryDirectoryName);
            if (!Directory.Exists(historyDirectory))
            {
                return;
            }

            FileInfo[] historyFiles = new DirectoryInfo(historyDirectory)
                .GetFiles(searchPattern, SearchOption.TopDirectoryOnly);

            Array.Sort(historyFiles, static (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));

            for (int index = historyLimit; index < historyFiles.Length; index++)
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
