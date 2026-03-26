using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Xunit;

namespace Lattice.Tests.Performance
{
    public class SessionRuntimeBenchmarkGovernanceTests
    {
        [Fact]
        public void CurrentPolicy_CoversPriority18FormalScenarios()
        {
            SessionRuntimeBenchmarkGovernancePolicy policy = SessionRuntimeBenchmarkGovernancePolicy.Current;

            Assert.Equal(4, policy.Version);
            Assert.Equal("2026-03-26", policy.UpdatedOn);
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "gameplay_256_mixed");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "stress_1024_mixed");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "stress_1024_raw_history");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "gameplay_256_combat_mixed");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "gameplay_1024_combat_mixed");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "checkpoint_heavy_1024_combat_save_like");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "history_read_1024_combat_replay");
            Assert.Contains(policy.Scenarios, scenario => scenario.ScenarioId == "rollback_storm_256_combat_window");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "stress_1024_mixed" && budget.Method == "Rollback + Resimulate x4");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "gameplay_256_mixed" && budget.Method == "Historical Cross-Anchor Read 11-14 x32");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "gameplay_256_combat_mixed" && budget.Method == "Rollback Storm Window x4");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "gameplay_1024_combat_mixed" && budget.Method == "Rollback Storm Window x4");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "checkpoint_heavy_1024_combat_save_like" && budget.Method == "Create Checkpoint x128");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "history_read_1024_combat_replay" && budget.Method == "Historical Rebuild From Sampled Snapshot x32");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "checkpoint_heavy_1024_combat_save_like" && budget.Method == "Checkpoint Save-Like Chain x16");
            Assert.Contains(
                policy.Budgets,
                budget => budget.ScenarioId == "history_read_1024_combat_replay" && budget.Method == "Historical Replay Scrub 9-24 x16");
        }

        [Fact]
        public void CurrentPolicy_BudgetedMethodsMatchBenchmarkSurface()
        {
            SessionRuntimeBenchmarkGovernancePolicy policy = SessionRuntimeBenchmarkGovernancePolicy.Current;
            var benchmarkMethods = typeof(SessionRuntimeBenchmarks)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(
                    method => new
                    {
                        Method = method,
                        Attribute = method.GetCustomAttribute<BenchmarkAttribute>()
                    })
                .Where(item => item.Attribute != null)
                .Select(
                    item => string.IsNullOrWhiteSpace(item.Attribute!.Description)
                        ? item.Method.Name
                        : item.Attribute.Description)
                .ToHashSet(StringComparer.Ordinal);

            Assert.All(policy.Budgets, budget => Assert.Contains(budget.Method, benchmarkMethods));
        }

        [Fact]
        public void LoadMeasurementsFromCsv_ParsesBenchmarkDotNetUnitsAndQuotedValues()
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

            try
            {
                File.WriteAllText(
                    path,
                    "Method,EntityCount,PayloadProfile,Mean,Allocated" + Environment.NewLine +
                    "'Update x64',256,MixedSerialized,532.87 μs,30726 B" + Environment.NewLine +
                    "\"Restore Checkpoint x32\",1024,RawDense,\"2,703.00 μs\",1132 B" + Environment.NewLine);

                var measurements = SessionRuntimeBenchmarkGovernance.LoadMeasurementsFromCsv(path);

                Assert.Equal(2, measurements.Count);
                Assert.Equal("Update x64", measurements[0].Method);
                Assert.Equal(256, measurements[0].EntityCount);
                Assert.Equal(SessionRuntimeBenchmarks.BenchmarkPayloadProfile.MixedSerialized, measurements[0].PayloadProfile);
                Assert.Equal(532.87d, measurements[0].MeanMicroseconds, 2);
                Assert.Equal(30726, measurements[0].AllocatedBytes);

                Assert.Equal("Restore Checkpoint x32", measurements[1].Method);
                Assert.Equal(1024, measurements[1].EntityCount);
                Assert.Equal(2703.0d, measurements[1].MeanMicroseconds, 2);
                Assert.Equal(1132, measurements[1].AllocatedBytes);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void Validate_WhenMeasurementsStayWithinBudget_Passes()
        {
            SessionRuntimeBenchmarkGovernancePolicy policy = SessionRuntimeBenchmarkGovernancePolicy.Current;
            SessionRuntimeBenchmarkMeasurement[] measurements = policy.Scenarios
                .SelectMany(
                    scenario => scenario.Budgets.Select(
                        budget => new SessionRuntimeBenchmarkMeasurement(
                            budget.Method,
                            scenario.EntityCount,
                            scenario.PayloadProfile,
                            budget.MaxMeanMicroseconds - 1.0d,
                            budget.MaxAllocatedBytes - 1)))
                .ToArray();

            SessionRuntimeBenchmarkGovernanceResult result = SessionRuntimeBenchmarkGovernance.Validate(measurements, policy);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void Validate_WhenBudgetIsExceeded_FailsWithDetailedViolation()
        {
            SessionRuntimeBenchmarkGovernancePolicy policy = SessionRuntimeBenchmarkGovernancePolicy.Current;
            SessionRuntimeBenchmarkGovernanceScenario scenario = policy.Scenarios.Single(item => item.ScenarioId == "gameplay_256_mixed");
            SessionRuntimeBenchmarkBudget targetBudget = scenario.Budgets.Single(item => item.Method == "Restore Checkpoint x32");

            SessionRuntimeBenchmarkMeasurement[] measurements = policy.Scenarios
                .SelectMany(
                    currentScenario => currentScenario.Budgets.Select(
                        budget => new SessionRuntimeBenchmarkMeasurement(
                            budget.Method,
                            currentScenario.EntityCount,
                            currentScenario.PayloadProfile,
                            budget.Method == targetBudget.Method && currentScenario.ScenarioId == scenario.ScenarioId
                                ? budget.MaxMeanMicroseconds + 100.0d
                                : budget.MaxMeanMicroseconds - 1.0d,
                            budget.Method == targetBudget.Method && currentScenario.ScenarioId == scenario.ScenarioId
                                ? budget.MaxAllocatedBytes + 100
                                : budget.MaxAllocatedBytes - 1)))
                .ToArray();

            SessionRuntimeBenchmarkGovernanceResult result = SessionRuntimeBenchmarkGovernance.Validate(measurements, policy);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Violations, violation => violation.ScenarioId == "gameplay_256_mixed" && violation.Message.Contains("平均耗时", StringComparison.Ordinal));
            Assert.Contains(result.Violations, violation => violation.ScenarioId == "gameplay_256_mixed" && violation.Message.Contains("分配", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_WhenBudgetedMeasurementIsMissing_Fails()
        {
            SessionRuntimeBenchmarkGovernancePolicy policy = SessionRuntimeBenchmarkGovernancePolicy.Current;
            SessionRuntimeBenchmarkGovernanceResult result = SessionRuntimeBenchmarkGovernance.Validate(Array.Empty<SessionRuntimeBenchmarkMeasurement>(), policy);

            Assert.False(result.IsSuccess);
            Assert.Equal(policy.Budgets.Count, result.Violations.Count);
            Assert.All(result.Violations, violation => Assert.Contains("缺少基准结果", violation.Message, StringComparison.Ordinal));
        }
    }
}
