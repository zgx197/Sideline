using System;
using System.Diagnostics;

namespace Lattice.Core
{
    /// <summary>
    /// Entity版本号正确性验证测试
    /// </summary>
    public static class EntityValidationTest
    {
        public static void RunAllTests()
        {
            Console.WriteLine("=== Entity Version Validation Tests ===\n");
            
            Test_CreateAndDestroy();
            Test_VersionIncrement();
            Test_ReuseDetection();
            Test_DelayedDestroy();
            Test_BatchOperations();
            Test_BoundaryCheck();
            
            Console.WriteLine("\n=== All Tests Passed! ===");
        }
        
        /// <summary>
        /// 测试1：基本创建和销毁
        /// </summary>
        private static void Test_CreateAndDestroy()
        {
            Console.WriteLine("Test 1: Create and Destroy");
            
            using var registry = new EntityRegistry();
            
            var e1 = registry.Create();
            Debug.Assert(registry.IsValid(e1), "e1 should be valid after creation");
            Debug.Assert(registry.IsAlive(e1), "e1 should be alive");
            
            bool destroyed = registry.DestroyImmediate(e1);
            Debug.Assert(destroyed, "Destroy should return true");
            Debug.Assert(!registry.IsValid(e1), "e1 should be invalid after destroy");
            Debug.Assert(!registry.IsAlive(e1), "e1 should not be alive after destroy");
            
            Console.WriteLine("  ✓ Create and immediate destroy works");
        }
        
        /// <summary>
        /// 测试2：版本号正确递增（核心测试）
        /// </summary>
        private static void Test_VersionIncrement()
        {
            Console.WriteLine("\nTest 2: Version Increment on Reuse");
            
            using var registry = new EntityRegistry();
            
            // 创建实体
            var e1 = registry.Create();
            int e1Version = e1.Version & EntityRegistry.VersionMask;
            Console.WriteLine($"  Created e1: Index={e1.Index}, Version={e1Version}");
            
            // 销毁
            registry.DestroyImmediate(e1);
            int afterDestroyVersion = registry.GetCurrentVersion(e1.Index);
            Console.WriteLine($"  After destroy: Version={afterDestroyVersion} (should be {e1Version})");
            Debug.Assert(afterDestroyVersion == e1Version, "Version should be preserved after destroy");
            
            // 复用槽位 - 这是关键测试！
            var e2 = registry.Create();
            int e2Version = e2.Version & EntityRegistry.VersionMask;
            Console.WriteLine($"  Created e2 (reuse): Index={e2.Index}, Version={e2Version}");
            
            // 验证：
            // 1. e2应该复用e1的索引
            Debug.Assert(e2.Index == e1.Index, "e2 should reuse e1's index");
            
            // 2. e2的版本号应该比e1大1
            Debug.Assert(e2Version == e1Version + 1, $"Version should increment by 1 (expected {e1Version + 1}, got {e2Version})");
            
            // 3. e1应该失效（版本号不匹配）
            Debug.Assert(!registry.IsValid(e1), "e1 should be invalid (version mismatch)");
            
            // 4. e2应该有效
            Debug.Assert(registry.IsValid(e2), "e2 should be valid");
            
            Console.WriteLine("  ✓ Version correctly increments on reuse");
            Console.WriteLine("  ✓ Old entity reference correctly invalidated");
        }
        
        /// <summary>
        /// 测试3：多次复用检测
        /// </summary>
        private static void Test_ReuseDetection()
        {
            Console.WriteLine("\nTest 3: Multiple Reuse Detection");
            
            using var registry = new EntityRegistry();
            
            var original = registry.Create();
            int index = original.Index;
            
            // 记录所有创建的实体
            var allVersions = new System.Collections.Generic.List<int>();
            allVersions.Add(original.Version & EntityRegistry.VersionMask);
            
            // 销毁并复用10次
            Entity current = original;
            for (int i = 0; i < 10; i++)
            {
                registry.DestroyImmediate(current);
                current = registry.Create();
                
                Debug.Assert(current.Index == index, $"Reuse {i+1}: Index should be {index}");
                int version = current.Version & EntityRegistry.VersionMask;
                allVersions.Add(version);
                
                // 验证版本号连续递增
                Debug.Assert(version == allVersions[i] + 1, 
                    $"Reuse {i+1}: Version should increment by 1");
            }
            
            Console.WriteLine($"  Index {index} reused 10 times");
            Console.WriteLine($"  Versions: {string.Join(" -> ", allVersions)}");
            Console.WriteLine("  ✓ All old references correctly invalidated");
            
            // 验证原始引用已失效
            Debug.Assert(!registry.IsValid(original), "Original reference should be invalid");
        }
        
        /// <summary>
        /// 测试4：延迟销毁
        /// </summary>
        private static void Test_DelayedDestroy()
        {
            Console.WriteLine("\nTest 4: Delayed Destroy (CommandBuffer)");
            
            using var registry = new EntityRegistry();
            var cmd = new CommandBuffer(registry);
            
            var e1 = registry.Create();
            var e2 = registry.Create();
            
            Debug.Assert(registry.IsValid(e1) && registry.IsValid(e2), "Both should be valid");
            
            // 延迟销毁
            cmd.Destroy(e1);
            Debug.Assert(registry.IsValid(e1), "e1 should still be valid before commit");
            Debug.Assert(registry.IsDestroyPending(e1), "e1 should be marked as destroy pending");
            
            cmd.ExecuteDestroys();
            Debug.Assert(!registry.IsValid(e1), "e1 should be invalid after commit");
            Debug.Assert(registry.IsValid(e2), "e2 should still be valid");
            
            Console.WriteLine("  ✓ Delayed destroy works correctly");
        }
        
        /// <summary>
        /// 测试5：批量操作
        /// </summary>
        private static void Test_BatchOperations()
        {
            Console.WriteLine("\nTest 5: Batch Operations");
            
            using var registry = new EntityRegistry();
            
            // 批量创建
            var entities = new Entity[100];
            registry.CreateBatch(entities);
            
            foreach (var e in entities)
            {
                Debug.Assert(registry.IsValid(e), "All batch created entities should be valid");
            }
            
            // 批量销毁
            var cmd = new CommandBuffer(registry);
            cmd.DestroyBatch(entities.AsSpan());
            cmd.ExecuteDestroys();
            
            foreach (var e in entities)
            {
                Debug.Assert(!registry.IsValid(e), "All batch destroyed entities should be invalid");
            }
            
            // 验证版本号递增
            for (int i = 0; i < 100; i++)
            {
                var newEntity = registry.Create();
                int version = newEntity.Version & EntityRegistry.VersionMask;
                Debug.Assert(version == 2, $"Reused entity should have version 2, got {version}");
            }
            
            Console.WriteLine("  ✓ Batch create works");
            Console.WriteLine("  ✓ Batch destroy works");
            Console.WriteLine("  ✓ Version increment correct after batch operations");
        }
        
        /// <summary>
        /// 测试6：边界检查
        /// </summary>
        private static void Test_BoundaryCheck()
        {
            Console.WriteLine("\nTest 6: Boundary Check (Unsigned Comparison)");
            
            using var registry = new EntityRegistry();
            
            // 测试负数索引
            var invalidEntity = new Entity(-1, EntityRegistry.ActiveBit | 1);
            Debug.Assert(!registry.IsValid(invalidEntity), "Negative index should be invalid");
            
            // 测试越界索引
            var outOfBounds = new Entity(99999, EntityRegistry.ActiveBit | 1);
            Debug.Assert(!registry.IsValid(outOfBounds), "Out of bounds index should be invalid");
            
            // 测试版本号不匹配
            var e = registry.Create();
            var wrongVersion = new Entity(e.Index, e.Version + 1);
            Debug.Assert(!registry.IsValid(wrongVersion), "Wrong version should be invalid");
            
            // 测试非活跃实体
            registry.DestroyImmediate(e);
            Debug.Assert(!registry.IsValid(e), "Destroyed entity should be invalid");
            
            Console.WriteLine("  ✓ Negative index correctly rejected");
            Console.WriteLine("  ✓ Out of bounds correctly rejected");
            Console.WriteLine("  ✓ Version mismatch correctly detected");
            Console.WriteLine("  ✓ Destroyed entity correctly invalidated");
        }
        
        /// <summary>
        /// 运行性能基准测试
        /// </summary>
        public static void RunBenchmark(int iterations = 100000)
        {
            Console.WriteLine("\n=== Performance Benchmark ===\n");
            
            using var registry = new EntityRegistry();
            
            // 预热
            for (int i = 0; i < 1000; i++)
            {
                var e = registry.Create();
                registry.DestroyImmediate(e);
            }
            registry.Clear();
            
            var sw = Stopwatch.StartNew();
            
            // 创建测试
            for (int i = 0; i < iterations; i++)
            {
                registry.Create();
            }
            
            sw.Stop();
            Console.WriteLine($"Create {iterations} entities: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  {(iterations / (sw.ElapsedMilliseconds + 0.001)):F0} ops/ms");
            
            // 验证测试
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var e = new Entity(i % registry.Count, registry.GetVersion(i % registry.Count));
                registry.IsValid(e);
            }
            sw.Stop();
            Console.WriteLine($"\nValidate {iterations} entities: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  {(iterations / (sw.ElapsedMilliseconds + 0.001)):F0} ops/ms");
        }
    }
}
