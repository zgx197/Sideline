#nullable enable

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 预留给 Lua 控制器的红点接口。
    /// 阶段 8 先只保留受限查询能力，具体实现留到扩展系统阶段。
    /// </summary>
    public interface ILuaRedDotBridge
    {
        bool IsAvailable { get; }

        bool TryGetState(string path, out bool hasRedDot);
    }
}