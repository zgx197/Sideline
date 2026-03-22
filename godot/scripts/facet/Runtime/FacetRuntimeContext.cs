#nullable enable

using Sideline.Facet.Application;
using Sideline.Facet.Projection;

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

        /// <summary>
        /// Facet 当前命令总线。
        /// </summary>
        public ICommandBus CommandBus => Services.GetRequired<ICommandBus>();

        /// <summary>
        /// Facet 当前查询总线。
        /// </summary>
        public IQueryBus QueryBus => Services.GetRequired<IQueryBus>();

        /// <summary>
        /// Facet 当前 Projection 存储。
        /// </summary>
        public ProjectionStore ProjectionStore => Services.GetRequired<ProjectionStore>();

        /// <summary>
        /// Facet 当前 Projection 刷新协调器。
        /// </summary>
        public ProjectionRefreshCoordinator ProjectionRefreshCoordinator => Services.GetRequired<ProjectionRefreshCoordinator>();
    }
}
