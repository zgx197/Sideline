#nullable enable

using System;
using System.IO;
using System.Text;
using Godot;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// 负责为活动中的 Godot 文本日志补齐 UTF-8 BOM。
    /// 在 Windows 下，一些外部工具会把无 BOM 的 UTF-8 文本误判为本地编码，
    /// 进而导致 godot.log 中的中文显示乱码。
    /// </summary>
    internal static class FacetPlainTextLogEncoding
    {
        /// <summary>
        /// UTF-8 BOM 字节序列。
        /// </summary>
        private static readonly byte[] Utf8Bom = Encoding.UTF8.GetPreamble();

        /// <summary>
        /// 标记当前进程是否已经尝试处理过活动日志文件。
        /// 即使处理失败，也不再为每条日志重复触发文件访问。
        /// </summary>
        private static bool _attempted;

        /// <summary>
        /// 标记当前活动日志是否已经具备 UTF-8 BOM。
        /// </summary>
        private static bool _prepared;

        /// <summary>
        /// 为当前活动的 godot.log 补齐 UTF-8 BOM。
        /// 如果文件不存在、无法访问或本身已具备 BOM，则直接跳过。
        /// </summary>
        public static void EnsureGodotLogUtf8Bom()
        {
            if (_prepared || _attempted)
            {
                return;
            }

            _attempted = true;

            try
            {
                string logPath = ProjectSettings.GlobalizePath("user://logs/godot.log");
                if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
                {
                    return;
                }

                using FileStream stream = new(logPath, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite);
                if (stream.Length == 0)
                {
                    stream.Write(Utf8Bom, 0, Utf8Bom.Length);
                    stream.Flush();
                    _prepared = true;
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
                        _prepared = true;
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

                _prepared = true;
            }
            catch (Exception)
            {
                // 这条修复链路不应反向影响游戏运行。
                // 如果当前日志文件被 Godot 占用或系统暂时拒绝访问，则保留默认输出行为。
            }
        }
    }
}
