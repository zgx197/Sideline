using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 最小系统接口。
    /// 当前主干正式支持的是“平面 `SystemScheduler` + 轻量 phase + 显式生命周期”模型，
    /// 不要求系统派生自 `SystemBase`，也不引入 group / enable-disable / dependency graph 等更重系统家族语义。
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 系统所属的执行阶段。
        /// 默认落在 `Simulation`，调度器会先按 phase 排序，再在 phase 内保持注册顺序稳定。
        /// </summary>
        SystemPhase Phase => SystemPhase.Simulation;

        /// <summary>
        /// 当前系统的轻量作者契约。
        /// 默认允许读写普通组件数据，但不默认开放 global state 与结构性修改；
        /// 若系统需要这些更强能力，应显式声明，而不是继续依赖隐式约定。
        /// </summary>
        SystemAuthoringContract Contract => SystemAuthoringContract.Default;

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
