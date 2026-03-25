using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework.Systems
{
    /// <summary>
    /// 最小样例移动系统。
    /// </summary>
    public sealed class MovementSystem : ISystem
    {
        public void OnInit(Frame frame)
        {
        }

        public void OnUpdate(Frame frame, FP deltaTime)
        {
            var enumerator = frame.Query<Position2D, Velocity2D>().GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumerator.Component1.X += enumerator.Component2.X * deltaTime;
                enumerator.Component1.Y += enumerator.Component2.Y * deltaTime;
            }
        }

        public void OnDestroy(Frame frame)
        {
        }
    }
}
