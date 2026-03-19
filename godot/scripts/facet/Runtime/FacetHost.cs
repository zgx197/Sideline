#nullable enable

using Godot;

namespace Sideline.Facet.Runtime
{
	/// <summary>
	/// Facet 全局宿主入口。
	/// </summary>
	public partial class FacetHost : Node
	{
		/// <summary>
		/// 最小启动验证日志标记。
		/// 启动主场景后可直接搜索该文本，确认 Facet 宿主已接入。
		/// </summary>
		public const string StartupVerificationMarker = "FacetHost 启动验证成功";

		[Signal]
		public delegate void InitializedEventHandler();

		[Export]
		public bool AutoInitialize { get; set; } = true;

		[Export]
		public bool EnableDebugLogging { get; set; } = true;

		[Export]
		public bool EnableHotReload { get; set; } = true;

		[Export]
		public bool EnablePageCacheByDefault { get; set; } = true;

		[Export(PropertyHint.Range, "1,128,1")]
		public int DefaultPageCacheCapacity { get; set; } = 8;

		/// <summary>
		/// 当前宿主实例。
		/// </summary>
		public static FacetHost? Instance { get; private set; }

		/// <summary>
		/// 是否已完成初始化。
		/// </summary>
		public bool IsInitialized { get; private set; }

		/// <summary>
		/// Facet 当前配置。
		/// </summary>
		public FacetConfig Config { get; private set; } = FacetConfig.Default;

		/// <summary>
		/// Facet 当前服务容器。
		/// </summary>
		public FacetServices Services { get; private set; } = new();

		/// <summary>
		/// Facet 当前日志器。
		/// </summary>
		public IFacetLogger Logger { get; private set; } = new FacetLogger(FacetConfig.Default.MinimumLogLevel);

		/// <summary>
		/// Facet 当前运行时上下文。
		/// </summary>
		public FacetRuntimeContext Context { get; private set; } = null!;

		public override void _EnterTree()
		{
			if (Instance != null && Instance != this)
			{
				GD.PushWarning("[Facet][Host] 检测到多个 FacetHost 实例，后进入的实例将覆盖前一个引用。");
			}

			Instance = this;
		}

		public override void _Ready()
		{
			if (!AutoInitialize)
			{
				GD.Print($"[Facet][Bootstrap] FacetHost 已加载，等待手动初始化。Path={GetPath()}");
				return;
			}

			GD.Print($"[Facet][Bootstrap] FacetHost 自动初始化开始。Path={GetPath()}");
			Initialize();
		}

		/// <summary>
		/// 初始化 Facet 宿主。
		/// </summary>
		public void Initialize()
		{
			if (IsInitialized)
			{
				Logger.Warning("Host", "FacetHost 重复初始化请求已忽略。");
				return;
			}

			Config = BuildConfig();
			Services = new FacetServices();
			Logger = new FacetLogger(Config.MinimumLogLevel);
			Context = new FacetRuntimeContext(Config, Services, Logger);

			RegisterCoreServices();

			IsInitialized = true;
			LogStartupSummary();
			EmitSignal(SignalName.Initialized);
		}

		/// <summary>
		/// 重置宿主运行时状态。
		/// </summary>
		public void ResetHost()
		{
			IsInitialized = false;
			Services = new FacetServices();
			Config = FacetConfig.Default;
			Logger = new FacetLogger(Config.MinimumLogLevel);
			Context = new FacetRuntimeContext(Config, Services, Logger);
		}

		/// <summary>
		/// 获取必须存在的 Facet 服务。
		/// </summary>
		public TService GetRequired<TService>() where TService : class
		{
			return Services.GetRequired<TService>();
		}

		private FacetConfig BuildConfig()
		{
			return new FacetConfig
			{
				EnableDebugLogging = EnableDebugLogging,
				EnableHotReload = EnableHotReload,
				EnablePageCacheByDefault = EnablePageCacheByDefault,
				DefaultPageCacheCapacity = DefaultPageCacheCapacity,
			};
		}

		private void RegisterCoreServices()
		{
			Services.RegisterSingleton(Config);
			Services.RegisterSingleton(Logger);
			Services.RegisterSingleton((FacetLogger)Logger);
			Services.RegisterSingleton(Context);
		}

		private void LogStartupSummary()
		{
			Logger.Info(
				"Bootstrap",
				$"{StartupVerificationMarker}。Path={GetPath()} AutoInitialize={AutoInitialize} HotReload={Config.EnableHotReload} PageCache={Config.EnablePageCacheByDefault} CacheCapacity={Config.DefaultPageCacheCapacity}");
		}
	}
}
