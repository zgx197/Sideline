using System;
using Lattice.ECS.Session;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class SessionRunnerTests
    {
        [Fact]
        public void Start_Step_Stop_DrivesSessionLifecycle()
        {
            using var session = new Session(FP.One);
            using var runner = new SessionRunner(session);

            runner.Start();
            runner.Step();
            runner.Step(2);
            runner.Stop();

            Assert.False(runner.IsRunning);
            Assert.Equal(3, session.CurrentTick);
        }

        [Fact]
        public void Start_AndStop_AreIdempotent()
        {
            using var session = new Session(FP.One);
            using var runner = new SessionRunner(session);

            runner.Start();
            runner.Start();

            Assert.True(runner.IsRunning);

            runner.Stop();
            runner.Stop();

            Assert.False(runner.IsRunning);
        }

        [Fact]
        public void Step_WhenRunnerNotStarted_Throws()
        {
            using var session = new Session(FP.One);
            using var runner = new SessionRunner(session);

            Assert.Throws<InvalidOperationException>(() => runner.Step());
        }

        [Fact]
        public void Step_WithNegativeCount_Throws()
        {
            using var session = new Session(FP.One);
            using var runner = new SessionRunner(session);

            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Step(-1));
        }

        [Fact]
        public void Dispose_PreventsFurtherLifecycleCalls()
        {
            using var session = new Session(FP.One);
            var runner = new SessionRunner(session);

            runner.Start();
            runner.Dispose();

            Assert.False(runner.IsRunning);
            Assert.Throws<ObjectDisposedException>(() => runner.Start());
            Assert.Throws<ObjectDisposedException>(() => runner.Step());
            Assert.Throws<ObjectDisposedException>(() => runner.Stop());
        }
    }
}
