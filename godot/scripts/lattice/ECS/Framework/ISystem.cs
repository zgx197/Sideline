using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 最小系统接口。
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 系统初始化时调用。
        /// </summary>
        void OnInit(Frame frame);

        /// <summary>
        /// 每帧更新时调用。
        /// </summary>
        void OnUpdate(Frame frame, FP deltaTime);

        /// <summary>
        /// 系统销毁时调用。
        /// </summary>
        void OnDestroy(Frame frame);
    }
}
