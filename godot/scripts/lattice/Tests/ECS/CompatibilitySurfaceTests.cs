using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.ECS.Session;
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

        [Fact]
        public void RuntimeCompatibilityAliases_RemainHiddenCompatibilitySurface()
        {
            MethodInfo withSessionFactory = typeof(SessionRunnerBuilder).GetMethod(nameof(SessionRunnerBuilder.WithSessionFactory))!;
            MethodInfo buildSession = typeof(SessionRunnerBuilder).GetMethod(nameof(SessionRunnerBuilder.BuildSession))!;
            PropertyInfo runnerSession = typeof(SessionRunner).GetProperty(nameof(SessionRunner.Session))!;

            AssertCompatibilitySurface(typeof(Session));
            AssertCompatibilitySurface(withSessionFactory);
            AssertCompatibilitySurface(buildSession);
            AssertCompatibilitySurface(runnerSession);
        }

        [Fact]
        public void LegacyInputCommand_RemainsHiddenCompatibilitySurface()
        {
            AssertCompatibilitySurface(typeof(IInputCommand));
        }

        [Fact]
        public void ProductionSources_DoNotConsumeCompatibilityApis()
        {
            string root = ResolveRepoRoot();

            AssertNoSourceConsumption(
                root,
                new Regex(@"(?<!Packed)\bFrameSnapshot\b", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Core\Frame.cs",
                    @"godot\scripts\lattice\ECS\Core\FrameSnapshot.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"\bFilter<", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Core\Filter.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"\bFilterOptimized<", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Framework\FilterOptimized.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"\bWithSessionFactory\s*\(", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Session\SessionRunnerBuilder.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"\bBuildSession\s*\(", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Session\SessionRunnerBuilder.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"(?<!ECS)\.Session\b", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Session\SessionRunner.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"new\s+Session\s*\(", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Session\SessionCompatibility.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@":\s*Session\b", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Session\SessionCompatibility.cs"
                });

            AssertNoSourceConsumption(
                root,
                new Regex(@"\bIInputCommand\b", RegexOptions.CultureInvariant),
                new[]
                {
                    @"godot\scripts\lattice\ECS\Session\SessionInputContract.cs"
                });
        }

        [Fact]
        public void HistoricalDocs_WithLegacyArchitectureTerms_AreExplicitlyArchived()
        {
            string root = ResolveRepoRoot();

            AssertArchived(root, @"godot\scripts\lattice\ECS\Architecture.md");
            AssertArchived(root, @"godot\scripts\lattice\Core\README.md");
            AssertArchived(root, @"godot\scripts\lattice\ECS\PERFORMANCE_OPTIMIZATION.md");
            AssertArchived(root, @"godot\scripts\lattice\ECS\Core\PHASE1_SUMMARY.md");
            AssertArchived(root, @"godot\scripts\lattice\ECS\Core\PHASE1_USAGE.md");
            AssertArchived(root, @"godot\scripts\lattice\ECS\Core\PHASE2_SUMMARY.md");
            AssertArchived(root, @"godot\scripts\lattice\ECS\Core\PHASE3_SUMMARY.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\README.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\index.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\FP-Tutorial.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\HotReloadArchitecture.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\articles\determinism.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\articles\performance.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\articles\lut-system.md");
            AssertArchived(root, @"godot\scripts\lattice\Docs\articles\simd-determinism.md");
            AssertArchived(root, @"docs\development\LatticeEvaluationReport.md");
        }

        [Fact]
        public void SystemFramework_OnlyExposesAcceptedSystemFamilyTypes()
        {
            Assembly assembly = typeof(SystemScheduler).Assembly;

            Type? systemBaseType = assembly.GetType("Lattice.ECS.Framework.SystemBase", throwOnError: false, ignoreCase: false);
            Type? systemGroupType = assembly.GetType("Lattice.ECS.Framework.SystemGroup", throwOnError: false, ignoreCase: false);

            Assert.NotNull(systemBaseType);
            Assert.NotNull(systemGroupType);
            Assert.True(systemBaseType!.IsPublic);
            Assert.True(systemGroupType!.IsPublic);
            Assert.Null(assembly.GetType("Lattice.ECS.Framework.SystemMainThreadFilter", throwOnError: false, ignoreCase: false));
            Assert.Null(assembly.GetType("Lattice.ECS.Framework.SystemThreadedFilter", throwOnError: false, ignoreCase: false));
        }

        [Fact]
        public void SystemScheduler_PublicSurface_RemainsMinimalAndFlat()
        {
            string[] publicProperties = typeof(SystemScheduler)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            string[] publicMethods = typeof(SystemScheduler)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "Boundary", "Count", "IsInitialized" }, publicProperties);
            Assert.Equal(new[] { "Add", "Clear", "Initialize", "Remove", "Shutdown", "Update" }, publicMethods);
        }

        [Fact]
        public void SessionRunner_PublicNonCompatibilitySurface_RemainsRuntimeFirst()
        {
            string[] publicProperties = typeof(SessionRunner)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.GetCustomAttribute<ObsoleteAttribute>() == null)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            string[] publicMethods = typeof(SessionRunner)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && method.GetCustomAttribute<ObsoleteAttribute>() == null)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "Context",
                    "Definition",
                    "IsRunning",
                    "LastFailure",
                    "LastResetReason",
                    "LastShutdownCause",
                    "Name",
                    "Runtime",
                    "RuntimeBoundary",
                    "RuntimeKind",
                    "RuntimeOptions",
                    "State"
                },
                publicProperties);
            Assert.Equal(new[] { "Dispose", "ResetRuntime", "Start", "Step", "Step", "Stop" }, publicMethods);
        }

        [Fact]
        public void SessionRunnerBuilder_PublicNonCompatibilitySurface_RemainsRuntimeFirst()
        {
            string[] publicMethods = typeof(SessionRunnerBuilder)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && method.GetCustomAttribute<ObsoleteAttribute>() == null)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "AddSystem",
                    "Build",
                    "BuildDefinition",
                    "BuildRuntime",
                    "ConfigureContext",
                    "ConfigureLifecycle",
                    "WithDeltaTime",
                    "WithLocalPlayerId",
                    "WithRunnerName",
                    "WithRuntimeFactory",
                    "WithRuntimeOptions"
                },
                publicMethods);
        }

        [Fact]
        public void SessionRunnerDefinition_PublicSurface_RemainsFocusedOnStableRuntimeDefinition()
        {
            string[] publicProperties = typeof(SessionRunnerDefinition)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            string[] publicMethods = typeof(SessionRunnerDefinition)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "ContextConfiguratorCount",
                    "RunnerName",
                    "RuntimeOptions",
                    "SystemCount"
                },
                publicProperties);
            Assert.Equal(new[] { "BuildRuntime", "CreateRunner" }, publicMethods);
        }

        [Fact]
        public void SessionRuntime_PublicSurface_ExposesDataAndInputBoundaries_AndHidesSizingConstants()
        {
            string[] publicProperties = typeof(SessionRuntime)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.GetCustomAttribute<ObsoleteAttribute>() == null)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            FieldInfo[] publicFields = typeof(SessionRuntime)
                .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.Contains("DataBoundary", publicProperties);
            Assert.Contains("InputBoundary", publicProperties);
            Assert.Empty(publicFields);
        }

        [Fact]
        public void SessionRuntime_PublicSurface_ExposesTickPipelineAndCurrentTickStage()
        {
            string[] publicProperties = typeof(SessionRuntime)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.GetCustomAttribute<ObsoleteAttribute>() == null)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Contains("TickPipeline", publicProperties);
            Assert.Contains("CurrentTickStage", publicProperties);
        }

        [Fact]
        public void SessionRuntimeContext_PublicSurface_RemainsFocusedOnRuntimeSharedServices()
        {
            string[] publicProperties = typeof(SessionRuntimeContext)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            string[] publicMethods = typeof(SessionRuntimeContext)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "Boundary",
                    "RunnerName",
                    "Runtime",
                    "RuntimeBoundary",
                    "RuntimeKind",
                    "RuntimeOptions",
                    "SharedObjectCount"
                },
                publicProperties);
            Assert.Equal(new[] { "Dispose", "GetRequiredShared", "RemoveShared", "SetShared", "TryGetShared" }, publicMethods);
        }

        [Fact]
        public void SessionRuntime_InternalKernel_IsSplitIntoDedicatedHelpers()
        {
            Assembly assembly = typeof(SessionRuntime).Assembly;
            Type inputStoreType = assembly.GetType("Lattice.ECS.Session.SessionInputStore", throwOnError: true)!;
            Type frameHistoryType = assembly.GetType("Lattice.ECS.Session.SessionFrameHistory", throwOnError: true)!;
            Type tickProcessorType = assembly.GetType("Lattice.ECS.Session.SessionTickProcessor", throwOnError: true)!;
            Type checkpointFactoryType = assembly.GetType("Lattice.ECS.Session.SessionCheckpointFactory", throwOnError: true)!;

            Assert.False(inputStoreType.IsPublic);
            Assert.False(frameHistoryType.IsPublic);
            Assert.False(tickProcessorType.IsPublic);
            Assert.False(checkpointFactoryType.IsPublic);

            string[] privateFields = typeof(SessionRuntime)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Contains("_inputStore", privateFields);
            Assert.Contains("_frameHistory", privateFields);
            Assert.Contains("_tickProcessor", privateFields);
            Assert.DoesNotContain("_inputBuffers", privateFields);
            Assert.DoesNotContain("_inputSetScratch", privateFields);
            Assert.DoesNotContain("_historyByTick", privateFields);
            Assert.DoesNotContain("_historyOrder", privateFields);
            Assert.DoesNotContain("_historySnapshotsByTick", privateFields);
            Assert.DoesNotContain("_historySnapshotTicks", privateFields);
            Assert.DoesNotContain("_recycledFrames", privateFields);
            Assert.DoesNotContain("_historicalScratchFrame", privateFields);
            Assert.DoesNotContain("_materializedHistoryByTick", privateFields);
            Assert.DoesNotContain("_materializedHistoryOrder", privateFields);
        }

        [Fact]
        public void InputBuffer_IsNotPartOfPublicRuntimeSurface()
        {
            Type inputBufferType = typeof(SessionRuntime).Assembly.GetType("Lattice.ECS.Session.InputBuffer", throwOnError: true)!;

            Assert.False(inputBufferType.IsPublic);
        }

        [Fact]
        public void SessionRuntimeDataDefaults_AreInternalImplementationDetail()
        {
            Type defaultsType = typeof(SessionRuntime).Assembly.GetType("Lattice.ECS.Session.SessionRuntimeDataDefaults", throwOnError: true)!;

            Assert.False(defaultsType.IsPublic);
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

        private static void AssertNoSourceConsumption(string repoRoot, Regex pattern, string[] allowedRelativePaths)
        {
            string latticeRoot = Path.Combine(repoRoot, "godot", "scripts", "lattice");
            string[] sourceFiles = Directory.GetFiles(latticeRoot, "*.cs", SearchOption.AllDirectories);

            foreach (string file in sourceFiles)
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(repoRoot, file);
                if (IsAllowedPath(relativePath, allowedRelativePaths))
                {
                    continue;
                }

                string content = File.ReadAllText(file);
                Assert.False(pattern.IsMatch(content), $"Compatibility pattern '{pattern}' should not appear in production source: {relativePath}");
            }
        }

        private static bool IsAllowedPath(string relativePath, string[] allowedRelativePaths)
        {
            string normalizedRelativePath = NormalizeRelativePath(relativePath);

            for (int i = 0; i < allowedRelativePaths.Length; i++)
            {
                if (string.Equals(
                    normalizedRelativePath,
                    NormalizeRelativePath(allowedRelativePaths[i]),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertArchived(string repoRoot, string relativePath)
        {
            string content = File.ReadAllText(Path.Combine(repoRoot, NormalizeRelativePath(relativePath)));
            Assert.Contains("历史归档文档", content, StringComparison.Ordinal);
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string ResolveRepoRoot()
        {
            string current = AppContext.BaseDirectory;
            DirectoryInfo? directory = new DirectoryInfo(current);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "godot", "scripts", "lattice", "Lattice.csproj");
                if (File.Exists(candidate))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Repository root could not be resolved from the current test base directory.");
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
