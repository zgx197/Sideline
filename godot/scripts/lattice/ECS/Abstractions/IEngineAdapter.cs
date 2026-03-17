// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Math;

namespace Lattice.ECS.Abstractions
{
    /// <summary>
    /// 游戏引擎适配器接口
    /// 
    /// Lattice ECS 核心与具体引擎解耦，通过适配器与引擎交互。
    /// 这样可以支持：
    /// - Godot（当前）
    /// - Unity（未来）
    /// - 自定义引擎（未来）
    /// - 纯 .NET（服务器/无图形）
    /// </summary>
    public interface IEngineAdapter
    {
        #region 时间管理

        /// <summary>当前游戏时间（秒）</summary>
        FP GameTime { get; }

        /// <summary>固定时间步长（如 1/60）</summary>
        FP FixedDeltaTime { get; }

        /// <summary>当前帧号</summary>
        int FrameNumber { get; }

        /// <summary>是否在主线程</summary>
        bool IsMainThread { get; }

        #endregion

        #region 实体视图同步

        /// <summary>
        /// 创建实体对应的引擎对象（如 Godot Node、Unity GameObject）
        /// </summary>
        IEntityView CreateEntityView(Entity entity);

        /// <summary>
        /// 销毁引擎对象
        /// </summary>
        void DestroyEntityView(Entity entity, IEntityView view);

        /// <summary>
        /// 更新实体位置（引擎回调）
        /// </summary>
        void UpdateEntityTransform(Entity entity, FPVector3 position, FPQuaternion rotation);

        #endregion

        #region 输入系统

        /// <summary>获取当前帧的玩家输入</summary>
        TInput GetPlayerInput<TInput>(int playerId) where TInput : struct;

        /// <summary>输入是否可用（如网络延迟时）</summary>
        bool IsInputAvailable(int playerId);

        #endregion

        #region 资源管理

        /// <summary>加载资源</summary>
        TResource LoadResource<TResource>(string path) where TResource : class;

        /// <summary>实例化预制体</summary>
        IEntityView InstantiatePrefab(string prefabPath, FPVector3 position);

        #endregion

        #region 日志和调试

        /// <summary>输出日志</summary>
        void Log(LogLevel level, string message);

        /// <summary>绘制调试图形（如碰撞框）</summary>
        void DrawDebugLine(FPVector3 start, FPVector3 end, Color color);
        void DrawDebugBox(FPVector3 center, FPVector3 size, Color color);

        #endregion

        #region 生命周期

        /// <summary>初始化适配器</summary>
        void Initialize();

        /// <summary>每帧更新前调用</summary>
        void BeforeTick(Frame frame);

        /// <summary>每帧更新后调用</summary>
        void AfterTick(Frame frame);

        /// <summary>关闭适配器</summary>
        void Shutdown();

        #endregion
    }

    /// <summary>
    /// 实体视图接口（引擎对象的抽象）
    /// </summary>
    public interface IEntityView
    {
        /// <summary>关联的实体</summary>
        Entity Entity { get; }

        /// <summary>是否有效</summary>
        bool IsValid { get; }

        /// <summary>设置位置</summary>
        void SetPosition(FPVector3 position);

        /// <summary>设置旋转</summary>
        void SetRotation(FPQuaternion rotation);

        /// <summary>设置激活状态</summary>
        void SetActive(bool active);

        /// <summary>销毁视图</summary>
        void Destroy();
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 颜色结构（引擎无关）
    /// </summary>
    public struct Color
    {
        public byte R, G, B, A;

        public Color(byte r, byte g, byte b, byte a = 255)
        {
            R = r; G = g; B = b; A = a;
        }

        public static Color Red => new Color(255, 0, 0);
        public static Color Green => new Color(0, 255, 0);
        public static Color Blue => new Color(0, 0, 255);
    }
}
