using System;
using System.ComponentModel;
using System.Reflection;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Xunit;

namespace Lattice.Tests.ECS
{
#pragma warning disable CS0618
    public class CompatibilitySurfaceTests
    {
        [Fact]
        public void FrameSnapshot_IsMarkedAsCompatibilitySurface()
        {
            AssertCompatibilitySurface(typeof(FrameSnapshot));
        }

        [Fact]
        public void FrameSnapshotEntryPoints_AreMarkedAsCompatibilitySurface()
        {
            MethodInfo createSnapshot = typeof(Frame).GetMethod(nameof(Frame.CreateSnapshot))!;
            MethodInfo restoreFromSnapshot = typeof(Frame).GetMethod(nameof(Frame.RestoreFromSnapshot))!;

            AssertCompatibilitySurface(createSnapshot);
            AssertCompatibilitySurface(restoreFromSnapshot);
        }

        [Fact]
        public void LegacyFilters_RemainHiddenCompatibilitySurface()
        {
            AssertCompatibilitySurface(typeof(Filter<TestComponent>));
            AssertCompatibilitySurface(typeof(Filter<TestComponent, TestComponent2>));
            AssertCompatibilitySurface(typeof(Filter<TestComponent, TestComponent2, TestComponent3>));
            AssertCompatibilitySurface(typeof(FilterOptimized<TestComponent>));
            AssertCompatibilitySurface(typeof(FilterOptimized<TestComponent, TestComponent2>));
        }

        private static void AssertCompatibilitySurface(MemberInfo member)
        {
            ObsoleteAttribute? obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
            EditorBrowsableAttribute? editorBrowsable = member.GetCustomAttribute<EditorBrowsableAttribute>();

            Assert.NotNull(obsolete);
            Assert.False(obsolete!.IsError);
            Assert.NotNull(editorBrowsable);
            Assert.Equal(EditorBrowsableState.Never, editorBrowsable!.State);
        }

        private struct TestComponent : Lattice.ECS.Core.IComponent
        {
        }

        private struct TestComponent2 : Lattice.ECS.Core.IComponent
        {
        }

        private struct TestComponent3 : Lattice.ECS.Core.IComponent
        {
        }
    }
#pragma warning restore CS0618
}
