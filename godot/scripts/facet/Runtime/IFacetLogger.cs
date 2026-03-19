#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 统一日志接口。
    /// </summary>
    public interface IFacetLogger
    {
        /// <summary>
        /// 当前最小日志级别。
        /// </summary>
        FacetLogLevel MinimumLevel { get; }

        /// <summary>
        /// 记录一条日志。
        /// </summary>
        void Log(FacetLogLevel level, string category, string message);

        /// <summary>
        /// 记录 Trace 日志。
        /// </summary>
        void Trace(string category, string message);

        /// <summary>
        /// 记录 Debug 日志。
        /// </summary>
        void Debug(string category, string message);

        /// <summary>
        /// 记录 Info 日志。
        /// </summary>
        void Info(string category, string message);

        /// <summary>
        /// 记录 Warning 日志。
        /// </summary>
        void Warning(string category, string message);

        /// <summary>
        /// 记录 Error 日志。
        /// </summary>
        void Error(string category, string message);
    }
}