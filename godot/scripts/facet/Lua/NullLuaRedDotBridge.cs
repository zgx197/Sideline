#nullable enable

namespace Sideline.Facet.Lua
{
    /// <summary>
    /// 默认红点桥接占位实现。
    /// 在真正的红点树系统接入前，统一向 Lua 控制器返回“不可用”状态。
    /// </summary>
    public sealed class NullLuaRedDotBridge : ILuaRedDotBridge
    {
        public bool IsAvailable => false;

        public bool TryGetState(string path, out bool hasRedDot)
        {
            hasRedDot = false;
            return false;
        }
    }
}