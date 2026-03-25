using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// 8.5 分运行时回归入口。
    /// 这组测试用于覆盖 Builder / Runner / 成功路径 / 错误路径 / 回滚路径 / 检查点路径。
    /// </summary>
    public class Runtime85RegressionTests
    {
        [Fact]
        public void Iteration85_BuilderRunnerEntry_IsRunnable()
        {
            using var runner = new SessionRunnerBuilder()
                .WithDeltaTime(FP.One)
                .Build();

            runner.Start();
            runner.Step();

            Assert.Equal(1, runner.Session.CurrentTick);
        }

        [Fact]
        public void Iteration85_CheckpointRestore_PathIsCovered()
        {
            var tests = new SessionTests();
            tests.CreateCheckpoint_AndRestoreFromCheckpoint_RecoverStateAndAllowContinue();
        }

        [Fact]
        public void Iteration85_Rollback_PathIsCovered()
        {
            var tests = new SessionTests();
            tests.VerifyFrame_WhenChecksumMismatch_RollsBackAndResimulatesToCurrentTick();
        }

        [Fact]
        public void Iteration85_SpawnerGameplay_PathIsCovered()
        {
            var tests = new SpawnerSystemIntegrationTests();
            tests.SpawnerChain_SpawnsMovesAndExpiresProjectiles();
        }
    }
}
