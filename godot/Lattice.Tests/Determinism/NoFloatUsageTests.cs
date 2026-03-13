// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Reflection;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.Determinism
{
    /// <summary>
    /// 验证 Lattice 程序集中没有使用浮点类型和函数
    /// </summary>
    public class NoFloatUsageTests
    {
        private static readonly HashSet<string> AllowedFloatUsagePatterns = new()
        {
            // 允许的 UNSAFE 转换方法（已标记为 Obsolete）
            "FromFloat_UNSAFE",
            "FromDouble_UNSAFE",
            "ToFloat_UNSAFE",
            "ToDouble_UNSAFE",
            "FromString_UNSAFE",
            
            // LUT 生成器工具（不在主程序集中）
            "LutGenerator",
            "Generate",
            
            // 显式转换运算符（故意设计为抛出异常）
            "op_Explicit",
            
            // ToString 方法（格式化可能使用运行时辅助）
            "ToString",
            "ToStringInternal",
        };

        /// <summary>
        /// 验证没有声明 float/double 类型的局部变量
        /// </summary>
        [Fact]
        public void LatticeAssembly_ShouldNotDeclareFloatLocals()
        {
            var assembly = typeof(FP).Assembly;
            var violations = new List<string>();

            foreach (var type in assembly.GetTypes())
            {
                if (type.Namespace?.Contains("Tests") == true)
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (IsAllowedMethod(method))
                        continue;

                    try
                    {
                        var body = method.GetMethodBody();
                        if (body?.LocalVariables == null)
                            continue;

                        foreach (var local in body.LocalVariables)
                        {
                            var localType = local.LocalType;
                            if (localType == typeof(float) || localType == typeof(double))
                            {
                                violations.Add($"{type.Name}.{method.Name}: 局部变量类型 {localType.Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skip {method.Name}: {ex.Message}");
                    }
                }
            }

            Assert.Empty(violations);
        }

        /// <summary>
        /// 验证方法签名中没有 float/double 参数或返回值
        /// </summary>
        [Fact]
        public void LatticeAssembly_ShouldNotHaveFloatSignatures()
        {
            var assembly = typeof(FP).Assembly;
            var violations = new List<string>();

            foreach (var type in assembly.GetTypes())
            {
                if (type.Namespace?.Contains("Tests") == true)
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (IsAllowedMethod(method))
                        continue;

                    // 检查返回值
                    if (method.ReturnType == typeof(float) || method.ReturnType == typeof(double))
                    {
                        violations.Add($"{type.Name}.{method.Name}: 返回类型 {method.ReturnType.Name}");
                    }

                    // 检查参数
                    foreach (var param in method.GetParameters())
                    {
                        if (param.ParameterType == typeof(float) || param.ParameterType == typeof(double))
                        {
                            violations.Add($"{type.Name}.{method.Name}: 参数 {param.Name} 类型 {param.ParameterType.Name}");
                        }
                    }
                }
            }

            Assert.Empty(violations);
        }

        /// <summary>
        /// 验证没有 float/double 类型的字段或属性
        /// </summary>
        [Fact]
        public void LatticeAssembly_ShouldNotReferenceFloatTypes()
        {
            var assembly = typeof(FP).Assembly;
            var violations = new List<string>();

            foreach (var type in assembly.GetTypes())
            {
                if (type.Namespace?.Contains("Tests") == true)
                    continue;

                // 检查字段类型
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(float) || field.FieldType == typeof(double))
                    {
                        violations.Add($"{type.Name}.{field.Name}: 字段类型 {field.FieldType.Name}");
                    }
                }

                // 检查属性类型
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(double))
                    {
                        violations.Add($"{type.Name}.{prop.Name}: 属性类型 {prop.PropertyType.Name}");
                    }
                }
            }

            Assert.Empty(violations);
        }

        #region 辅助方法

        private bool IsAllowedMethod(MethodInfo? method)
        {
            if (method == null) return false;
            var name = method.Name;
            return AllowedFloatUsagePatterns.Any(pattern => name.Contains(pattern));
        }

        #endregion
    }
}
