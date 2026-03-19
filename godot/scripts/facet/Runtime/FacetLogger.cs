#nullable enable

using Godot;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 默认 Godot 日志实现。
    /// </summary>
    public sealed class FacetLogger : IFacetLogger
    {
        public FacetLogger(FacetLogLevel minimumLevel)
        {
            MinimumLevel = minimumLevel;
        }

        /// <inheritdoc />
        public FacetLogLevel MinimumLevel { get; }

        /// <inheritdoc />
        public void Log(FacetLogLevel level, string category, string message)
        {
            if (level < MinimumLevel)
            {
                return;
            }

            string formatted = $"[Facet][{level}][{category}] {message}";

            switch (level)
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

        /// <inheritdoc />
        public void Trace(string category, string message)
        {
            Log(FacetLogLevel.Trace, category, message);
        }

        /// <inheritdoc />
        public void Debug(string category, string message)
        {
            Log(FacetLogLevel.Debug, category, message);
        }

        /// <inheritdoc />
        public void Info(string category, string message)
        {
            Log(FacetLogLevel.Info, category, message);
        }

        /// <inheritdoc />
        public void Warning(string category, string message)
        {
            Log(FacetLogLevel.Warning, category, message);
        }

        /// <inheritdoc />
        public void Error(string category, string message)
        {
            Log(FacetLogLevel.Error, category, message);
        }
    }
}