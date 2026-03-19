#nullable enable

using System;
using System.Collections.Generic;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 结构化日志条目。
    /// </summary>
    public sealed class FacetLogEntry
    {
        public FacetLogEntry(
            DateTimeOffset timestampUtc,
            string sessionId,
            long eventId,
            FacetLogLevel level,
            string category,
            string message,
            IReadOnlyDictionary<string, object?>? payload = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            TimestampUtc = timestampUtc;
            SessionId = sessionId;
            EventId = eventId;
            Level = level;
            Category = category;
            Message = message;
            Payload = payload == null ? null : new Dictionary<string, object?>(payload);
        }

        /// <summary>
        /// UTC 时间戳。
        /// </summary>
        public DateTimeOffset TimestampUtc { get; }

        /// <summary>
        /// 本次运行的会话标识。
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 会话内单调递增的事件编号。
        /// </summary>
        public long EventId { get; }

        /// <summary>
        /// 日志级别。
        /// </summary>
        public FacetLogLevel Level { get; }

        /// <summary>
        /// 日志分类。
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// 人类可读的主消息。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 结构化载荷。
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Payload { get; }

        /// <summary>
        /// 是否包含结构化载荷。
        /// </summary>
        public bool HasPayload => Payload is { Count: > 0 };

        public static FacetLogEntry CreateNow(
            string sessionId,
            long eventId,
            FacetLogLevel level,
            string category,
            string message,
            IReadOnlyDictionary<string, object?>? payload = null)
        {
            return new FacetLogEntry(DateTimeOffset.UtcNow, sessionId, eventId, level, category, message, payload);
        }
    }
}