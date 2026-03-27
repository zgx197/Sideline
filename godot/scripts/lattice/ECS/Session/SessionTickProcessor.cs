using System;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// SessionRuntime 的 Tick 管线执行器。
    /// 负责统一驱动输入、系统模拟、结构提交、清理与历史写入阶段。
    /// </summary>
    internal sealed class SessionTickProcessor
    {
        public void Execute(
            Frame frame,
            FP deltaTime,
            Action<SessionTickStage> enterStage,
            Action<Frame> applyInputs,
            Action<Frame, FP> updateSystems,
            Action<Frame> cleanupFrame,
            Action<Frame> captureHistory,
            Action<Frame, FP>? onFrameUpdate,
            bool raiseFrameUpdateEvent,
            bool captureHistoryStage)
        {
            enterStage(SessionTickStage.InputApply);
            applyInputs(frame);

            bool structuralScopeStarted = false;
            try
            {
                enterStage(SessionTickStage.Simulation);
                frame.BeginDeferredStructuralChanges();
                structuralScopeStarted = true;
                updateSystems(frame, deltaTime);

                enterStage(SessionTickStage.StructuralCommit);
                frame.CommitStructuralChanges();
                structuralScopeStarted = false;

                enterStage(SessionTickStage.Cleanup);
                cleanupFrame(frame);

                if (raiseFrameUpdateEvent)
                {
                    onFrameUpdate?.Invoke(frame, deltaTime);
                }

                if (captureHistoryStage)
                {
                    enterStage(SessionTickStage.HistoryCapture);
                    captureHistory(frame);
                }
            }
            finally
            {
                if (structuralScopeStarted)
                {
                    frame.AbortStructuralChanges();
                }

                enterStage(SessionTickStage.Idle);
            }
        }
    }
}
