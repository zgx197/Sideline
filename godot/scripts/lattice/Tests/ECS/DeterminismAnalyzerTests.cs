using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lattice.Analyzers;
using Lattice.Math;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Lattice.Tests.ECS
{
    public class DeterminismAnalyzerTests
    {
        private static readonly ImmutableArray<MetadataReference> MetadataReferences = BuildMetadataReferences();

        [Fact]
        public async Task MutableStaticField_InSystemType_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file sealed class BadSystem : ISystem
{
    private static int _cache;

    public void OnInit(Frame frame) { }
    public void OnUpdate(Frame frame, FP deltaTime) { }
    public void OnDestroy(Frame frame) { }
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MutableStaticFieldId);
        }

        [Fact]
        public async Task ReadonlyStaticField_InSystemType_IsAllowed()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file sealed class GoodSystem : ISystem
{
    private static readonly int Cache = 7;

    public void OnInit(Frame frame) { }
    public void OnUpdate(Frame frame, FP deltaTime) { }
    public void OnDestroy(Frame frame) { }
}
""");

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MutableStaticFieldId);
        }

        [Fact]
        public async Task MutableStaticField_InRuntimeType_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Session;
using Lattice.Math;

file sealed class BadRuntime : SessionRuntime
{
    private static readonly SessionRuntimeBoundary Boundary = new(
        SessionRuntimeKind.LocalAuthoritative,
        SessionRuntimeCapability.LocalPredictionStep,
        UnsupportedSessionRuntimeCapability.None);

    private static int _cachedTick;

    public BadRuntime(FP deltaTime) : base(deltaTime) { }

    public override SessionRuntimeBoundary RuntimeBoundary => Boundary;
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MutableStaticFieldId);
        }

        [Fact]
        public async Task GuidNewGuid_InSystemInit_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using System;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file sealed class BootSystem : ISystem
{
    public void OnInit(Frame frame)
    {
        _ = Guid.NewGuid();
    }

    public void OnUpdate(Frame frame, FP deltaTime) { }
    public void OnDestroy(Frame frame) { }
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.NondeterministicApiId);
        }

        [Fact]
        public async Task DateTimeUtcNow_InSystemUpdate_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using System;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file sealed class TimeSystem : ISystem
{
    public void OnInit(Frame frame) { }

    public void OnUpdate(Frame frame, FP deltaTime)
    {
        _ = DateTime.UtcNow;
    }

    public void OnDestroy(Frame frame) { }
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.NondeterministicApiId);
        }

        [Fact]
        public async Task Random_InApplyInputSet_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using System;
using Lattice.ECS.Core;
using Lattice.ECS.Session;
using Lattice.Math;

file sealed class BadRuntime : SessionRuntime
{
    private static readonly SessionRuntimeBoundary Boundary = new(
        SessionRuntimeKind.LocalAuthoritative,
        SessionRuntimeCapability.LocalPredictionStep,
        UnsupportedSessionRuntimeCapability.None);

    public BadRuntime(FP deltaTime) : base(deltaTime) { }

    public override SessionRuntimeBoundary RuntimeBoundary => Boundary;

    protected override void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
    {
        var random = new Random();
        _ = random.Next();
    }
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.NondeterministicApiId);
        }

        [Fact]
        public async Task StructuralFrameApi_WithoutExplicitContract_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file sealed class SpawnSystem : ISystem
{
    public void OnInit(Frame frame) { }

    public void OnUpdate(Frame frame, FP deltaTime)
    {
        frame.CreateEntity();
    }

    public void OnDestroy(Frame frame) { }
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MissingContractId);
        }

        [Fact]
        public async Task StructuralFrameApi_WithExplicitContract_IsAllowed()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file sealed class SpawnSystem : ISystem
{
    public SystemAuthoringContract Contract => new(
        SystemFrameAccess.ReadWrite,
        SystemGlobalAccess.None,
        SystemStructuralChangeAccess.Deferred);

    public void OnInit(Frame frame) { }

    public void OnUpdate(Frame frame, FP deltaTime)
    {
        frame.CreateEntity();
    }

    public void OnDestroy(Frame frame) { }
}
""");

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MissingContractId);
        }

        [Fact]
        public async Task GlobalReadApi_WithoutExplicitContract_IsRejected()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file struct GlobalState : IComponent { }

file sealed class GlobalSystem : ISystem
{
    public void OnInit(Frame frame) { }

    public void OnUpdate(Frame frame, FP deltaTime)
    {
        frame.TryGetGlobal(out GlobalState state);
    }

    public void OnDestroy(Frame frame) { }
}
""");

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MissingContractId);
        }

        [Fact]
        public async Task GlobalReadApi_WithReadOnlyContract_IsAllowed()
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

file struct GlobalState : IComponent { }

file sealed class GlobalSystem : ISystem
{
    public SystemAuthoringContract Contract => new(
        SystemFrameAccess.ReadOnly,
        SystemGlobalAccess.ReadOnly);

    public void OnInit(Frame frame) { }

    public void OnUpdate(Frame frame, FP deltaTime)
    {
        frame.TryGetGlobal(out GlobalState state);
    }

    public void OnDestroy(Frame frame) { }
}
""");

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == DeterminismGuardAnalyzer.MissingContractId);
        }

        private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp11));
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: "DeterminismAnalyzerFixture",
                syntaxTrees: new[] { syntaxTree },
                references: MetadataReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismGuardAnalyzer());
            CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
            ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
            return diagnostics.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        }

        private static ImmutableArray<MetadataReference> BuildMetadataReferences()
        {
            string trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
                ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is not available.");

            var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                references[path] = MetadataReference.CreateFromFile(path);
            }

            references[typeof(FP).Assembly.Location] = MetadataReference.CreateFromFile(typeof(FP).Assembly.Location);
            references[typeof(DeterminismGuardAnalyzer).Assembly.Location] = MetadataReference.CreateFromFile(typeof(DeterminismGuardAnalyzer).Assembly.Location);
            return references.Values.ToImmutableArray();
        }
    }
}
