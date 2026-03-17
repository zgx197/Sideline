// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// Delta 压缩器 - FrameSync 风格
    /// 
    /// 用于计算两个帧之间的差异，减少网络传输数据量
    /// </summary>
    public unsafe sealed class DeltaCompressor
    {
        #region 字段

        /// <summary>临时缓冲区</summary>
        private byte[] _tempBuffer;

        /// <summary>缓冲区大小</summary>
        private int _bufferSize;

        #endregion

        #region 构造函数

        public DeltaCompressor(int maxFrameSize = 1024 * 1024) // 1MB
        {
            _tempBuffer = new byte[maxFrameSize];
            _bufferSize = maxFrameSize;
        }

        #endregion

        #region Delta 压缩

        /// <summary>
        /// 计算两个帧之间的差异
        /// </summary>
        /// <param name="baseline">基准帧数据</param>
        /// <param name="current">当前帧数据</param>
        /// <param name="deltaOutput">差异输出</param>
        /// <returns>差异数据长度</returns>
        public int Compress(byte[] baseline, byte[] current, byte[] deltaOutput)
        {
            if (baseline.Length != current.Length)
                throw new ArgumentException("Baseline and current must have same length");

            int length = baseline.Length;
            int deltaPos = 0;

            fixed (byte* basePtr = baseline)
            fixed (byte* currPtr = current)
            fixed (byte* deltaPtr = deltaOutput)
            {
                int i = 0;
                while (i < length)
                {
                    // 找到第一个不同的字节
                    while (i < length && basePtr[i] == currPtr[i])
                        i++;

                    if (i >= length)
                        break;

                    // 记录偏移量
                    if (deltaPos + 4 > deltaOutput.Length)
                        throw new InvalidOperationException("Delta output buffer too small");

                    *(int*)(deltaPtr + deltaPos) = i;
                    deltaPos += 4;

                    // 找到连续不同的范围
                    int start = i;
                    while (i < length && basePtr[i] != currPtr[i])
                        i++;

                    int count = i - start;

                    // 写入长度
                    if (deltaPos + 4 > deltaOutput.Length)
                        throw new InvalidOperationException("Delta output buffer too small");

                    *(int*)(deltaPtr + deltaPos) = count;
                    deltaPos += 4;

                    // 写入差异数据
                    if (deltaPos + count > deltaOutput.Length)
                        throw new InvalidOperationException("Delta output buffer too small");

                    Buffer.BlockCopy(current, start, deltaOutput, deltaPos, count);
                    deltaPos += count;
                }
            }

            return deltaPos;
        }

        /// <summary>
        /// 应用差异到基准帧
        /// </summary>
        /// <param name="baseline">基准帧数据</param>
        /// <param name="delta">差异数据</param>
        /// <param name="deltaLength">差异数据长度</param>
        /// <param name="output">输出帧</param>
        public void Decompress(byte[] baseline, byte[] delta, int deltaLength, byte[] output)
        {
            // 先复制基准帧
            Buffer.BlockCopy(baseline, 0, output, 0, baseline.Length);

            fixed (byte* deltaPtr = delta)
            {
                int pos = 0;
                while (pos < deltaLength)
                {
                    // 读取偏移量
                    int offset = *(int*)(deltaPtr + pos);
                    pos += 4;

                    // 读取长度
                    int count = *(int*)(deltaPtr + pos);
                    pos += 4;

                    // 应用差异
                    Buffer.BlockCopy(delta, pos, output, offset, count);
                    pos += count;
                }
            }
        }

        #endregion

        #region 简化版本（位图标记）

        /// <summary>
        /// 使用位图标记变化的块（更高效的版本）
        /// </summary>
        /// <param name="baseline">基准帧</param>
        /// <param name="current">当前帧</param>
        /// <param name="blockSize">块大小</param>
        /// <param name="changedBlocks">变化的块索引列表</param>
        /// <returns>变化的块数量</returns>
        public int FindChangedBlocks(byte[] baseline, byte[] current, int blockSize, Span<int> changedBlocks)
        {
            if (baseline.Length != current.Length)
                throw new ArgumentException("Length mismatch");

            int numBlocks = (baseline.Length + blockSize - 1) / blockSize;
            int count = 0;

            fixed (byte* basePtr = baseline)
            fixed (byte* currPtr = current)
            {
                for (int i = 0; i < numBlocks && count < changedBlocks.Length; i++)
                {
                    int start = i * blockSize;
                    int end = System.Math.Min(start + blockSize, baseline.Length);

                    // 检查块是否有变化
                    bool changed = false;
                    for (int j = start; j < end && !changed; j++)
                    {
                        if (basePtr[j] != currPtr[j])
                            changed = true;
                    }

                    if (changed)
                    {
                        changedBlocks[count++] = i;
                    }
                }
            }

            return count;
        }

        #endregion
    }
}
