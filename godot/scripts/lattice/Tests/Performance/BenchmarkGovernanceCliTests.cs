using System;
using System.IO;
using System.Linq;
using Lattice.RuntimeBenchmarks;
using Xunit;

namespace Lattice.Tests.Performance
{
    public class BenchmarkGovernanceCliTests
    {
        [Fact]
        public void ParseArguments_WithoutGovernFlags_ReturnsNonePlan()
        {
            BenchmarkGovernanceExecutionPlan plan = BenchmarkGovernanceCli.ParseArguments(new[] { "--filter", "*FPBenchmarks*" });

            Assert.Equal(BenchmarkGovernanceExecutionMode.None, plan.Mode);
            Assert.Null(plan.ReportPath);
            Assert.Null(plan.BenchmarkArgs);
        }

        [Fact]
        public void ParseArguments_GovernWithoutFilter_AppendsDefaultSessionRuntimeFilter()
        {
            BenchmarkGovernanceExecutionPlan plan = BenchmarkGovernanceCli.ParseArguments(new[] { "--govern" });

            Assert.Equal(BenchmarkGovernanceExecutionMode.RunBenchmarksAndValidateDefaultReport, plan.Mode);
            Assert.NotNull(plan.BenchmarkArgs);
            Assert.Equal(new[] { "--filter", "*SessionRuntimeBenchmarks*" }, plan.BenchmarkArgs);
        }

        [Fact]
        public void ParseArguments_GovernWithExplicitFilter_PreservesFilterAndRemovesGovernFlag()
        {
            BenchmarkGovernanceExecutionPlan plan = BenchmarkGovernanceCli.ParseArguments(
                new[] { "--govern", "--filter", "*SessionRuntimeBenchmarks*" });

            Assert.Equal(BenchmarkGovernanceExecutionMode.RunBenchmarksAndValidateDefaultReport, plan.Mode);
            Assert.NotNull(plan.BenchmarkArgs);
            Assert.Equal(new[] { "--filter", "*SessionRuntimeBenchmarks*" }, plan.BenchmarkArgs);
        }

        [Fact]
        public void ParseArguments_GovernReport_ResolvesValidationPlan()
        {
            BenchmarkGovernanceExecutionPlan plan = BenchmarkGovernanceCli.ParseArguments(
                new[] { "--govern-report", "BenchmarkDotNet.Artifacts/results/report.csv" });

            Assert.Equal(BenchmarkGovernanceExecutionMode.ValidateReport, plan.Mode);
            Assert.Equal("BenchmarkDotNet.Artifacts/results/report.csv", plan.ReportPath);
            Assert.Null(plan.BenchmarkArgs);
        }

        [Fact]
        public void ParseArguments_GovernReportWithoutPath_Throws()
        {
            Assert.Throws<ArgumentException>(() => BenchmarkGovernanceCli.ParseArguments(new[] { "--govern-report" }));
        }

        [Fact]
        public void TryExecute_GovernReportWithinBudget_ReturnsZero()
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

            try
            {
                WriteCsv(path, overBudgetScenarioId: null, overBudgetMethod: null);

                int? exitCode = BenchmarkGovernanceCli.TryExecute(new[] { "--govern-report", path });

                Assert.Equal(0, exitCode);
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
        public void TryExecute_GovernReportWhenBudgetExceeded_ReturnsOne()
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

            try
            {
                WriteCsv(
                    path,
                    overBudgetScenarioId: "checkpoint_heavy_1024_combat_save_like",
                    overBudgetMethod: "Checkpoint Save-Like Chain x16");

                int? exitCode = BenchmarkGovernanceCli.TryExecute(new[] { "--govern-report", path });

                Assert.Equal(1, exitCode);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void WriteCsv(string path, string? overBudgetScenarioId, string? overBudgetMethod)
        {
            SessionRuntimeBenchmarkGovernancePolicy policy = SessionRuntimeBenchmarkGovernancePolicy.Current;
            string[] lines = policy.Scenarios
                .SelectMany(
                    scenario => scenario.Budgets.Select(
                        budget =>
                        {
                            bool isViolation =
                                string.Equals(scenario.ScenarioId, overBudgetScenarioId, StringComparison.Ordinal) &&
                                string.Equals(budget.Method, overBudgetMethod, StringComparison.Ordinal);

                            double mean = isViolation
                                ? budget.MaxMeanMicroseconds + 10.0d
                                : budget.MaxMeanMicroseconds - 1.0d;
                            long allocated = isViolation
                                ? budget.MaxAllocatedBytes + 10
                                : budget.MaxAllocatedBytes - 1;

                            return
                                $"{Quote(budget.Method)},{scenario.EntityCount},{scenario.PayloadProfile},{mean:0.00} μs,{allocated} B";
                        }))
                .Prepend("Method,EntityCount,PayloadProfile,Mean,Allocated")
                .ToArray();

            File.WriteAllLines(path, lines);
        }

        private static string Quote(string value)
        {
            return $"\"{value}\"";
        }
    }
}
