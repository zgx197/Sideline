using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Lattice.Analyzers
{
    /// <summary>
    /// 为确定性运行时提供最小静态守卫：
    /// - 禁止系统 / runtime 类型引入可变静态字段
    /// - 禁止在正式模拟入口中调用典型非确定性 API
    /// - 禁止系统在未显式声明或错误声明 Contract 时使用受守卫的 Frame API
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeterminismGuardAnalyzer : DiagnosticAnalyzer
    {
        public const string MutableStaticFieldId = "LATTICE100";
        public const string NondeterministicApiId = "LATTICE101";
        public const string MissingContractId = "LATTICE102";

        private static readonly DiagnosticDescriptor MutableStaticFieldRule = new(
            MutableStaticFieldId,
            "确定性模拟类型禁止可变静态字段",
            "类型 '{0}' 包含可变静态字段 '{1}'，会引入跨 Tick / 跨 Session 的隐式共享状态",
            "Determinism",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "ISystem 与 SessionRuntime 相关类型不应通过可变静态字段缓存运行态数据。");

        private static readonly DiagnosticDescriptor NondeterministicApiRule = new(
            NondeterministicApiId,
            "正式模拟入口禁止非确定性 API",
            "方法 '{0}' 调用了非确定性 API '{1}'",
            "Determinism",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "系统 OnUpdate 与输入应用等正式模拟入口不应依赖时间、随机数、异步调度等非确定性 API。");

        private static readonly DiagnosticDescriptor MissingContractRule = new(
            MissingContractId,
            "系统使用受守卫 Frame API 时必须显式声明兼容 Contract",
            "系统 '{0}' 使用了受守卫的 Frame API '{1}'，但 Contract 未显式声明或未授予所需权限 '{2}'",
            "Determinism",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "使用 global state 或结构性修改相关 API 的系统必须显式声明兼容的 SystemAuthoringContract。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MutableStaticFieldRule, NondeterministicApiRule, MissingContractRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(static startContext =>
            {
                Compilation compilation = startContext.Compilation;
                INamedTypeSymbol? systemType = compilation.GetTypeByMetadataName("Lattice.ECS.Framework.ISystem");
                INamedTypeSymbol? runtimeType = compilation.GetTypeByMetadataName("Lattice.ECS.Session.SessionRuntime");
                INamedTypeSymbol? frameType = compilation.GetTypeByMetadataName("Lattice.ECS.Core.Frame");
                INamedTypeSymbol? contractType = compilation.GetTypeByMetadataName("Lattice.ECS.Framework.SystemAuthoringContract");

                if (systemType == null || runtimeType == null || frameType == null || contractType == null)
                {
                    return;
                }

                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeField(symbolContext, systemType, runtimeType),
                    SymbolKind.Field);

                startContext.RegisterOperationBlockStartAction(operationContext =>
                {
                    if (operationContext.OwningSymbol is not IMethodSymbol method)
                    {
                        return;
                    }

                    bool isSystemUpdate = IsSystemUpdateMethod(method, systemType);
                    bool isSimulationHook = IsSystemSimulationMethod(method, systemType) || IsRuntimeSimulationHook(method, runtimeType);
                    if (!isSimulationHook)
                    {
                        return;
                    }

                    bool sawGuardedApi = false;
                    string? firstGuardedApiName = null;
                    string? firstRequiredCapability = null;
                    Location? firstGuardedLocation = null;
                    bool needsStructuralContract = false;
                    bool needsGlobalReadContract = false;
                    bool needsGlobalWriteContract = false;

                    operationContext.RegisterOperationAction(static operationAction =>
                    {
                        var invocation = (IInvocationOperation)operationAction.Operation;
                        if (TryGetNondeterministicApiName(invocation.TargetMethod, out string? apiName))
                        {
                            operationAction.ReportDiagnostic(
                                Diagnostic.Create(
                                    NondeterministicApiRule,
                                    invocation.Syntax.GetLocation(),
                                    operationAction.ContainingSymbol.Name,
                                    apiName));
                        }
                    }, OperationKind.Invocation);

                    operationContext.RegisterOperationAction(static operationAction =>
                    {
                        var creation = (IObjectCreationOperation)operationAction.Operation;
                        if (TryGetNondeterministicTypeName(creation.Type, out string? apiName))
                        {
                            operationAction.ReportDiagnostic(
                                Diagnostic.Create(
                                    NondeterministicApiRule,
                                    creation.Syntax.GetLocation(),
                                    operationAction.ContainingSymbol.Name,
                                    apiName));
                        }
                    }, OperationKind.ObjectCreation);

                    operationContext.RegisterOperationAction(static operationAction =>
                    {
                        var propertyReference = (IPropertyReferenceOperation)operationAction.Operation;
                        if (TryGetNondeterministicPropertyName(propertyReference.Property, out string? apiName))
                        {
                            operationAction.ReportDiagnostic(
                                Diagnostic.Create(
                                    NondeterministicApiRule,
                                    propertyReference.Syntax.GetLocation(),
                                    operationAction.ContainingSymbol.Name,
                                    apiName));
                        }
                    }, OperationKind.PropertyReference);

                    if (!isSystemUpdate)
                    {
                        return;
                    }

                    operationContext.RegisterOperationAction(operationAction =>
                    {
                        var invocation = (IInvocationOperation)operationAction.Operation;
                        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, frameType))
                        {
                            return;
                        }

                        if (!TryGetRequiredContractCapability(invocation.TargetMethod.Name, out string? requiredCapability))
                        {
                            return;
                        }

                        sawGuardedApi = true;
                        firstGuardedApiName ??= invocation.TargetMethod.Name;
                        firstRequiredCapability ??= requiredCapability;
                        firstGuardedLocation ??= invocation.Syntax.GetLocation();

                        switch (requiredCapability)
                        {
                            case nameof(ContractRequirement.StructuralChange):
                                needsStructuralContract = true;
                                break;
                            case nameof(ContractRequirement.GlobalRead):
                                needsGlobalReadContract = true;
                                break;
                            case nameof(ContractRequirement.GlobalWrite):
                                needsGlobalWriteContract = true;
                                break;
                        }
                    }, OperationKind.Invocation);

                    operationContext.RegisterOperationBlockEndAction(endContext =>
                    {
                        if (!sawGuardedApi)
                        {
                            return;
                        }

                        if (!TryGetContractCapabilities(compilation, method.ContainingType, contractType, out ContractCapabilities capabilities))
                        {
                            endContext.ReportDiagnostic(
                                Diagnostic.Create(
                                    MissingContractRule,
                                    firstGuardedLocation ?? method.Locations.FirstOrDefault(),
                                    method.ContainingType.Name,
                                    firstGuardedApiName ?? "Frame API",
                                    firstRequiredCapability ?? "ExplicitContract"));
                            return;
                        }

                        bool lacksStructural = needsStructuralContract && !capabilities.AllowsStructuralChanges;
                        bool lacksGlobalRead = needsGlobalReadContract && !capabilities.AllowsGlobalReads;
                        bool lacksGlobalWrite = needsGlobalWriteContract && !capabilities.AllowsGlobalWrites;

                        if (!lacksStructural && !lacksGlobalRead && !lacksGlobalWrite)
                        {
                            return;
                        }

                        string missingCapability = lacksStructural
                            ? nameof(ContractRequirement.StructuralChange)
                            : lacksGlobalWrite
                                ? nameof(ContractRequirement.GlobalWrite)
                                : nameof(ContractRequirement.GlobalRead);

                        endContext.ReportDiagnostic(
                            Diagnostic.Create(
                                MissingContractRule,
                                firstGuardedLocation ?? method.Locations.FirstOrDefault(),
                                method.ContainingType.Name,
                                firstGuardedApiName ?? "Frame API",
                                missingCapability));
                    });
                });
            });
        }

        private static void AnalyzeField(SymbolAnalysisContext context, INamedTypeSymbol systemType, INamedTypeSymbol runtimeType)
        {
            var field = (IFieldSymbol)context.Symbol;
            if (!field.IsStatic || field.IsConst || field.IsReadOnly)
            {
                return;
            }

            INamedTypeSymbol containingType = field.ContainingType;
            if (!Implements(containingType, systemType) && !DerivesFrom(containingType, runtimeType))
            {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    MutableStaticFieldRule,
                    field.Locations.FirstOrDefault(),
                    containingType.Name,
                    field.Name));
        }

        private static bool IsSystemSimulationMethod(IMethodSymbol method, INamedTypeSymbol systemType)
        {
            if (method.ContainingType == null || !Implements(method.ContainingType, systemType))
            {
                return false;
            }

            return ((method.Name == "OnInit" || method.Name == "OnDestroy") &&
                    method.Parameters.Length == 1) ||
                   IsSystemUpdateMethod(method, systemType);
        }

        private static bool IsSystemUpdateMethod(IMethodSymbol method, INamedTypeSymbol systemType)
        {
            return method.Name == "OnUpdate" &&
                   method.Parameters.Length == 2 &&
                   method.ContainingType != null &&
                   Implements(method.ContainingType, systemType);
        }

        private static bool IsRuntimeSimulationHook(IMethodSymbol method, INamedTypeSymbol runtimeType)
        {
            if (method.ContainingType == null || !DerivesFrom(method.ContainingType, runtimeType))
            {
                return false;
            }

            return method.Name == "ApplyInputSet" ||
                   method.Name == "ApplyInputs";
        }

        private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
        {
            return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, interfaceType));
        }

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            INamedTypeSymbol? current = type;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }

        private static bool TryGetNondeterministicApiName(IMethodSymbol method, out string? apiName)
        {
            string containingType = method.ContainingType.ToDisplayString();
            string methodName = method.Name;

            if (containingType == "System.Guid" && methodName == "NewGuid")
            {
                apiName = "System.Guid.NewGuid()";
                return true;
            }

            if (containingType == "System.Threading.Tasks.Task" && (methodName == "Run" || methodName == "Delay"))
            {
                apiName = $"System.Threading.Tasks.Task.{methodName}";
                return true;
            }

            if (containingType == "System.Threading.Thread" && (methodName == "Sleep" || methodName == "Yield"))
            {
                apiName = $"System.Threading.Thread.{methodName}";
                return true;
            }

            if (containingType == "System.Threading.SpinWait" && methodName == "SpinUntil")
            {
                apiName = "System.Threading.SpinWait.SpinUntil";
                return true;
            }

            if (containingType == "System.Diagnostics.Stopwatch" && (methodName == "StartNew" || methodName == "GetTimestamp"))
            {
                apiName = $"System.Diagnostics.Stopwatch.{methodName}";
                return true;
            }

            if (containingType == "System.Random")
            {
                apiName = $"System.Random.{methodName}";
                return true;
            }

            apiName = null;
            return false;
        }

        private static bool TryGetNondeterministicTypeName(ITypeSymbol? type, out string? apiName)
        {
            string? typeName = type?.ToDisplayString();
            if (typeName == "System.Random" ||
                typeName == "System.Diagnostics.Stopwatch" ||
                typeName == "System.Threading.Thread")
            {
                apiName = typeName;
                return true;
            }

            apiName = null;
            return false;
        }

        private static bool TryGetNondeterministicPropertyName(IPropertySymbol property, out string? apiName)
        {
            string containingType = property.ContainingType.ToDisplayString();
            string propertyName = property.Name;

            if ((containingType == "System.DateTime" || containingType == "System.DateTimeOffset") &&
                (propertyName == "Now" || propertyName == "UtcNow"))
            {
                apiName = $"{containingType}.{propertyName}";
                return true;
            }

            if (containingType == "System.Environment" &&
                (propertyName == "TickCount" || propertyName == "TickCount64"))
            {
                apiName = $"{containingType}.{propertyName}";
                return true;
            }

            if (containingType == "System.Random" && propertyName == "Shared")
            {
                apiName = "System.Random.Shared";
                return true;
            }

            apiName = null;
            return false;
        }

        private static bool TryGetRequiredContractCapability(string frameMethodName, out string? requiredCapability)
        {
            switch (frameMethodName)
            {
                case "CreateEntity":
                case "DestroyEntity":
                case "Add":
                case "Remove":
                    requiredCapability = nameof(ContractRequirement.StructuralChange);
                    return true;

                case "HasGlobal":
                case "TryGetGlobal":
                    requiredCapability = nameof(ContractRequirement.GlobalRead);
                    return true;

                case "GetGlobal":
                case "GetGlobalEntity":
                case "TryGetGlobalEntity":
                case "SetGlobal":
                case "RemoveGlobal":
                    requiredCapability = nameof(ContractRequirement.GlobalWrite);
                    return true;

                default:
                    requiredCapability = null;
                    return false;
            }
        }

        private static bool TryGetContractCapabilities(
            Compilation compilation,
            INamedTypeSymbol type,
            INamedTypeSymbol contractType,
            out ContractCapabilities capabilities)
        {
            IPropertySymbol? contractProperty = type.GetMembers("Contract")
                .OfType<IPropertySymbol>()
                .FirstOrDefault(static property => !property.IsImplicitlyDeclared);

            if (contractProperty == null)
            {
                capabilities = default;
                return false;
            }

            foreach (SyntaxReference syntaxReference in contractProperty.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax propertySyntax)
                {
                    continue;
                }

                ExpressionSyntax? expression = propertySyntax.ExpressionBody?.Expression;
                if (expression == null && propertySyntax.AccessorList != null)
                {
                    AccessorDeclarationSyntax? getter = propertySyntax.AccessorList.Accessors
                        .FirstOrDefault(static accessor => accessor.Keyword.Text == "get");

                    if (getter?.ExpressionBody != null)
                    {
                        expression = getter.ExpressionBody.Expression;
                    }
                    else if (getter?.Body?.Statements.Count == 1 &&
                             getter.Body.Statements[0] is ReturnStatementSyntax returnStatement)
                    {
                        expression = returnStatement.Expression;
                    }
                }

                if (expression == null)
                {
                    continue;
                }

                SemanticModel semanticModel = compilation.GetSemanticModel(propertySyntax.SyntaxTree);
                IOperation? operation = semanticModel.GetOperation(expression);
                if (operation == null)
                {
                    continue;
                }

                if (TryEvaluateContractOperation(operation, contractType, out capabilities))
                {
                    return true;
                }
            }

            capabilities = default;
            return false;
        }

        private static bool TryEvaluateContractOperation(
            IOperation operation,
            INamedTypeSymbol contractType,
            out ContractCapabilities capabilities)
        {
            switch (operation)
            {
                case IPropertyReferenceOperation propertyReference
                    when SymbolEqualityComparer.Default.Equals(propertyReference.Property.ContainingType, contractType):
                    if (propertyReference.Property.Name == "Default")
                    {
                        capabilities = default;
                        return true;
                    }

                    if (propertyReference.Property.Name == "ReadOnly")
                    {
                        capabilities = default;
                        return true;
                    }

                    break;

                case IObjectCreationOperation creation
                    when SymbolEqualityComparer.Default.Equals(creation.Constructor?.ContainingType, contractType):
                    int globalAccess = 0;
                    int structuralChangeAccess = 0;

                    foreach (IArgumentOperation argument in creation.Arguments)
                    {
                        string? parameterName = argument.Parameter?.Name;
                        if (string.Equals(parameterName, "globalAccess", StringComparison.Ordinal))
                        {
                            globalAccess = GetEnumValue(argument.Value);
                            continue;
                        }

                        if (string.Equals(parameterName, "structuralChangeAccess", StringComparison.Ordinal))
                        {
                            structuralChangeAccess = GetEnumValue(argument.Value);
                            continue;
                        }
                    }

                    capabilities = new ContractCapabilities(
                        allowsGlobalReads: globalAccess >= 1,
                        allowsGlobalWrites: globalAccess >= 2,
                        allowsStructuralChanges: structuralChangeAccess >= 1);
                    return true;
            }

            capabilities = default;
            return false;
        }

        private static int GetEnumValue(IOperation operation)
        {
            Optional<object?> constantValue = operation.ConstantValue;
            if (constantValue.HasValue && constantValue.Value != null)
            {
                return Convert.ToInt32(constantValue.Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (operation is IConversionOperation conversion)
            {
                return GetEnumValue(conversion.Operand);
            }

            return 0;
        }

        private enum ContractRequirement
        {
            StructuralChange,
            GlobalRead,
            GlobalWrite
        }

        private readonly struct ContractCapabilities
        {
            public ContractCapabilities(bool allowsGlobalReads, bool allowsGlobalWrites, bool allowsStructuralChanges)
            {
                AllowsGlobalReads = allowsGlobalReads;
                AllowsGlobalWrites = allowsGlobalWrites;
                AllowsStructuralChanges = allowsStructuralChanges;
            }

            public bool AllowsGlobalReads { get; }

            public bool AllowsGlobalWrites { get; }

            public bool AllowsStructuralChanges { get; }
        }
    }
}
