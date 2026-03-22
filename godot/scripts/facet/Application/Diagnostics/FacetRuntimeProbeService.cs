#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Sideline.Facet.Runtime;

namespace Sideline.Facet.Application.Diagnostics
{
    /// <summary>
    /// Facet 阶段 2 的最小应用服务样例。
    /// 用于验证 Query -> AppService -> Command -> Gateway 这条应用边界链路。
    /// </summary>
    public sealed class FacetRuntimeProbeService : IAppService
    {
        private readonly FacetRuntimeContext _context;
        private readonly IFacetRuntimeProbeGateway _gateway;
        private readonly string _sessionId;

        public FacetRuntimeProbeService(
            FacetRuntimeContext context,
            IFacetRuntimeProbeGateway gateway,
            string sessionId)
        {
            _context = context;
            _gateway = gateway;
            _sessionId = sessionId;
        }

        public ValueTask<AppResult<FacetRuntimeProbeSnapshot>> QueryCurrentAsync(
            FacetRuntimeProbeQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            FacetRuntimeProbeSnapshot snapshot = new(
                sessionId: _sessionId,
                hotReloadEnabled: _context.Config.EnableHotReload,
                pageCacheEnabled: _context.Config.EnablePageCacheByDefault,
                pageCacheCapacity: _context.Config.DefaultPageCacheCapacity,
                structuredLoggingEnabled: _context.Config.EnableStructuredLogging,
                structuredLogPath: _context.Config.StructuredLogPath,
                commandBusRegistered: _context.Services.Contains<ICommandBus>(),
                queryBusRegistered: _context.Services.Contains<IQueryBus>(),
                capturedAtUtc: DateTimeOffset.UtcNow);

            return ValueTask.FromResult(AppResult<FacetRuntimeProbeSnapshot>.Success(snapshot));
        }

        public async ValueTask<AppResult> RecordAsync(
            RecordFacetRuntimeProbeCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            await _gateway.RecordAsync(command.Snapshot, cancellationToken);
            return AppResult.Success();
        }

        public async ValueTask<AppResult<FacetRuntimeProbeStatusSnapshot>> QueryStatusAsync(
            FacetRuntimeProbeStatusQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            FacetRuntimeProbeStatusSnapshot status = await _gateway.GetStatusAsync(cancellationToken);
            return AppResult<FacetRuntimeProbeStatusSnapshot>.Success(status);
        }
    }
}
