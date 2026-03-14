using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// Session 鍗曞厓娴嬭瘯
    /// </summary>
    public class SessionTests
    {
        private struct Position : IEquatable<Position>
        {
            public int X, Y;
            public Position(int x, int y) { X = x; Y = y; }
            public bool Equals(Position other) => X == other.X && Y == other.Y;
            public override bool Equals(object? obj) => obj is Position p && Equals(p);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        private class TestSession : Session
        {
            public TestSession(FP deltaTime, ComponentTypeRegistry registry) 
                : base(deltaTime, registry, 0)
            {
            }

            protected override void ApplyInputs(Frame frame)
            {
                // 娴嬭瘯鐢ㄧ┖瀹炵幇
            }
        }

        private class TestSystem : ISystem
        {
            public int InitCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int DestroyCount { get; private set; }

            public void OnInit(Frame frame) => InitCount++;
            public void OnUpdate(Frame frame, FP deltaTime) => UpdateCount++;
            public void OnDestroy(Frame frame) => DestroyCount++;
        }

        private ComponentTypeRegistry CreateRegistry()
        {
            var registry = new ComponentTypeRegistry();
            registry.Register<Position>();
            return registry;
        }

        [Fact]
        public void Constructor_InitialState_ShouldBeCorrect()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);

            Assert.Equal(0, session.CurrentTick);
            Assert.False(session.IsRunning);
            Assert.Null(session.VerifiedFrame);
            Assert.Null(session.PredictedFrame);
        }

        [Fact]
        public void Start_ShouldInitializeFrames()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);

            session.Start();

            Assert.True(session.IsRunning);
            Assert.NotNull(session.VerifiedFrame);
            Assert.NotNull(session.PredictedFrame);
            Assert.Equal(0, session.PredictedFrame.Tick);
        }

        [Fact]
        public void Stop_ShouldCleanup()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Stop();

            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Update_ShouldAdvanceTick()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Update();

            Assert.Equal(1, session.CurrentTick);
            Assert.Equal(1, session.PredictedFrame?.Tick);
        }

        [Fact]
        public void Update_MultipleTimes_ShouldIncreaseTick()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            
            for (int i = 0; i < 10; i++)
            {
                session.Update();
            }

            Assert.Equal(10, session.CurrentTick);
        }

        [Fact]
        public void RegisterSystem_BeforeStart_ShouldWork()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            var system = new TestSystem();

            session.RegisterSystem(system);
            session.Start();

            Assert.Equal(1, system.InitCount);
        }

        [Fact]
        public void RegisterSystem_AfterStart_ShouldCallInit()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            var system = new TestSystem();

            session.Start();
            session.RegisterSystem(system);

            Assert.Equal(1, system.InitCount);
        }

        [Fact]
        public void UnregisterSystem_ShouldCallDestroy()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            var system = new TestSystem();

            session.Start();
            session.RegisterSystem(system);
            session.UnregisterSystem(system);

            Assert.Equal(1, system.DestroyCount);
        }

        [Fact]
        public void Update_ShouldCallSystemUpdate()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            var system = new TestSystem();

            session.Start();
            session.RegisterSystem(system);
            session.Update();

            Assert.Equal(1, system.UpdateCount);
        }

        [Fact]
        public void GetHistoricalFrame_RecentFrame_ShouldReturnFrame()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Update();
            session.Update();

            var frame = session.GetHistoricalFrame(1);

            Assert.NotNull(frame);
            Assert.Equal(1, frame.Tick);
        }

        [Fact]
        public void GetHistoricalFrame_OldFrame_ShouldReturnNull()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            
            // 鍒涘缓澶ч噺甯т娇鏃у抚琚鐩?
            for (int i = 0; i < 200; i++)
            {
                session.Update();
            }

            // 绗?1 甯у簲璇ュ凡缁忚瑕嗙洊
            var frame = session.GetHistoricalFrame(1);
            Assert.Null(frame);
        }

        [Fact]
        public void VerifyFrame_CorrectChecksum_ShouldMarkVerified()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Update();

            var frame = session.GetHistoricalFrame(1);
            Assert.NotNull(frame);
            
            long checksum = frame.CalculateChecksum();
            bool verified = false;
            session.OnFrameVerified += (tick, success) => verified = success;
            
            session.VerifyFrame(1, checksum);

            Assert.True(verified);
            Assert.True(frame.IsVerified);
        }

        [Fact]
        public void VerifyFrame_WrongChecksum_ShouldTriggerRollback()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Update();

            bool rollbackTriggered = false;
            session.OnRollback += (from, to) => rollbackTriggered = true;
            
            // 浣跨敤閿欒鐨勬牎楠屽拰
            session.VerifyFrame(1, 999999);

            Assert.True(rollbackTriggered);
        }

        [Fact]
        public void Rewind_ShouldDecreaseTick()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            for (int i = 0; i < 10; i++)
            {
                session.Update();
            }

            int tickBefore = session.CurrentTick;
            session.Rewind(5);

            Assert.Equal(tickBefore - 5, session.CurrentTick);
        }

        [Fact]
        public void Rewind_TooFar_ShouldStopAtZero()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Update();

            session.Rewind(100);

            Assert.Equal(0, session.CurrentTick);
        }

        [Fact]
        public void CreateCheckpoint_ShouldSaveState()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            for (int i = 0; i < 5; i++)
            {
                session.Update();
            }

            var checkpoint = session.CreateCheckpoint();

            Assert.Equal(5, checkpoint.Tick);
            Assert.NotNull(checkpoint.PredictedFrame);
        }

        [Fact]
        public void RestoreFromCheckpoint_ShouldRestoreState()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            for (int i = 0; i < 10; i++)
            {
                session.Update();
            }

            var checkpoint = session.CreateCheckpoint();
            
            // 缁х画鎺ㄨ繘
            for (int i = 0; i < 5; i++)
            {
                session.Update();
            }
            
            // 鎭㈠妫€鏌ョ偣
            session.RestoreFromCheckpoint(checkpoint);

            Assert.Equal(10, session.CurrentTick);
        }

        [Fact]
        public void PreviousFrame_ShouldBeSetAfterUpdate()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            Assert.Null(session.PreviousFrame);
            
            session.Update();
            
            Assert.NotNull(session.PreviousFrame);
            Assert.Equal(0, session.PreviousFrame.Tick);
        }

        [Fact]
        public void IsRollingBack_DuringRollback_ShouldBeTrue()
        {
            var registry = CreateRegistry();
            using var session = new TestSession(FP.FromRaw(FP.Raw._0_016), registry);
            
            session.Start();
            session.Update();

            bool wasRollingBack = false;
            session.OnRollback += (from, to) => wasRollingBack = session.IsRollingBack;
            
            session.VerifyFrame(1, 999999);  // 瑙﹀彂鍥炴粴

            Assert.True(wasRollingBack);
            Assert.False(session.IsRollingBack);  // 缁撴潫鍚庡簲涓?false
        }
    }
}
