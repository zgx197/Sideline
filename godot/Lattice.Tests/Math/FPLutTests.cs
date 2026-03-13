// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FPLut 运行时加载测试
    /// </summary>
    public class FPLutTests
    {
        [Fact]
        public void InitializeBuiltIn_LoadsSuccessfully()
        {
            FPLut.InitializeBuiltIn();
            
            Assert.True(FPLut.IsLoaded);
            Assert.Equal("builtin", FPLut.Version);
            
            // 验证表不为空
            Assert.NotNull(FPLut.SinCosTable);
            Assert.NotNull(FPLut.SqrtTable);
            Assert.NotNull(FPLut.AcosTable);
            Assert.NotNull(FPLut.AtanTable);
        }

        [Fact]
        public void Tables_HaveCorrectSize()
        {
            FPLut.InitializeBuiltIn();
            
            Assert.True(FPLut.SinCosTable.Length >= 4096);
            Assert.True(FPLut.SqrtTable.Length >= 65536);
            Assert.True(FPLut.AcosTable.Length >= 65536);
        }

        [Fact]
        public void SinCosTable_ContainsValidValues()
        {
            FPLut.InitializeBuiltIn();
            
            // 表不为空且有合理大小
            Assert.True(FPLut.SinCosTable.Length >= 4096);
            
            // 所有值在 [-1, 1] 范围内（前 4096 个元素）
            for (int i = 0; i < 4096 && i < FPLut.SinCosTable.Length; i++)
            {
                long value = FPLut.SinCosTable[i];
                Assert.True(value >= -FP.ONE && value <= FP.ONE, 
                    $"Value[{i}] out of range: {value}");
            }
        }

        [Fact]
        public void SqrtTable_ContainsValidValues()
        {
            FPLut.InitializeBuiltIn();
            
            // Sqrt(0) = 0
            Assert.Equal(0, FPLut.SqrtTable[0]);
            
            // Sqrt 是单调递增的
            for (int i = 1; i < 1000; i++)
            {
                Assert.True(FPLut.SqrtTable[i] >= FPLut.SqrtTable[i - 1],
                    $"Sqrt table not monotonic at index {i}");
            }
        }

        [Fact]
        public void DoubleInitialization_IsIgnored()
        {
            FPLut.InitializeBuiltIn();
            var firstVersion = FPLut.Version;
            
            // 第二次初始化应该被忽略
            FPLut.InitializeBuiltIn();
            var secondVersion = FPLut.Version;
            
            Assert.Equal(firstVersion, secondVersion);
        }

        [Fact]
        public void AccessBeforeInitialization_ThrowsException()
        {
            // 注意：这个测试假设 FPLut 未被初始化
            // 如果其他测试已经初始化，可能会失败
            
            // 重置状态（通过反射，仅测试用途）
            var sinCosField = typeof(FPLut).GetField("_sinCosTable", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            sinCosField?.SetValue(null, null);
            
            var isLoadedProperty = typeof(FPLut).GetProperty("IsLoaded");
            // IsLoaded 是计算属性，重置 _sinCosTable 后应该返回 false
            
            // 访问未初始化的表应该抛出异常
            Assert.Throws<System.InvalidOperationException>(() => FPLut.SinCosTable);
        }
    }
}
