#nullable enable

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 运行时上下文。
    /// </summary>
    public sealed class FacetRuntimeContext
    {
        public FacetRuntimeContext(FacetConfig config, FacetServices services, IFacetLogger logger)
        {
            Config = config;
            Services = services;
            Logger = logger;
        }

        /// <summary>
        /// Facet 当前配置。
        /// </summary>
        public FacetConfig Config { get; }

        /// <summary>
        /// Facet 当前服务容器。
        /// </summary>
        public FacetServices Services { get; }

        /// <summary>
        /// Facet 当前日志器。
        /// </summary>
        public IFacetLogger Logger { get; }
    }
}