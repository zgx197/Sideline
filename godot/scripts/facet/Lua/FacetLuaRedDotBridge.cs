#nullable enable

using System;
using Sideline.Facet.Extensions.RedDot;

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// Facet 正式 Lua 红点桥接实现。
    /// 通过 RedDotService 向 Lua 控制器暴露受限查询能力。
    /// </summary>
    public sealed class FacetLuaRedDotBridge : ILuaRedDotBridge
    {
        private readonly IRedDotService _redDotService;

        public FacetLuaRedDotBridge(IRedDotService redDotService)
        {
            ArgumentNullException.ThrowIfNull(redDotService);
            _redDotService = redDotService;
        }

        public bool IsAvailable => _redDotService.IsAvailable;

        public bool TryGetState(string path, out bool hasRedDot)
        {
            return _redDotService.TryGetState(path, out hasRedDot);
        }
    }
}
