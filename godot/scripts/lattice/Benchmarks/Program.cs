using BenchmarkDotNet.Running;

int? governanceExitCode = Lattice.RuntimeBenchmarks.BenchmarkGovernanceCli.TryExecute(args);
if (governanceExitCode.HasValue)
{
    return governanceExitCode.Value;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
