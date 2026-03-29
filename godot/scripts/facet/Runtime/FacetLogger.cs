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
    /// Facet 婵帗绋掗…鍫ヮ敇?Godot 闂佸搫鍟ㄩ崕杈╂崲閺冨倵鍋撻崷顓炰户妤犵偛娲俊?    /// 闂佸憡鑹鹃張顒€顪冮崒婊勫闁绘棁宕甸悡鎴犵磽娴ｈ灏伴柣搴灦瀹曠娀寮介妸锔肩礆闂婎偄娲﹂妵婊堝焵椤戣法顑刼dot 闂佺鐭囬崘銊у幀闂佸憡鐟︽刊鐣屾椤撱垹绀勯柛婵嗗鐎氳尙绱掔紒銏犵仭闁哄鍟村鐢割敆閸曨剚鐣堕梺绋跨Т缁绘垵螞閳哄嫮鐤€婵＄偛鐨烽崑?    /// </summary>
    public sealed class FacetLogger : IFacetLogger, IDisposable
    {
        private const string HistoryDirectoryName = "history";
        private static readonly HashSet<string> SuppressedConsoleDebugCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "UI.Binding.Register",
            "UI.Binding.Refresh",
            "UI.Binding.Component",
            "UI.Binding.ComplexList",
        };

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
        private bool _disposed;

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
        /// 閻熸粎澧楅幐鍛婃櫠閻樿櫕浜ら柟閭﹀灱閺€钘壝归崗闂翠孩闁伙缚绮欏浠嬪炊椤忓棙顫氶梺?        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 闂佸搫鍊绘晶妤€螞閳哄嫮鐤€婵°倐鍋撻柛娆忔缁辨捇骞橀崘鍙夋瘣闂佸憡鑹惧ù宄扳枔閹达附鐒绘慨妯夸含閸欌偓闂?        /// </summary>
        public event Action<FacetLogEntry>? EntryLogged;

        /// <summary>
        /// 闂佸吋鍎抽崲鑼躲亹閸ヮ亗浜归柟鎯у暱椤ゅ懐绱撻崒娑欏碍闁哥喎娼″畷鐘诲传閸曨偅鏆ラ梺姹囧妼鐎氼厼銆掗懜鍨氦闁瑰鍋為敍澶愮叓閸ヮ煈娈旈柟鐑╂櫊閹ゎ槹闁?        /// </summary>
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
            if (ShouldWriteToConsoleSurfaces(entry))
            {
                WriteConsoleEntry(entry);
                WriteConsoleMirrorEntry(entry);
            }

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

        private static bool ShouldWriteToConsoleSurfaces(FacetLogEntry entry)
        {
            if (entry.Level == FacetLogLevel.Debug &&
                SuppressedConsoleDebugCategories.Contains(entry.Category))
            {
                return false;
            }

            return true;
        }

        private void WriteConsoleMirrorEntry(FacetLogEntry entry)
        {
            if (!_enableConsoleMirrorLogging)
            {
                return;
            }

            try
            {
                string globalizedPath = ResolveOutputPath(_consoleMirrorLogPath);
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
            string activePath = ResolveOutputPath(logPath);
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
                string globalizedPath = ResolveOutputPath(_structuredLogPath);
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

        private static string ResolveOutputPath(string path)
        {
            if (path.StartsWith("user://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                return ProjectSettings.GlobalizePath(path);
            }

            return Path.GetFullPath(path);
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
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            EntryLogged = null;
            _buffer.Clear();
            _disposed = true;
        }
    }
}
