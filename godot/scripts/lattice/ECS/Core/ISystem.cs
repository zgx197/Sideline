using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 系统接口 - 操作 Frame 状态
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 系统初始化
        /// </summary>
        void OnInit(Frame frame);

        /// <summary>
        /// 系统更新
        /// </summary>
        void OnUpdate(Frame frame, FP deltaTime);

        /// <summary>
        /// 系统销毁
        /// </summary>
        void OnDestroy(Frame frame);
    }
}
