using System;
using System.Collections.Generic;
using BenchmarkDotNet.Running;
using Lattice.Tests.Performance;

namespace Lattice.RuntimeBenchmarks
{
    internal static class BenchmarkGovernanceCli
    {
        private const string GovernFlag = "--govern";
        private const string GovernReportFlag = "--govern-report";

        public static int? TryExecute(string[] args)
        {
            BenchmarkGovernanceExecutionPlan plan = ParseArguments(args);
            if (plan.Mode == BenchmarkGovernanceExecutionMode.None)
            {
                return null;
            }

            if (plan.Mode == BenchmarkGovernanceExecutionMode.ValidateReport)
            {
                return ValidateReport(plan.ReportPath!);
            }

            BenchmarkSwitcher.FromAssembly(typeof(BenchmarkGovernanceCli).Assembly).Run(plan.BenchmarkArgs!);

            string reportPath = SessionRuntimeBenchmarkGovernance.GetDefaultCsvReportPath(Environment.CurrentDirectory);
            return ValidateReport(reportPath);
        }

        internal static BenchmarkGovernanceExecutionPlan ParseArguments(string[]? args)
        {
            if (args == null || args.Length == 0)
            {
                return BenchmarkGovernanceExecutionPlan.None;
            }

            if (Array.Exists(args, static arg => string.Equals(arg, GovernReportFlag, StringComparison.Ordinal)))
            {
                string csvPath = GetGovernReportPath(args);
                return BenchmarkGovernanceExecutionPlan.ForReport(csvPath);
            }

            if (!Array.Exists(args, static arg => string.Equals(arg, GovernFlag, StringComparison.Ordinal)))
            {
                return BenchmarkGovernanceExecutionPlan.None;
            }

            string[] benchmarkArgs = FilterGovernArgs(args);
            if (!ContainsFilterArgument(benchmarkArgs))
            {
                benchmarkArgs = AppendDefaultFilter(benchmarkArgs);
            }

            return BenchmarkGovernanceExecutionPlan.ForBenchmarkRun(benchmarkArgs);
        }

        private static int ValidateReport(string csvPath)
        {
            IReadOnlyList<SessionRuntimeBenchmarkMeasurement> measurements =
                SessionRuntimeBenchmarkGovernance.LoadMeasurementsFromCsv(csvPath);
            SessionRuntimeBenchmarkGovernanceResult result = SessionRuntimeBenchmarkGovernance.Validate(measurements);
            Console.WriteLine(result.ToDisplayText());
            return result.IsSuccess ? 0 : 1;
        }

        private static string GetGovernReportPath(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], GovernReportFlag, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("--govern-report 需要提供 BenchmarkDotNet 导出的 CSV 路径。", nameof(args));
                }

                return args[i + 1];
            }

            throw new InvalidOperationException("Govern report path could not be resolved.");
        }

        private static string[] FilterGovernArgs(string[] args)
        {
            var filtered = new List<string>(args.Length);

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], GovernFlag, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(args[i], GovernReportFlag, StringComparison.Ordinal))
                {
                    i++;
                    continue;
                }

                filtered.Add(args[i]);
            }

            return filtered.ToArray();
        }

        private static bool ContainsFilterArgument(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--filter", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] AppendDefaultFilter(string[] args)
        {
            string[] expanded = new string[args.Length + 2];
            Array.Copy(args, expanded, args.Length);
            expanded[^2] = "--filter";
            expanded[^1] = "*SessionRuntimeBenchmarks*";
            return expanded;
        }
    }

    internal enum BenchmarkGovernanceExecutionMode
    {
        None,
        ValidateReport,
        RunBenchmarksAndValidateDefaultReport
    }

    internal readonly record struct BenchmarkGovernanceExecutionPlan(
        BenchmarkGovernanceExecutionMode Mode,
        string? ReportPath,
        string[]? BenchmarkArgs)
    {
        public static BenchmarkGovernanceExecutionPlan None { get; } =
            new(BenchmarkGovernanceExecutionMode.None, null, null);

        public static BenchmarkGovernanceExecutionPlan ForReport(string reportPath)
        {
            return new BenchmarkGovernanceExecutionPlan(
                BenchmarkGovernanceExecutionMode.ValidateReport,
                reportPath,
                null);
        }

        public static BenchmarkGovernanceExecutionPlan ForBenchmarkRun(string[] benchmarkArgs)
        {
            return new BenchmarkGovernanceExecutionPlan(
                BenchmarkGovernanceExecutionMode.RunBenchmarksAndValidateDefaultReport,
                null,
                benchmarkArgs);
        }
    }
}
