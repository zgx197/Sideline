// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.Tests.Benchmarks
{
    /// <summary>
    /// ComponentSet 性能基准测试
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class ComponentSetBenchmarks
    {
        private ComponentSetNet8 _set1;
        private ComponentSetNet8 _set2;

        [GlobalSetup]
        public void Setup()
        {
            _set1 = new ComponentSetNet8();
            _set2 = new ComponentSetNet8();

            // 填充测试数据
            for (int i = 0; i < 100; i += 2)
            {
                _set1.Add(i);
                if (i % 4 == 0)
                    _set2.Add(i);
            }
        }

        [Benchmark]
        public void Add_Operation()
        {
            var set = new ComponentSetNet8();
            for (int i = 0; i < 100; i++)
            {
                set.Add(i);
            }
        }

        [Benchmark]
        public void IsSet_Operation()
        {
            bool result = false;
            for (int i = 0; i < 100; i++)
            {
                result ^= _set1.IsSet(i);
            }
        }

        [Benchmark]
        public void IsSupersetOf_SIMD()
        {
            _set1.IsSupersetOf(ref _set2);
        }

        [Benchmark]
        public void Overlaps_SIMD()
        {
            _set1.Overlaps(ref _set2);
        }

        [Benchmark]
        public void UnionWith_SIMD()
        {
            var temp = new ComponentSetNet8();
            temp.UnionWith(ref _set1);
        }

        [Benchmark]
        public void IntersectWith_SIMD()
        {
            var temp = new ComponentSetNet8();
            temp.IntersectWith(ref _set1);
        }

        [Benchmark]
        public void Equals_SIMD()
        {
            _set1.Equals(_set2);
        }

        [Benchmark]
        public void GetHashCode_Operation()
        {
            _set1.GetHashCode();
        }
    }

    /// <summary>
    /// EntityRef 性能基准测试
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class EntityRefBenchmarks
    {
        private EntityRef[] _entities;

        [GlobalSetup]
        public void Setup()
        {
            _entities = new EntityRef[1000];
            for (int i = 0; i < 1000; i++)
            {
                _entities[i] = new EntityRef(i, i + 1);
            }
        }

        [Benchmark]
        public void Create_Entity()
        {
            var entities = new EntityRef[1000];
            for (int i = 0; i < 1000; i++)
            {
                entities[i] = new EntityRef(i, i + 1);
            }
        }

        [Benchmark]
        public void GetHashCode_Entity()
        {
            int hash = 0;
            for (int i = 0; i < 1000; i++)
            {
                hash ^= _entities[i].GetHashCode();
            }
        }

        [Benchmark]
        public void Equality_Check()
        {
            bool equal = true;
            for (int i = 0; i < 999; i++)
            {
                equal &= _entities[i] == _entities[i + 1];
            }
        }

        [Benchmark]
        public void Raw_Access()
        {
            ulong sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                sum += _entities[i].Raw;
            }
        }
    }

    /// <summary>
    /// 哈希算法性能对比
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    public class HashBenchmarks
    {
        private byte[] _data;

        [Params(16, 64, 256, 1024)]
        public int DataSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _data = new byte[DataSize];
            new Random(42).NextBytes(_data);
        }

        [Benchmark(Baseline = true)]
        public int Fnv1a32()
        {
            return DeterministicHash.Fnv1a32(_data);
        }

        [Benchmark]
        public long Fnv1a64()
        {
            return DeterministicHash.Fnv1a64(_data);
        }

        [Benchmark]
        public int DotNet_HashCode()
        {
            // 非确定性，仅用于对比
            return _data.GetHashCode();
        }
    }

    /// <summary>
    /// 确定性集合性能对比
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class DeterministicCollectionsBenchmarks
    {
        private DeterministicIdMap<int> _idMap;
        private Dictionary<int, int> _dictionary;

        [GlobalSetup]
        public void Setup()
        {
            _idMap = new DeterministicIdMap<int>(256);
            _dictionary = new Dictionary<int, int>(256);

            for (int i = 1; i <= 100; i++)
            {
                _idMap.Add(i, i * 10);
                _dictionary[i] = i * 10;
            }
        }

        [Benchmark]
        public void DeterministicIdMap_Lookup()
        {
            for (int i = 1; i <= 100; i++)
            {
                _idMap.TryGetValue(i, out var _);
            }
        }

        [Benchmark(Baseline = true)]
        public void Dictionary_Lookup()
        {
            for (int i = 1; i <= 100; i++)
            {
                _dictionary.TryGetValue(i, out var _);
            }
        }
    }
}
