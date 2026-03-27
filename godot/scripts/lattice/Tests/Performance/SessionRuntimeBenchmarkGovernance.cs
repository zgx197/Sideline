using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Lattice.Tests.Performance
{
    /// <summary>
    /// SessionRuntime benchmark 的正式治理策略。
    /// 用于把当前重点场景从“观察项”升级为“可执行门槛”。
    /// </summary>
    public sealed class SessionRuntimeBenchmarkGovernancePolicy
    {
        private SessionRuntimeBenchmarkBudget[]? _budgets;

        public SessionRuntimeBenchmarkGovernancePolicy(
            int version,
            string updatedOn,
            IReadOnlyList<SessionRuntimeBenchmarkGovernanceScenario> scenarios)
        {
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (string.IsNullOrWhiteSpace(updatedOn))
            {
                throw new ArgumentException("UpdatedOn cannot be null or whitespace.", nameof(updatedOn));
            }

            Version = version;
            UpdatedOn = updatedOn;
            Scenarios = scenarios ?? throw new ArgumentNullException(nameof(scenarios));
        }

        public int Version { get; }

        public string UpdatedOn { get; }

        public IReadOnlyList<SessionRuntimeBenchmarkGovernanceScenario> Scenarios { get; }

        public IReadOnlyList<SessionRuntimeBenchmarkBudget> Budgets =>
            _budgets ??= Scenarios.SelectMany(static scenario => scenario.Budgets).ToArray();

        public static SessionRuntimeBenchmarkGovernancePolicy Current { get; } = CreateCurrent();

        private static SessionRuntimeBenchmarkGovernancePolicy CreateCurrent()
        {
            return new SessionRuntimeBenchmarkGovernancePolicy(
                version: 4,
                updatedOn: "2026-03-26",
                scenarios: new[]
                {
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "gameplay_256_mixed",
                        "贴近真实玩法的 MixedSerialized 中等负载画像，重点守住 update / restore / rollback / history。",
                        256,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.MixedSerialized,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Update x64", 700.0d, 35000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Create Checkpoint x128", 150.0d, 300000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Restore Checkpoint x32", 3200.0d, 1500),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Rollback + Resimulate x4", 15000.0d, 950000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Historical Cold Materialize x32", 650.0d, 5000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Historical Sequential Read 13-15 x32", 1700.0d, 6000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_mixed", "Historical Cross-Anchor Read 11-14 x32", 2000.0d, 6000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "stress_1024_mixed",
                        "MixedSerialized 大负载坏场景基线，重点防守 checkpoint / rollback / restore 与历史读取的长期回退。",
                        1024,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.MixedSerialized,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Update x64", 900.0d, 140000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Create Checkpoint x128", 450.0d, 1200000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Restore Checkpoint x32", 3200.0d, 1500),
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Rollback + Resimulate x4", 17000.0d, 3700000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Historical Cold Materialize x32", 950.0d, 5000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Historical Sequential Read 13-15 x32", 2800.0d, 6000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_mixed", "Historical Cross-Anchor Read 11-14 x32", 1900.0d, 6000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "stress_1024_raw_history",
                        "RawDense 大负载历史路径与状态恢复守门，用于确认非序列化主路径没有被整体性能治理遗漏。",
                        1024,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.RawDense,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("stress_1024_raw_history", "Restore Checkpoint x32", 3300.0d, 1500),
                            new SessionRuntimeBenchmarkBudget("stress_1024_raw_history", "Rollback + Resimulate x4", 14500.0d, 1700000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_raw_history", "Historical Cold Materialize x32", 850.0d, 5000),
                            new SessionRuntimeBenchmarkBudget("stress_1024_raw_history", "Historical Cross-Anchor Read 11-14 x32", 1900.0d, 6000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "gameplay_256_combat_mixed",
                        "更接近真实玩法的 CombatMixed 画像，覆盖生成、移动、碰撞伤害、击杀掉落与 checkpoint/rollback 重路径。",
                        256,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.CombatMixed,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Update x64", 2200.0d, 120000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Create Checkpoint x128", 550.0d, 650000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Restore Checkpoint x32", 4200.0d, 4000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Rollback + Resimulate x4", 26000.0d, 1800000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Historical Cold Materialize x32", 1300.0d, 9000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Historical Cross-Anchor Read 11-14 x32", 3200.0d, 9000),
                            new SessionRuntimeBenchmarkBudget("gameplay_256_combat_mixed", "Rollback Storm Window x4", 42000.0d, 2400000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "gameplay_1024_combat_mixed",
                        "1024 CombatMixed 主玩法画像，覆盖更接近中大型战斗密度下的 update / checkpoint / rollback / history 全路径成本。",
                        1024,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.CombatMixed,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Update x64", 8800.0d, 480000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Create Checkpoint x128", 2200.0d, 2800000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Restore Checkpoint x32", 9800.0d, 16000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Rollback + Resimulate x4", 98000.0d, 7600000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Historical Cold Materialize x32", 4200.0d, 24000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Historical Sequential Read 13-15 x32", 7800.0d, 26000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Historical Cross-Anchor Read 11-14 x32", 8600.0d, 26000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Rollback Storm Window x4", 135000.0d, 9400000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Checkpoint Save-Like Chain x16", 5200.0d, 4200000),
                            new SessionRuntimeBenchmarkBudget("gameplay_1024_combat_mixed", "Historical Replay Scrub 9-24 x16", 18500.0d, 54000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "checkpoint_heavy_1024_combat_save_like",
                        "1024 CombatMixed 的 save-like checkpoint 压力画像，重点守住频繁存档/恢复下的 checkpoint 与重模拟成本。",
                        1024,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.CombatMixed,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("checkpoint_heavy_1024_combat_save_like", "Update x64", 8800.0d, 480000),
                            new SessionRuntimeBenchmarkBudget("checkpoint_heavy_1024_combat_save_like", "Create Checkpoint x128", 2200.0d, 2800000),
                            new SessionRuntimeBenchmarkBudget("checkpoint_heavy_1024_combat_save_like", "Restore Checkpoint x32", 9800.0d, 16000),
                            new SessionRuntimeBenchmarkBudget("checkpoint_heavy_1024_combat_save_like", "Rollback + Resimulate x4", 98000.0d, 7600000),
                            new SessionRuntimeBenchmarkBudget("checkpoint_heavy_1024_combat_save_like", "Checkpoint Save-Like Chain x16", 5200.0d, 4200000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "history_read_1024_combat_replay",
                        "1024 CombatMixed 的 replay-like history 读取画像，覆盖 sampled snapshot 重建、cold materialize 与 cross-anchor 读取。",
                        1024,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.CombatMixed,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("history_read_1024_combat_replay", "Historical Rebuild From Sampled Snapshot x32", 3600.0d, 22000),
                            new SessionRuntimeBenchmarkBudget("history_read_1024_combat_replay", "Historical Cold Materialize x32", 4200.0d, 24000),
                            new SessionRuntimeBenchmarkBudget("history_read_1024_combat_replay", "Historical Sequential Read 13-15 x32", 7800.0d, 26000),
                            new SessionRuntimeBenchmarkBudget("history_read_1024_combat_replay", "Historical Cross-Anchor Read 11-14 x32", 8600.0d, 26000),
                            new SessionRuntimeBenchmarkBudget("history_read_1024_combat_replay", "Historical Replay Scrub 9-24 x16", 18500.0d, 54000)
                        }),
                    new SessionRuntimeBenchmarkGovernanceScenario(
                        "rollback_storm_256_combat_window",
                        "CombatMixed 负载下的连续迟到输入窗口回滚画像，重点防守 rollback storm 与相关 restore/history 成本。",
                        256,
                        SessionRuntimeBenchmarks.BenchmarkPayloadProfile.CombatMixed,
                        new[]
                        {
                            new SessionRuntimeBenchmarkBudget("rollback_storm_256_combat_window", "Restore Checkpoint x32", 4200.0d, 4000),
                            new SessionRuntimeBenchmarkBudget("rollback_storm_256_combat_window", "Rollback + Resimulate x4", 26000.0d, 1800000),
                            new SessionRuntimeBenchmarkBudget("rollback_storm_256_combat_window", "Historical Cross-Anchor Read 11-14 x32", 3200.0d, 9000),
                            new SessionRuntimeBenchmarkBudget("rollback_storm_256_combat_window", "Rollback Storm Window x4", 42000.0d, 2400000)
                        })
                });
        }
    }

    public sealed class SessionRuntimeBenchmarkGovernanceScenario
    {
        public SessionRuntimeBenchmarkGovernanceScenario(
            string scenarioId,
            string description,
            int entityCount,
            SessionRuntimeBenchmarks.BenchmarkPayloadProfile payloadProfile,
            IReadOnlyList<SessionRuntimeBenchmarkBudget> budgets)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("ScenarioId cannot be null or whitespace.", nameof(scenarioId));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description cannot be null or whitespace.", nameof(description));
            }

            if (entityCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityCount));
            }

            ScenarioId = scenarioId;
            Description = description;
            EntityCount = entityCount;
            PayloadProfile = payloadProfile;
            Budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        }

        public string ScenarioId { get; }

        public string Description { get; }

        public int EntityCount { get; }

        public SessionRuntimeBenchmarks.BenchmarkPayloadProfile PayloadProfile { get; }

        public IReadOnlyList<SessionRuntimeBenchmarkBudget> Budgets { get; }
    }

    public sealed class SessionRuntimeBenchmarkBudget
    {
        public SessionRuntimeBenchmarkBudget(
            string scenarioId,
            string method,
            double maxMeanMicroseconds,
            long maxAllocatedBytes)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("ScenarioId cannot be null or whitespace.", nameof(scenarioId));
            }

            if (string.IsNullOrWhiteSpace(method))
            {
                throw new ArgumentException("Method cannot be null or whitespace.", nameof(method));
            }

            if (maxMeanMicroseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMeanMicroseconds));
            }

            if (maxAllocatedBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAllocatedBytes));
            }

            ScenarioId = scenarioId;
            Method = method;
            MaxMeanMicroseconds = maxMeanMicroseconds;
            MaxAllocatedBytes = maxAllocatedBytes;
        }

        public string ScenarioId { get; }

        public string Method { get; }

        public double MaxMeanMicroseconds { get; }

        public long MaxAllocatedBytes { get; }
    }

    public sealed class SessionRuntimeBenchmarkMeasurement
    {
        public SessionRuntimeBenchmarkMeasurement(
            string method,
            int entityCount,
            SessionRuntimeBenchmarks.BenchmarkPayloadProfile payloadProfile,
            double meanMicroseconds,
            long allocatedBytes)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                throw new ArgumentException("Method cannot be null or whitespace.", nameof(method));
            }

            if (entityCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityCount));
            }

            if (meanMicroseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(meanMicroseconds));
            }

            if (allocatedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(allocatedBytes));
            }

            Method = method;
            EntityCount = entityCount;
            PayloadProfile = payloadProfile;
            MeanMicroseconds = meanMicroseconds;
            AllocatedBytes = allocatedBytes;
        }

        public string Method { get; }

        public int EntityCount { get; }

        public SessionRuntimeBenchmarks.BenchmarkPayloadProfile PayloadProfile { get; }

        public double MeanMicroseconds { get; }

        public long AllocatedBytes { get; }
    }

    public sealed class SessionRuntimeBenchmarkGovernanceViolation
    {
        public SessionRuntimeBenchmarkGovernanceViolation(string scenarioId, string message)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("ScenarioId cannot be null or whitespace.", nameof(scenarioId));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
            }

            ScenarioId = scenarioId;
            Message = message;
        }

        public string ScenarioId { get; }

        public string Message { get; }
    }

    public sealed class SessionRuntimeBenchmarkGovernanceResult
    {
        public SessionRuntimeBenchmarkGovernanceResult(
            SessionRuntimeBenchmarkGovernancePolicy policy,
            IReadOnlyList<SessionRuntimeBenchmarkGovernanceViolation> violations)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Violations = violations ?? throw new ArgumentNullException(nameof(violations));
        }

        public SessionRuntimeBenchmarkGovernancePolicy Policy { get; }

        public IReadOnlyList<SessionRuntimeBenchmarkGovernanceViolation> Violations { get; }

        public bool IsSuccess => Violations.Count == 0;

        public string ToDisplayText()
        {
            var builder = new StringBuilder();
            builder.Append("SessionRuntime benchmark 治理校验：");
            builder.Append(IsSuccess ? "通过" : "失败");
            builder.Append(" | policy v");
            builder.Append(Policy.Version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | updated ");
            builder.Append(Policy.UpdatedOn);

            if (IsSuccess)
            {
                builder.Append(" | scenarios=");
                builder.Append(Policy.Scenarios.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | budgets=");
                builder.Append(Policy.Budgets.Count.ToString(CultureInfo.InvariantCulture));
                return builder.ToString();
            }

            for (int i = 0; i < Violations.Count; i++)
            {
                builder.AppendLine();
                builder.Append("- [");
                builder.Append(Violations[i].ScenarioId);
                builder.Append("] ");
                builder.Append(Violations[i].Message);
            }

            return builder.ToString();
        }
    }

    public static class SessionRuntimeBenchmarkGovernance
    {
        private const string MethodColumnName = "Method";
        private const string EntityCountColumnName = "EntityCount";
        private const string PayloadProfileColumnName = "PayloadProfile";
        private const string MeanColumnName = "Mean";
        private const string AllocatedColumnName = "Allocated";

        public static SessionRuntimeBenchmarkGovernanceResult Validate(
            IReadOnlyList<SessionRuntimeBenchmarkMeasurement> measurements,
            SessionRuntimeBenchmarkGovernancePolicy? policy = null)
        {
            ArgumentNullException.ThrowIfNull(measurements);

            SessionRuntimeBenchmarkGovernancePolicy effectivePolicy = policy ?? SessionRuntimeBenchmarkGovernancePolicy.Current;
            var violations = new List<SessionRuntimeBenchmarkGovernanceViolation>();
            var measurementByKey = new Dictionary<string, SessionRuntimeBenchmarkMeasurement>(StringComparer.Ordinal);

            for (int i = 0; i < measurements.Count; i++)
            {
                SessionRuntimeBenchmarkMeasurement measurement = measurements[i];
                measurementByKey[CreateKey(measurement.Method, measurement.EntityCount, measurement.PayloadProfile)] = measurement;
            }

            IReadOnlyList<SessionRuntimeBenchmarkBudget> budgets = effectivePolicy.Budgets;
            for (int i = 0; i < budgets.Count; i++)
            {
                SessionRuntimeBenchmarkBudget budget = budgets[i];
                SessionRuntimeBenchmarkGovernanceScenario scenario = FindScenario(effectivePolicy, budget.ScenarioId);
                string key = CreateKey(budget.Method, scenario.EntityCount, scenario.PayloadProfile);

                if (!measurementByKey.TryGetValue(key, out SessionRuntimeBenchmarkMeasurement? measurement))
                {
                    violations.Add(new SessionRuntimeBenchmarkGovernanceViolation(
                        budget.ScenarioId,
                        $"缺少基准结果：Method={budget.Method}, EntityCount={scenario.EntityCount}, PayloadProfile={scenario.PayloadProfile}."));
                    continue;
                }

                if (measurement.MeanMicroseconds > budget.MaxMeanMicroseconds)
                {
                    violations.Add(new SessionRuntimeBenchmarkGovernanceViolation(
                        budget.ScenarioId,
                        $"{measurement.Method} 平均耗时 {measurement.MeanMicroseconds.ToString("F2", CultureInfo.InvariantCulture)} us 超过门槛 {budget.MaxMeanMicroseconds.ToString("F2", CultureInfo.InvariantCulture)} us。"));
                }

                if (measurement.AllocatedBytes > budget.MaxAllocatedBytes)
                {
                    violations.Add(new SessionRuntimeBenchmarkGovernanceViolation(
                        budget.ScenarioId,
                        $"{measurement.Method} 分配 {measurement.AllocatedBytes.ToString(CultureInfo.InvariantCulture)} B 超过门槛 {budget.MaxAllocatedBytes.ToString(CultureInfo.InvariantCulture)} B。"));
                }
            }

            return new SessionRuntimeBenchmarkGovernanceResult(effectivePolicy, violations);
        }

        public static IReadOnlyList<SessionRuntimeBenchmarkMeasurement> LoadMeasurementsFromCsv(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                throw new ArgumentException("CSV path cannot be null or whitespace.", nameof(csvPath));
            }

            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException("Benchmark governance CSV report was not found.", csvPath);
            }

            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length <= 1)
            {
                return Array.Empty<SessionRuntimeBenchmarkMeasurement>();
            }

            IReadOnlyList<string> header = ParseCsvLine(lines[0]);
            int methodIndex = FindColumnIndex(header, MethodColumnName);
            int entityCountIndex = FindColumnIndex(header, EntityCountColumnName);
            int payloadProfileIndex = FindColumnIndex(header, PayloadProfileColumnName);
            int meanIndex = FindColumnIndex(header, MeanColumnName);
            int allocatedIndex = FindColumnIndex(header, AllocatedColumnName);

            var measurements = new List<SessionRuntimeBenchmarkMeasurement>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                IReadOnlyList<string> cells = ParseCsvLine(lines[i]);
                measurements.Add(
                    new SessionRuntimeBenchmarkMeasurement(
                        TrimBenchmarkCell(cells[methodIndex]),
                        int.Parse(cells[entityCountIndex], CultureInfo.InvariantCulture),
                        Enum.Parse<SessionRuntimeBenchmarks.BenchmarkPayloadProfile>(cells[payloadProfileIndex], ignoreCase: false),
                        ParseMicroseconds(cells[meanIndex]),
                        ParseBytes(cells[allocatedIndex])));
            }

            return measurements;
        }

        public static string GetDefaultCsvReportPath(string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                throw new ArgumentException("Working directory cannot be null or whitespace.", nameof(workingDirectory));
            }

            string fileName = $"{typeof(SessionRuntimeBenchmarks).FullName}-report.csv";
            DirectoryInfo? directory = new DirectoryInfo(workingDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "BenchmarkDotNet.Artifacts", "results", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return Path.Combine(workingDirectory, "BenchmarkDotNet.Artifacts", "results", fileName);
        }

        private static SessionRuntimeBenchmarkGovernanceScenario FindScenario(
            SessionRuntimeBenchmarkGovernancePolicy policy,
            string scenarioId)
        {
            for (int i = 0; i < policy.Scenarios.Count; i++)
            {
                if (string.Equals(policy.Scenarios[i].ScenarioId, scenarioId, StringComparison.Ordinal))
                {
                    return policy.Scenarios[i];
                }
            }

            throw new InvalidOperationException($"Scenario '{scenarioId}' is not defined in the current benchmark governance policy.");
        }

        private static int FindColumnIndex(IReadOnlyList<string> header, string columnName)
        {
            for (int i = 0; i < header.Count; i++)
            {
                if (string.Equals(header[i], columnName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"Benchmark governance CSV is missing required column '{columnName}'.");
        }

        private static IReadOnlyList<string> ParseCsvLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    cells.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            cells.Add(current.ToString());
            return cells;
        }

        private static string TrimBenchmarkCell(string value)
        {
            return value.Trim().Trim('\'');
        }

        private static double ParseMicroseconds(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.EndsWith("μs", StringComparison.Ordinal))
            {
                return ParseInvariantNumber(trimmed[..^2]);
            }

            if (trimmed.EndsWith("ms", StringComparison.Ordinal))
            {
                return ParseInvariantNumber(trimmed[..^2]) * 1000.0d;
            }

            if (trimmed.EndsWith("ns", StringComparison.Ordinal))
            {
                return ParseInvariantNumber(trimmed[..^2]) / 1000.0d;
            }

            throw new InvalidOperationException($"Unsupported benchmark time unit: '{value}'.");
        }

        private static long ParseBytes(string value)
        {
            string trimmed = value.Trim();
            if (trimmed == "-")
            {
                return 0;
            }

            if (trimmed.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                return (long)System.Math.Round(ParseInvariantNumber(trimmed[..^2]) * 1024.0d, MidpointRounding.AwayFromZero);
            }

            if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                return (long)System.Math.Round(ParseInvariantNumber(trimmed[..^2]) * 1024.0d * 1024.0d, MidpointRounding.AwayFromZero);
            }

            if (trimmed.EndsWith("B", StringComparison.Ordinal))
            {
                return (long)System.Math.Round(ParseInvariantNumber(trimmed[..^1]), MidpointRounding.AwayFromZero);
            }

            throw new InvalidOperationException($"Unsupported benchmark allocation unit: '{value}'.");
        }

        private static double ParseInvariantNumber(string value)
        {
            string normalized = value.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
            return double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string CreateKey(
            string method,
            int entityCount,
            SessionRuntimeBenchmarks.BenchmarkPayloadProfile payloadProfile)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{method}|{entityCount}|{payloadProfile}");
        }
    }
}
