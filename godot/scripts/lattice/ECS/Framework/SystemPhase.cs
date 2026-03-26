namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 当前 Lattice 正式支持的轻量系统阶段。
    /// 该阶段模型保持平面，不引入 `SystemGroup`、树状层级或依赖图。
    /// </summary>
    public enum SystemPhase
    {
        Input = 0,
        PreSimulation = 1,
        Simulation = 2,
        Resolve = 3,
        Cleanup = 4
    }
}
