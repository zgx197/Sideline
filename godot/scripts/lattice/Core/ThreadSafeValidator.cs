using System;
using System.Diagnostics;
using System.Threading;

namespace Lattice.Core
{
    /// <summary>
    /// 线程安全验证器 - 检测多线程访问冲突
    /// 
    /// 功能：
    /// 1. 检测读写冲突
    /// 2. 检测并发修改
    /// 3. 调试模式下的详细日志
    /// </summary>
    public sealed class ThreadSafeValidator
    {
        // 访问标记位
        private const int WriteFlag = 1;
        private const int ReadFlag = 2;

        // 每实体的访问状态（使用原子操作）
        private readonly int[] _accessState;
        private readonly int _capacity;

        // 验证模式
        private readonly ValidationMode _mode;

        public ThreadSafeValidator(int capacity, ValidationMode mode = ValidationMode.Debug)
        {
            _capacity = capacity;
            _mode = mode;
            _accessState = mode != ValidationMode.None ? new int[capacity] : null!;
        }

        /// <summary>
        /// 验证读访问
        /// </summary>
        public void VerifyRead(int entityIndex, string operationName = "")
        {
            if (_mode == ValidationMode.None) return;

            var state = Interlocked.Add(ref _accessState[entityIndex], ReadFlag);

            // 检查是否有写操作正在进行
            if ((state & WriteFlag) != 0)
            {
                ReportViolation($"Read-Write conflict on entity {entityIndex}. Operation: {operationName}");
            }
        }

        /// <summary>
        /// 验证写访问
        /// </summary>
        public void VerifyWrite(int entityIndex, string operationName = "")
        {
            if (_mode == ValidationMode.None) return;

            // 尝试获取写锁
            var oldState = Interlocked.CompareExchange(ref _accessState[entityIndex], WriteFlag, 0);

            if (oldState != 0)
            {
                string conflictType = (oldState & WriteFlag) != 0 ? "Write-Write" : "Read-Write";
                ReportViolation($"{conflictType} conflict on entity {entityIndex}. Operation: {operationName}");
            }
        }

        /// <summary>
        /// 释放读锁
        /// </summary>
        public void ReleaseRead(int entityIndex)
        {
            if (_mode == ValidationMode.None) return;

            Interlocked.Add(ref _accessState[entityIndex], -ReadFlag);
        }

        /// <summary>
        /// 释放写锁
        /// </summary>
        public void ReleaseWrite(int entityIndex)
        {
            if (_mode == ValidationMode.None) return;

            Interlocked.Exchange(ref _accessState[entityIndex], 0);
        }

        /// <summary>
        /// 重置所有状态
        /// </summary>
        public void Reset()
        {
            if (_mode == ValidationMode.None) return;

            Array.Clear(_accessState, 0, _accessState.Length);
        }

        [Conditional("DEBUG")]
        private void ReportViolation(string message)
        {
            if (_mode == ValidationMode.Debug)
            {
                Debug.Fail($"[ThreadSafety] {message}");
            }
            else if (_mode == ValidationMode.Release)
            {
                // 在Release模式下记录日志或抛出异常
                throw new InvalidOperationException($"[ThreadSafety] {message}");
            }
        }
    }

    /// <summary>
    /// 验证模式
    /// </summary>
    public enum ValidationMode
    {
        /// <summary>
        /// 不验证（生产环境最高性能）
        /// </summary>
        None,

        /// <summary>
        /// 调试模式（断言失败）
        /// </summary>
        Debug,

        /// <summary>
        /// Release模式（抛出异常）
        /// </summary>
        Release
    }

    /// <summary>
    /// 线程安全的实体访问范围
    /// </summary>
    public readonly struct ThreadSafeReadScope : IDisposable
    {
        private readonly ThreadSafeValidator? _validator;
        private readonly int _entityIndex;

        public ThreadSafeReadScope(ThreadSafeValidator validator, int entityIndex)
        {
            _validator = validator;
            _entityIndex = entityIndex;
            _validator?.VerifyRead(entityIndex);
        }

        public void Dispose()
        {
            _validator?.ReleaseRead(_entityIndex);
        }
    }

    public readonly struct ThreadSafeWriteScope : IDisposable
    {
        private readonly ThreadSafeValidator? _validator;
        private readonly int _entityIndex;

        public ThreadSafeWriteScope(ThreadSafeValidator validator, int entityIndex)
        {
            _validator = validator;
            _entityIndex = entityIndex;
            _validator?.VerifyWrite(entityIndex);
        }

        public void Dispose()
        {
            _validator?.ReleaseWrite(_entityIndex);
        }
    }
}
