using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework.Systems
{
    /// <summary>
    /// 最小联调用二维位置组件。
    /// </summary>
    public struct Position2D : IComponent
    {
        public FP X;
        public FP Y;
    }

    /// <summary>
    /// 最小联调用二维速度组件。
    /// </summary>
    public struct Velocity2D : IComponent
    {
        public FP X;
        public FP Y;
    }

    /// <summary>
    /// 最小联调用生命周期组件。
    /// </summary>
    public struct Lifetime : IComponent
    {
        public FP Remaining;
    }

    /// <summary>
    /// 最小联调用生成器组件。
    /// </summary>
    public struct Spawner : IComponent
    {
        public int RemainingCount;
        public FP CooldownRemaining;
        public FP Interval;
        public FP SpawnVelocityX;
        public FP SpawnVelocityY;
        public FP SpawnLifetime;
    }

    /// <summary>
    /// 最小联调用投射物标记。
    /// </summary>
    public struct ProjectileTag : IComponent
    {
    }
}
