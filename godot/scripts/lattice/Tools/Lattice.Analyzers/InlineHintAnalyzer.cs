// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lattice.Analyzers
{
    /// <summary>
    /// 检测热路径方法是否缺少 AggressiveInlining 的 Analyzer
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InlineHintAnalyzer : DiagnosticAnalyzer
    {
        // P0: 必须内联（简单属性、运算符）
        public const string RequiredId = "LATTICE001";
        private static readonly DiagnosticDescriptor RequiredRule = new(
            RequiredId,
            "热路径方法必须添加 AggressiveInlining",
            "方法 '{0}' 是简单的 FP 操作，必须添加 [MethodImpl(MethodImplOptions.AggressiveInlining)] 以确保性能",
            "Performance",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "简单的 FP/FPVector 属性访问器和运算符应该使用 AggressiveInlining 来避免方法调用开销。",
            helpLinkUri: "https://docs.lattice.dev/analyzers/LATTICE001");

        // P1: 建议内联（小型数学方法）
        public const string RecommendedId = "LATTICE002";
        private static readonly DiagnosticDescriptor RecommendedRule = new(
            RecommendedId,
            "建议添加 AggressiveInlining",
            "方法 '{0}' 可能是热路径，建议添加 [MethodImpl(MethodImplOptions.AggressiveInlining)]",
            "Performance",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "小型数学方法（≤3 行）建议内联以减少调用开销。",
            helpLinkUri: "https://docs.lattice.dev/analyzers/LATTICE002");

        // P2: 考虑内联
        public const string ConsiderId = "LATTICE003";
        private static readonly DiagnosticDescriptor ConsiderRule = new(
            ConsiderId,
            "考虑添加 AggressiveInlining",
            "方法 '{0}' 返回 FP/FPVector，考虑添加 [MethodImpl(MethodImplOptions.AggressiveInlining)]",
            "Performance",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "如果此方法是热路径，考虑添加 AggressiveInlining。",
            helpLinkUri: "https://docs.lattice.dev/analyzers/LATTICE003");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RequiredRule, RecommendedRule, ConsiderRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeOperator, SyntaxKind.OperatorDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;

            // 跳过已有 AggressiveInlining 的方法
            if (HasAggressiveInlining(method))
            {
                return;
            }

            // 检查返回类型
            var returnType = method.ReturnType.ToString();
            bool returnsFP = IsFPOrVectorType(returnType);
            bool returnsBoolOrInt = returnType == "bool" || returnType == "int" || returnType == "long";

            // 计算方法体大小
            int statementCount = GetStatementCount(method);
            bool isSimple = statementCount <= 1;
            bool isSmall = statementCount <= 3;

            // 运算符由 OperatorDeclarationSyntax 单独处理

            // P0: 简单方法（如 FP.Abs, FP.Min）必须内联
            if (returnsFP && isSimple && method.ParameterList.Parameters.Count <= 2)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequiredRule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text));
                return;
            }

            // P1: 小型数学方法建议内联
            if (returnsFP && isSmall)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RecommendedRule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text));
                return;
            }

            // P1: 比较方法建议内联
            if (returnsBoolOrInt && isSimple && method.Identifier.Text.Contains("Compare"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RecommendedRule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text));
                return;
            }

            // P2: 其他返回 FP 的方法考虑内联
            if (returnsFP)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ConsiderRule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text));
            }
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;

            // 跳过已有 AggressiveInlining 的属性
            if (HasAggressiveInlining(property))
            {
                return;
            }

            // 检查返回类型
            var returnType = property.Type.ToString();
            if (!IsFPOrVectorType(returnType))
            {
                return;
            }

            // 检查是否是简单的 getter-only 属性
            if (property.AccessorList == null)
            {
                return;
            }

            var getter = property.AccessorList.Accessors
                .FirstOrDefault(a => a.Keyword.IsKind(SyntaxKind.GetKeyword));

            if (getter == null)
            {
                return;
            }

            // 简单的表达式体或单行 getter
            bool isSimple = getter.ExpressionBody != null ||
                            (getter.Body?.Statements.Count ?? 0) <= 1;

            if (isSimple)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RequiredRule,
                    property.Identifier.GetLocation(),
                    property.Identifier.Text));
            }
        }

        private void AnalyzeOperator(SyntaxNodeAnalysisContext context)
        {
            var op = (OperatorDeclarationSyntax)context.Node;

            // 跳过已有 AggressiveInlining 的运算符
            if (HasAggressiveInlining(op))
            {
                return;
            }

            // 检查返回类型
            var returnType = op.ReturnType.ToString();
            if (!IsFPOrVectorType(returnType))
            {
                return;
            }

            // 所有 FP/FPVector 运算符必须内联
            context.ReportDiagnostic(Diagnostic.Create(
                RequiredRule,
                op.OperatorToken.GetLocation(),
                "operator " + op.OperatorToken.Text));
        }

        private static bool HasAggressiveInlining(MethodDeclarationSyntax method)
        {
            return method.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(IsAggressiveInliningAttribute);
        }

        private static bool HasAggressiveInlining(PropertyDeclarationSyntax property)
        {
            if (property.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(IsAggressiveInliningAttribute))
            {
                return true;
            }

            if (property.AccessorList == null)
            {
                return false;
            }

            return property.AccessorList.Accessors
                .SelectMany(accessor => accessor.AttributeLists)
                .SelectMany(attributeList => attributeList.Attributes)
                .Any(IsAggressiveInliningAttribute);
        }

        private static bool HasAggressiveInlining(OperatorDeclarationSyntax op)
        {
            return op.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(IsAggressiveInliningAttribute);
        }

        private static bool IsAggressiveInliningAttribute(AttributeSyntax attribute)
        {
            var name = attribute.Name.ToString();
            return name.Contains("MethodImpl") &&
                   attribute.ArgumentList?.ToString().Contains("AggressiveInlining") == true;
        }

        private static bool IsFPOrVectorType(string typeName)
        {
            return typeName == "FP" ||
                   typeName == "FPVector2" ||
                   typeName == "FPVector3" ||
                   typeName.Contains("FP");
        }

        private static int GetStatementCount(MethodDeclarationSyntax method)
        {
            // 表达式体
            if (method.ExpressionBody != null)
            {
                return 1;
            }

            // 语句体
            if (method.Body == null)
            {
                return 0;
            }

            int count = method.Body.Statements.Count;

            // 忽略 #if DEBUG 块
            foreach (var stmt in method.Body.Statements)
            {
                if (stmt is IfStatementSyntax ifStmt &&
                    ifStmt.Condition.ToString().Contains("DEBUG"))
                {
                    count--;
                }
            }

            return count;
        }
    }
}
