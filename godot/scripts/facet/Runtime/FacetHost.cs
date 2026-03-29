#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
using Sideline.Facet.Extensions.RedDot;
using Sideline.Facet.Layout;
using Sideline.Facet.Lua;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.UI;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 闂備胶顭堢换鍫ュ礉瀹€鍕剳妞ゆ帒鍊甸崑鎾绘偡閼割兛绮ч柣搴ㄦ涧閻倸鐣峰Δ鍛ㄩ柕澶堝劤缁嬪姊?
    /// </summary>
    public partial class FacetHost : Node
    {
        private static readonly ProjectionKey RuntimeProbeProjectionKey = FacetProjectionKeys.RuntimeProbe;
        private static readonly ProjectionKey RuntimeMetricsProjectionKey = FacetProjectionKeys.RuntimeMetrics;
        private const string LuaHotReloadCurrentPageTestShortcutDescription = "Ctrl+Shift+F10";
        private const string LuaHotReloadDungeonTestShortcutDescription = "Ctrl+Shift+F11";
        private const double EditorHotReloadLabPollIntervalSeconds = 0.25d;

        private double _hotReloadPollTimer;
        private double _editorHotReloadLabPollTimer;
        private string? _lastHandledHotReloadLabRequestId;
        private string? _lastHandledLayoutLabRequestId;
        private string? _lastRuntimeDiagnosticsFingerprint;
        private FacetRuntimeEnvironment _runtimeEnvironment = null!;

        /// <summary>
        /// 闂備礁鎼悧鍐磻閹剧粯鍊堕煫鍥ㄦ尵缁犱即鏌熼姘跺弰鐎规洘绻堟俊鎼佸煛閸愩劎鐓戦梺鑽ゅТ濞诧綁鎮為敃鍌涘亯闁挎繂娲︽刊濂告煠閸撴彃鍘撮柛?
        /// 闂備礁鎲￠崙褰掑垂閻楀牊鍙忛柍鍝勫€婚埢鏃堟偣閸パ冪骇缂併劍宀搁弻锟犲椽娴ｅ摜浠╅梺璇″枟椤ㄥ﹤鐣烽敐澶樻晬婵炲棗娴烽棄宥夋⒑閻戔晜娅呴柣掳鍔庨弫顕€骞橀鑲╊啇濠电姴锕ら幊搴ㄥ磻瀹ュ鍋ｉ柛銉ｅ劚閸旀粓鏌＄€ｎ亜鏆ｇ€殿喚鏁婚、妤佺節閸曨収妲风紓浣鸿檸閸欏酣宕板☉銏╂晣?Facet 闂佽娴烽幊鎾诲Φ濡皷鍋撻棃娑滃妞ゆ柨绻樺畷锝嗗緞婵犲偆妲婚梻浣侯焾缁诲牓宕曢鐐茬劦?
        /// </summary>
        public const string StartupVerificationMarker = "FacetHost 闂備礁鎲￠崙褰掑垂閻楀牊鍙忛柍鍝勫亞濞堢晫鈧厜鍋撻柛鎰典簼椤秹姊洪悷鎵憼闁告梹娲橀幈?";

        [Signal]
        public delegate void InitializedEventHandler();

        [Export]
        public bool AutoInitialize { get; set; } = true;

        [Export]
        public bool EnableDebugLogging { get; set; } = true;

        [Export]
        public bool EnableStructuredLogging { get; set; } = true;

        [Export]
        public string StructuredLogPath { get; set; } = "user://logs/facet-structured.jsonl";

        [Export(PropertyHint.Range, "32,2048,32")]
        public int StructuredLogBufferCapacity { get; set; } = 256;

        [Export(PropertyHint.Range, "1,50,1")]
        public int StructuredLogHistoryLimit { get; set; } = 10;

        [Export]
        public bool EnableConsoleMirrorLogging { get; set; } = true;

        [Export]
        public string ConsoleMirrorLogPath { get; set; } = "user://logs/facet-console.log";

        [Export(PropertyHint.Range, "1,50,1")]
        public int ConsoleMirrorLogHistoryLimit { get; set; } = 10;

        [Export]
        public bool EnableHotReload { get; set; } = true;

        [Export(PropertyHint.Range, "0.1,5,0.1")]
        public double HotReloadPollIntervalSeconds { get; set; } = 0.5d;

        [Export]
        public bool EnableLuaHotReloadTestShortcut { get; set; } = false;

        [Export]
        public bool EnablePageCacheByDefault { get; set; } = true;

        [Export(PropertyHint.Range, "1,128,1")]
        public int DefaultPageCacheCapacity { get; set; } = 8;

        /// <summary>
        /// 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯碱暯閸嬫捇鎮烽懜顑跨钵闁诲酣娼ч惉濂稿焵椤掆偓濠€閬嶅磻濡吋顐介柕澶嗘櫅杩?
        /// </summary>
        public static FacetHost? Instance { get; private set; }

        /// <summary>
        /// 闂備礁鎼€氱兘宕规导鏉戠畾濞达絽澹婇崯鍛存煙缂佹ê绗ч柡鍡樺哺閺岀喓鈧稒锚婵洭鏌涢妸銉т粵闁逛究鍔庨埀顒婄秵娴滄粓顢氳閺?
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯挎珪娴溿倝鏌熼柇锕€鐏遍柡鈧挊澹濆綊宕楅梻缈犲闂佷紮缂氱划娆忣嚕娴犲鐐婃い蹇撴椤岸姊?
        /// </summary>
        public string CurrentSessionId => Logger is FacetLogger facetLogger ? facetLogger.SessionId : string.Empty;

        /// <summary>
        /// Facet 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯肩帛閻撯偓閻庡箍鍎卞ú銊╁几閸岀偞鐓?
        /// </summary>
        public FacetConfig Config { get; private set; } = FacetConfig.Default;

        /// <summary>
        /// Facet 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯款嚙鐎氬鈧箍鍎遍幊搴綖閵堝鍊甸柛锔诲幗閸も偓濠电偛鎳忕敮鈩冧繆?
        /// </summary>
        public FacetServices Services { get; private set; } = new();

        /// <summary>
        /// Facet 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯款嚙缁秹鏌曟径鍫濆姢缂佺姴鐖奸弻娑㈡晲閸愩劌惟闂?
        /// </summary>
        public IFacetLogger Logger { get; private set; } = CreateLogger(FacetConfig.Default);

        /// <summary>
        /// Facet 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯挎珪娴溿倝鏌熼柇锕€鐏遍柡鈧禒瀣厸闁告劏鏅滈弸鍕磼濡も偓閸婂湱绮欐径灞稿亾閿濆骸浜濋柣搴櫍閺?
        /// </summary>
        public FacetRuntimeContext Context { get; private set; } = null!;

        public override void _EnterTree()
        {
            if (Instance != null && Instance != this)
            {
                GD.PushWarning("[Facet][Host] 婵犵妲呴崑鈧柛瀣尰缁绘盯寮堕幋顓炲壈闂佺硶鏅涢張顒€顕ラ崟顐僵妞ゆ劧绠戝▓?FacetHost 闂佽楠稿﹢閬嶅磻濡吋顐介柕澶嗘櫆閺咁剟鎮橀悙璺盒撻柛濠勬暬瀵爼鍩￠崒婧炬闂佸憡鐟ュΛ婵嬪箚閸愵喖绀嬫い鎰╁€栧鏍ㄧ箾閹剧澹樻い鎴濇嚇閹啴濮€閵忊€虫毇闂佺硶鍓濋悷锔惧娴煎瓨鐓曢柟鎯х－灏忕紓浣规煥閻°劎绮欐径鎰垫晜闁告洦鍘鹃埞娑㈡⒑濞茬粯濞囬柛鏂匡躬閸┾偓?");
            }

            Instance = this;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ResetHost();
        }

        public override void _Ready()
        {
            FacetPlainTextLogEncoding.EnsureGodotLogUtf8Bom();

            if (!AutoInitialize)
            {
                GD.Print($"[Facet][Bootstrap] FacetHost 闁诲海鎳撻幉陇銇愰崘鈺傚弿闁绘劕鐡ㄦ慨婊堟煛閸屾ê鈧绮堟径宀€纾兼繛鎴烇供濡插摜绱掗崜浣告灈妤犵偛绉堕埀顒婄秵娴滄粏顤勯梻浣告啞鐢鎹㈤崒鐑囩稏闁圭増婢樼粈宀勬煛瀹ュ啫鍔楅柛瀣尰缁楃喓绱炵槐鍗?{GetPath()}");
                return;
            }

            GD.Print($"[Facet][Bootstrap] FacetHost 闂備胶鍘ч〃搴㈢濠婂嫭鍙忛柍鍝勬噹缁€鍡樼節闂堟稒锛嶆繛鍏碱殜閺屾盯寮介妸褍鈪圭紓浣介哺閸ㄥ爼骞堥妸褉鍋撻敐鍐ㄥΨ闁稿鎸荤粭鐔虹礊缁卞崣={GetPath()}");
            Initialize();
        }

        public override void _Process(double delta)
        {
            if (!IsInitialized)
            {
                return;
            }

            _editorHotReloadLabPollTimer += delta;
            if (_editorHotReloadLabPollTimer >= EditorHotReloadLabPollIntervalSeconds)
            {
                _editorHotReloadLabPollTimer = 0.0d;
                PollEditorHotReloadLabRequests();
                PollEditorLayoutLabRequests();
                PublishRuntimeDiagnosticsSnapshot();
            }

            if (!Config.EnableHotReload)
            {
                return;
            }

            _hotReloadPollTimer += delta;
            if (_hotReloadPollTimer < Config.HotReloadPollIntervalSeconds)
            {
                return;
            }

            _hotReloadPollTimer = 0;
            Services.GetRequired<LuaReloadCoordinator>().Poll("host.poll");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsInitialized || !EnableLuaHotReloadTestShortcut)
            {
                return;
            }

            if (@event is not InputEventKey keyEvent ||
                !keyEvent.Pressed ||
                keyEvent.Echo ||
                !keyEvent.CtrlPressed ||
                !keyEvent.ShiftPressed)
            {
                return;
            }

            bool handled = keyEvent.Keycode switch
            {
                Key.F10 => TryRunLuaHotReloadRoundTripTest(reason: "host.shortcut"),
                Key.F11 => TryRunDungeonLuaHotReloadRoundTripTest(reason: "host.shortcut.dungeon"),
                _ => false,
            };

            if (handled)
            {
                GetViewport()?.SetInputAsHandled();
            }
        }

        /// <summary>
        /// 闂備礁鎲＄敮妤冩崲閸岀儑缍栭柟鐗堟緲缁€?Facet 闂佽娴烽幊鎾诲Φ濡皷鍋撻棃娑氱劯濠?
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
            {
                Logger.Warning("Host", "FacetHost 闂傚倷鐒﹁ぐ鍐矓閹绢啟鍥蓟閵夈儳顦┑鐘绘涧濡厼顭囧Δ鍛厱闁哄秲鍔庢晶铏亜閺囥劌澧弫鍫ユ煕鐏炴崘澹橀柛銈嗗浮閻擃偊宕堕埡鍌涚彧闂佸搫妫欓崝娆愪繆?, null");
                return;
            }

            _runtimeEnvironment = FacetRuntimeEnvironment.Detect();
            Config = BuildConfig();
            Services = new FacetServices();
            Logger = CreateLogger(Config);
            Context = new FacetRuntimeContext(Config, Services, Logger);
            _hotReloadPollTimer = 0;
            _editorHotReloadLabPollTimer = 0;
            _lastHandledHotReloadLabRequestId = null;
            _lastHandledLayoutLabRequestId = null;
            _lastRuntimeDiagnosticsFingerprint = null;

            RegisterCoreServices();

            IsInitialized = true;
            SetProcess(true);
            SetProcessUnhandledInput(EnableLuaHotReloadTestShortcut);
            LogStartupSummary();
            CleanupConsumedLabRequests();
            PublishHotReloadLabIdleStatus();
            PublishLayoutLabIdleStatus();
            PublishRuntimeDiagnosticsSnapshot(force: true);
            EmitSignal("Initialized");
        }

        /// <summary>
        /// 闂傚倷鐒﹁ぐ鍐矓閸洘鍋柛鈩冪憿閸嬫捇鎮烽懜顑跨钵闁诲酣娼ч張顒€顭囨繝姘疀妞ゆ牭绲鹃弬鈧梻浣告惈閸燁偅鏅舵惔銏″弿闁归棿绀佺粻鎴澝归敐鍥舵毌闁?
        /// </summary>
        public void ResetHost()
        {
            IsInitialized = false;
            Services = new FacetServices();
            Config = FacetConfig.Default;
            Logger = CreateLogger(Config);
            Context = new FacetRuntimeContext(Config, Services, Logger);
            _hotReloadPollTimer = 0;
            _editorHotReloadLabPollTimer = 0;
            _lastHandledHotReloadLabRequestId = null;
            _lastHandledLayoutLabRequestId = null;
            _lastRuntimeDiagnosticsFingerprint = null;
            _runtimeEnvironment = FacetRuntimeEnvironment.Detect();
            SetProcess(false);
            SetProcessUnhandledInput(false);
        }

        /// <summary>
        /// 濠电偞鍨堕幑渚€顢氳閹便劑鍩￠崨顓狀吋閻熸粍鍨块妴渚€骞嬮悙纰樻灃闂侀€炲苯澧柍?Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌﹀Ο渚Ш缂佹劖妫冨鍫曞煛閸屾稈鎷圭紓浣风椤曨參骞忛悩璇插嵆婵☆垰鍢叉禍?
        /// 濠殿喗甯楃粙鎺椻€﹂崼銉晣缂備焦锚椤曡鲸淇婇姘儓閻㈩垬鍎遍湁闁挎繂鐗婄涵鍫曟煛娴ｉ潧鈧盯濡甸幇鏉跨闁告劕妯婇弸鈧┑锛勫亼閸婃寮拠宸劷闁绘鐗婃禍銈夋煙闁箑鐏遍柡鈧禒瀣厸闁告劦鍘奸悡鎰熆瑜嶇粔鍓佹閹烘绠ｆ繝闈涚墛濮ｅ酣姊?Lua 闂備胶顢婇惌鍥礃閵娧冨箑闂備線娼荤徊濠氬礉韫囨稑绀堝┑鍌滎焾鐎氬銇勯幋锔芥殰闁?
        /// </summary>
        public bool TryRunLuaHotReloadRoundTripTest(string? scriptId = null, string reason = "manual")
        {
            if (!IsInitialized || !Services.TryGet(out LuaHotReloadTestService? testService) || testService == null)
            {
                Logger.Warning(
                    "Lua.HotReload.Test",
                    "Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌ら幁鎺戝姢闁靛牊鎸抽幃褰掑炊閵婏妇顦銈嗘处閸撴瑦鏅ラ梺绋挎湰缁诲秴顭囬崼鏇犲彄闁搞儮鏅滃▍鏇㈡煛閸℃ê濮嶉柡浣哥Ф娴狅箓宕滆閸嬬偤鏌ｉ悩鍙夊婵炲弶锚閿曘垽宕堕鈧粈澶愭煃閵夈儳锛嶆慨锝忕秮閺岋繝宕奸锛勭泿濠殿喗菧閸斿海妲愰幒妤婃晜闁稿本顨呮禍?",
                    new Dictionary<string, object?>
                    {
                        ["requestedScriptId"] = scriptId,
                        ["reason"] = reason,
                        ["isInitialized"] = IsInitialized,
                    });
                return false;
            }

            return testService.TryRunRoundTripTest(scriptId, reason);
        }

        public bool TryRunDungeonLuaHotReloadRoundTripTest(string reason = "manual")
        {
            return TryRunLuaHotReloadRoundTripTest(FacetLuaScriptIds.DungeonRuntimeController, reason);
        }

        /// <summary>
        /// 闂備礁鍚嬮崕鎶藉床閼艰翰浜归柛銉簽閻も偓闂佸憡绋掗悾顏堝焵椤掆偓缁绘帡鍩€椤掍胶鈯曢柨姘節閳ь剟顢旈崼鐔峰壄?Facet 闂備礁鎼悧鍡欑矓鐎涙ɑ鍙忛柣鏃傚帶杩?
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
                EnableStructuredLogging = EnableStructuredLogging,
                StructuredLogPath = _runtimeEnvironment.ResolveLogPath(StructuredLogPath, "facet-structured.jsonl"),
                StructuredLogBufferCapacity = StructuredLogBufferCapacity,
                StructuredLogHistoryLimit = StructuredLogHistoryLimit,
                EnableConsoleMirrorLogging = EnableConsoleMirrorLogging,
                ConsoleMirrorLogPath = _runtimeEnvironment.ResolveLogPath(ConsoleMirrorLogPath, "facet-console.log"),
                ConsoleMirrorLogHistoryLimit = ConsoleMirrorLogHistoryLimit,
                EnableHotReload = EnableHotReload,
                HotReloadPollIntervalSeconds = HotReloadPollIntervalSeconds,
                EnablePageCacheByDefault = EnablePageCacheByDefault,
                DefaultPageCacheCapacity = DefaultPageCacheCapacity,
            };
        }


        private void RegisterCoreServices()
        {
            LocalCommandBus commandBus = new(Logger);
            LocalQueryBus queryBus = new(Logger);
            ProjectionStore projectionStore = new();
            ProjectionRefreshCoordinator projectionRefreshCoordinator = new(Logger);
            IFacetRuntimeProbeGateway runtimeProbeGateway = new InMemoryFacetRuntimeProbeGateway();
            FacetRuntimeProbeService runtimeProbeService = new(Context, runtimeProbeGateway, CurrentSessionId);
            RuntimeProbeProjectionUpdater runtimeProbeProjectionUpdater = new(Context);
            RuntimeMetricsProjectionUpdater runtimeMetricsProjectionUpdater = new(Context);
            IPageDefinitionSource pageDefinitionSource = new InMemoryPageDefinitionSource(FacetBuiltInPageDefinitions.CreateMainSceneDefinitions());
            UIPageRegistry pageRegistry = new();
            FacetDynamicNodeFactory dynamicNodeFactory = new();
            IFacetGeneratedLayoutStore generatedLayoutStore = new InMemoryFacetGeneratedLayoutStore(FacetBuiltInLayoutDefinitions.CreateGeneratedLayouts());
            IFacetTemplateLayoutStore templateLayoutStore = new InMemoryFacetTemplateLayoutStore(FacetBuiltInLayoutDefinitions.CreateTemplateLayouts());
            SceneLayoutProvider sceneLayoutProvider = new(Logger);
            GeneratedLayoutProvider generatedLayoutProvider = new(generatedLayoutStore, dynamicNodeFactory, Logger);
            TemplateLayoutProvider templateLayoutProvider = new(templateLayoutStore, dynamicNodeFactory, Logger);
            IUILayoutProvider[] layoutProviders =
            {
                sceneLayoutProvider,
                templateLayoutProvider,
                generatedLayoutProvider,
            };
            UIPageLoader pageLoader = new(layoutProviders, Logger);
            UIBindingService bindingService = new(Logger);
            UIRouteService routeService = new();
            UIManager uiManager = new(pageRegistry, pageLoader, routeService, Context, Logger);
            ILuaScriptSource luaScriptSource = _runtimeEnvironment.CreateLuaScriptSource(FacetLuaScriptIds.All);
            RedDotService redDotService = new(Logger);
            FacetRuntimeRedDotProvider runtimeRedDotProvider = new(projectionStore, Logger);
            ManualRedDotProvider manualRedDotProvider = new();
            redDotService.RegisterProvider(runtimeRedDotProvider);
            redDotService.RegisterProvider(manualRedDotProvider);
            ILuaRedDotBridge luaRedDotBridge = new FacetLuaRedDotBridge(redDotService);
            ILuaRuntimeHost luaRuntimeHost = new LuaRuntimeHost(luaScriptSource, luaRedDotBridge);
            LuaReloadCoordinator luaReloadCoordinator = new(uiManager, Logger);
            LuaHotReloadTestService luaHotReloadTestService = new(uiManager, luaReloadCoordinator, luaScriptSource, Logger);

            pageRegistry.RegisterRange(pageDefinitionSource);

            Services.RegisterSingleton(Config);
            Services.RegisterSingleton(Logger);
            Services.RegisterSingleton((FacetLogger)Logger);
            Services.RegisterSingleton(Context);
            Services.RegisterSingleton(_runtimeEnvironment);
            Services.RegisterSingleton<ICommandBus>(commandBus);
            Services.RegisterSingleton(commandBus);
            Services.RegisterSingleton<IQueryBus>(queryBus);
            Services.RegisterSingleton(queryBus);
            Services.RegisterSingleton(projectionStore);
            Services.RegisterSingleton(projectionRefreshCoordinator);
            Services.RegisterSingleton(runtimeProbeGateway);
            Services.RegisterSingleton(runtimeProbeService);
            Services.RegisterSingleton(runtimeProbeProjectionUpdater);
            Services.RegisterSingleton(runtimeMetricsProjectionUpdater);
            Services.RegisterSingleton(pageDefinitionSource);
            Services.RegisterSingleton(pageRegistry);
            Services.RegisterSingleton(dynamicNodeFactory);
            Services.RegisterSingleton(generatedLayoutStore);
            Services.RegisterSingleton(templateLayoutStore);
            Services.RegisterSingleton(sceneLayoutProvider);
            Services.RegisterSingleton(generatedLayoutProvider);
            Services.RegisterSingleton(templateLayoutProvider);
            Services.RegisterSingleton(pageLoader);
            Services.RegisterSingleton(bindingService);
            Services.RegisterSingleton(routeService);
            Services.RegisterSingleton<IUIPageNavigator>(uiManager);
            Services.RegisterSingleton(uiManager);
            Services.RegisterSingleton<IRedDotService>(redDotService);
            Services.RegisterSingleton(redDotService);
            Services.RegisterSingleton(runtimeRedDotProvider);
            Services.RegisterSingleton(manualRedDotProvider);
            Services.RegisterSingleton(luaScriptSource);
            Services.RegisterSingleton(luaRedDotBridge);
            Services.RegisterSingleton(luaRuntimeHost);
            Services.RegisterSingleton(luaReloadCoordinator);
            Services.RegisterSingleton(luaHotReloadTestService);

            commandBus.RegisterHandler<RecordFacetRuntimeProbeCommand>(runtimeProbeService.RecordAsync);
            queryBus.RegisterHandler<FacetRuntimeProbeQuery, FacetRuntimeProbeSnapshot>(runtimeProbeService.QueryCurrentAsync);
            queryBus.RegisterHandler<FacetRuntimeProbeStatusQuery, FacetRuntimeProbeStatusSnapshot>(runtimeProbeService.QueryStatusAsync);

            projectionRefreshCoordinator.Register(runtimeProbeProjectionUpdater);
            projectionRefreshCoordinator.Register(runtimeMetricsProjectionUpdater);
        }

        private void LogStartupSummary()
        {
            Logger.Info(
                "Bootstrap",
                StartupVerificationMarker,
                new Dictionary<string, object?>
                {
                    ["path"] = GetPath().ToString(),
                    ["sessionId"] = CurrentSessionId,
                    ["autoInitialize"] = AutoInitialize,
                    ["hotReload"] = Config.EnableHotReload,
                    ["hotReloadPollIntervalSeconds"] = Config.HotReloadPollIntervalSeconds,
                    ["pageCache"] = Config.EnablePageCacheByDefault,
                    ["cacheCapacity"] = Config.DefaultPageCacheCapacity,
                    ["structuredLogging"] = Config.EnableStructuredLogging,
                    ["structuredLogPath"] = Config.StructuredLogPath,
                    ["structuredLogBufferCapacity"] = Config.StructuredLogBufferCapacity,
                    ["structuredLogHistoryLimit"] = Config.StructuredLogHistoryLimit,
                    ["consoleMirrorLogging"] = Config.EnableConsoleMirrorLogging,
                    ["consoleMirrorLogPath"] = Config.ConsoleMirrorLogPath,
                    ["consoleMirrorLogHistoryLimit"] = Config.ConsoleMirrorLogHistoryLimit,
                    ["runtimeEnvironment"] = _runtimeEnvironment.IsEditor ? "editor" : "runtime",
                    ["usesPackagedResources"] = _runtimeEnvironment.UsesPackagedResources,
                    ["projectRootPathAvailable"] = !string.IsNullOrWhiteSpace(_runtimeEnvironment.ProjectRootPath),
                    ["commandBus"] = nameof(LocalCommandBus),
                    ["queryBus"] = nameof(LocalQueryBus),
                    ["projectionStore"] = nameof(ProjectionStore),
                    ["projectionRefreshCoordinator"] = nameof(ProjectionRefreshCoordinator),
                    ["registeredProjectionUpdaters"] = Context.ProjectionRefreshCoordinator.Count,
                    ["runtimeProbeGateway"] = nameof(InMemoryFacetRuntimeProbeGateway),
                    ["registeredPages"] = Services.GetRequired<UIPageRegistry>().Count,
                    ["pageDefinitionSource"] = nameof(InMemoryPageDefinitionSource),
                    ["layoutProviders"] = string.Join(",", nameof(SceneLayoutProvider), nameof(TemplateLayoutProvider), nameof(GeneratedLayoutProvider)),
                    ["bindingService"] = nameof(UIBindingService),
                    ["routeService"] = nameof(UIRouteService),
                    ["backStackDepth"] = Services.GetRequired<UIRouteService>().Count,
                });

            VerifyApplicationBoundary();
            VerifyProjectionLayer();
            VerifyPageDefinitions();
            VerifyLuaRuntime();
            VerifyRedDotRuntime();
            VerifyAdvancedLayouts();
            VerifyRuntimeDiagnostics();
        }

        private void PublishHotReloadLabIdleStatus()
        {
            string message = Config.EnableHotReload
                ? "Hot reload lab is idle and waiting for requests."
                : "Hot reload is disabled in the current runtime configuration.";

            FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
            {
                State = FacetHotReloadLabBridge.StateIdle,
                Success = null,
                Message = message,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = Services.TryGet(out UIManager? uiManager) ? uiManager?.CurrentPageId ?? string.Empty : string.Empty,
            });
        }

        private void PublishLayoutLabIdleStatus()
        {
            FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
            {
                State = FacetLayoutLabBridge.StateIdle,
                Success = null,
                Message = "Layout Lab 当前空闲，等待新的打开请求。",
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = Services.TryGet(out UIManager? uiManager) ? uiManager?.CurrentPageId ?? string.Empty : string.Empty,
            });
        }

        private void PublishRuntimeDiagnosticsSnapshot(bool force = false)
        {
            try
            {
                FacetRuntimeDiagnosticsSnapshot snapshot = CreateRuntimeDiagnosticsSnapshot();
                string fingerprint = CreateRuntimeDiagnosticsFingerprint(snapshot);
                if (!force && string.Equals(_lastRuntimeDiagnosticsFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                FacetRuntimeDiagnosticsBridge.SaveSnapshot(snapshot);
                _lastRuntimeDiagnosticsFingerprint = fingerprint;
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Tooling.Diagnostics",
                    "Failed to publish runtime diagnostics snapshot.",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private FacetRuntimeDiagnosticsSnapshot CreateRuntimeDiagnosticsSnapshot()
        {
            UIPageRegistry pageRegistry = Services.GetRequired<UIPageRegistry>();
            UIManager uiManager = Services.GetRequired<UIManager>();
            ProjectionStore projectionStore = Services.GetRequired<ProjectionStore>();
            ILuaScriptSource luaScriptSource = Services.GetRequired<ILuaScriptSource>();
            IRedDotService redDotService = Services.GetRequired<IRedDotService>();
            IFacetGeneratedLayoutStore generatedLayoutStore = Services.GetRequired<IFacetGeneratedLayoutStore>();
            IFacetTemplateLayoutStore templateLayoutStore = Services.GetRequired<IFacetTemplateLayoutStore>();

            List<FacetRuntimeRegisteredPageSnapshot> registeredPages = new();
            foreach (UIPageDefinition definition in pageRegistry.GetAll())
            {
                registeredPages.Add(new FacetRuntimeRegisteredPageSnapshot
                {
                    PageId = definition.PageId,
                    LayoutType = definition.LayoutType.ToString(),
                    LayoutPath = definition.LayoutPath,
                    Layer = definition.Layer,
                    CachePolicy = definition.CachePolicy.ToString(),
                    ControllerScript = definition.ControllerScript ?? string.Empty,
                });
            }

            registeredPages.Sort(static (left, right) => string.Compare(left.PageId, right.PageId, StringComparison.OrdinalIgnoreCase));

            List<FacetRuntimePageRuntimeSnapshot> activeRuntimes = new();
            foreach (UIPageRuntime runtime in uiManager.GetPageRuntimesSnapshot())
            {
                UIBindingDiagnosticsSnapshot? bindingDiagnostics = runtime.BindingScope?.GetDiagnosticsSnapshot();
                activeRuntimes.Add(new FacetRuntimePageRuntimeSnapshot
                {
                    PageId = runtime.Definition.PageId,
                    IsCurrentPage = string.Equals(runtime.Definition.PageId, uiManager.CurrentPageId, StringComparison.OrdinalIgnoreCase),
                    State = runtime.State.ToString(),
                    LayoutType = runtime.Definition.LayoutType.ToString(),
                    ControllerScript = runtime.LuaControllerScript ?? runtime.Definition.ControllerScript ?? string.Empty,
                    HasLuaController = runtime.HasLuaController,
                    LuaControllerVersionToken = runtime.LuaControllerVersionToken ?? string.Empty,
                    PageRootPath = runtime.PageRoot.GetPath().ToString(),
                    BindingScope = bindingDiagnostics == null
                        ? null
                        : new FacetRuntimeBindingScopeSnapshot
                        {
                            ScopeId = bindingDiagnostics.ScopeId,
                            BindingCount = bindingDiagnostics.BindingCount,
                            RefreshCount = bindingDiagnostics.RefreshCount,
                            LastRefreshReason = bindingDiagnostics.LastRefreshReason ?? string.Empty,
                        },
                });
            }

            activeRuntimes.Sort(static (left, right) =>
            {
                int currentComparison = right.IsCurrentPage.CompareTo(left.IsCurrentPage);
                return currentComparison != 0
                    ? currentComparison
                    : string.Compare(left.PageId, right.PageId, StringComparison.OrdinalIgnoreCase);
            });

            List<string> projectionKeys = new();
            foreach (ProjectionKey key in projectionStore.GetKeysSnapshot())
            {
                projectionKeys.Add(key.ToString());
            }

            List<string> luaRegisteredScripts = new(luaScriptSource.GetRegisteredScripts());
            luaRegisteredScripts.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> redDotPaths = new(redDotService.GetRegisteredPaths());
            redDotPaths.Sort(StringComparer.OrdinalIgnoreCase);

            List<FacetRuntimeValidationResultSnapshot> validationResults = CreateRuntimeValidationResults(
                pageRegistry,
                uiManager,
                projectionStore,
                luaScriptSource,
                redDotService,
                generatedLayoutStore,
                templateLayoutStore,
                registeredPages,
                activeRuntimes,
                projectionKeys,
                redDotPaths);

            int validationPassedCount = CountValidationResults(validationResults, "Pass");
            int validationWarningCount = CountValidationResults(validationResults, "Warning");
            int validationFailedCount = CountValidationResults(validationResults, "Fail");

            return new FacetRuntimeDiagnosticsSnapshot
            {
                RuntimeSessionId = CurrentSessionId,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                CurrentPageId = uiManager.CurrentPageId ?? string.Empty,
                BackStackDepth = uiManager.BackStackDepth,
                RegisteredPageCount = registeredPages.Count,
                RegisteredPages = registeredPages,
                ActiveRuntimeCount = activeRuntimes.Count,
                ActiveRuntimes = activeRuntimes,
                ProjectionCount = projectionStore.Count,
                ProjectionKeys = projectionKeys,
                LuaRegisteredScriptCount = luaRegisteredScripts.Count,
                LuaRegisteredScripts = luaRegisteredScripts,
                RedDotRegisteredPathCount = redDotPaths.Count,
                RedDotPaths = redDotPaths,
                ValidationResultCount = validationResults.Count,
                ValidationPassedCount = validationPassedCount,
                ValidationWarningCount = validationWarningCount,
                ValidationFailedCount = validationFailedCount,
                ValidationResults = validationResults,
            };
        }

        private static List<FacetRuntimeValidationResultSnapshot> CreateRuntimeValidationResults(
            UIPageRegistry pageRegistry,
            UIManager uiManager,
            ProjectionStore projectionStore,
            ILuaScriptSource luaScriptSource,
            IRedDotService redDotService,
            IFacetGeneratedLayoutStore generatedLayoutStore,
            IFacetTemplateLayoutStore templateLayoutStore,
            List<FacetRuntimeRegisteredPageSnapshot> registeredPages,
            List<FacetRuntimePageRuntimeSnapshot> activeRuntimes,
            List<string> projectionKeys,
            List<string> redDotPaths)
        {
            List<FacetRuntimeValidationResultSnapshot> results = new();

            results.Add(CreateValidationResult(
                "page_registry.not_empty",
                isSuccess: registeredPages.Count > 0,
                subject: "UIPageRegistry",
                successMessage: $"Registered pages snapshot contains {registeredPages.Count} entries.",
                failureMessage: "Registered pages snapshot is empty."));

            results.Add(CreateValidationResult(
                "projection.count_consistent",
                isSuccess: projectionStore.Count == projectionKeys.Count,
                subject: "ProjectionStore",
                successMessage: $"Projection key count matches store count: {projectionStore.Count}.",
                failureMessage: $"Projection key count mismatch. count={projectionStore.Count}, keys={projectionKeys.Count}."));

            results.Add(CreateValidationResult(
                "red_dot.count_consistent",
                isSuccess: redDotService.RegisteredPathCount == redDotPaths.Count,
                subject: "RedDotService",
                successMessage: $"Red dot snapshot count matches registered count: {redDotPaths.Count}.",
                failureMessage: $"Red dot snapshot count mismatch. registered={redDotService.RegisteredPathCount}, snapshot={redDotPaths.Count}."));

            foreach (FacetRuntimeRegisteredPageSnapshot page in registeredPages)
            {
                bool layoutResolved = page.LayoutType switch
                {
                    nameof(UIPageLayoutType.Generated) => generatedLayoutStore.TryGet(page.LayoutPath, out _),
                    nameof(UIPageLayoutType.Template) => templateLayoutStore.TryGet(page.LayoutPath, out _),
                    _ => true,
                };

                results.Add(CreateValidationResult(
                    "page.layout_resolved",
                    isSuccess: layoutResolved,
                    subject: page.PageId,
                    successMessage: $"Layout path resolved: {page.LayoutPath}.",
                    failureMessage: $"Layout path could not be resolved: {page.LayoutPath}."));

                if (!string.IsNullOrWhiteSpace(page.ControllerScript))
                {
                    bool scriptRegistered = luaScriptSource.TryGetVersionToken(page.ControllerScript, out _);
                    results.Add(CreateValidationResult(
                        "page.lua_script_registered",
                        isSuccess: scriptRegistered,
                        subject: page.PageId,
                        successMessage: $"Lua controller script is registered: {page.ControllerScript}.",
                        failureMessage: $"Lua controller script is missing: {page.ControllerScript}."));
                }
                else
                {
                    results.Add(CreateValidationResult(
                        "page.lua_script_optional",
                        isSuccess: true,
                        subject: page.PageId,
                        successMessage: "Page does not require a Lua controller script.",
                        failureMessage: string.Empty,
                        severity: "Info"));
                }
            }

            foreach (FacetRuntimePageRuntimeSnapshot runtime in activeRuntimes)
            {
                bool pageRegistered = pageRegistry.Contains(runtime.PageId);
                results.Add(CreateValidationResult(
                    "runtime.page_registered",
                    isSuccess: pageRegistered,
                    subject: runtime.PageId,
                    successMessage: "Runtime page is still registered in the page registry.",
                    failureMessage: "Runtime page is no longer registered in the page registry."));

                bool bindingScopePresent = runtime.BindingScope != null;
                results.Add(CreateValidationResult(
                    "runtime.binding_scope_present",
                    status: bindingScopePresent ? "Pass" : "Warning",
                    subject: runtime.PageId,
                    successMessage: $"Binding scope is available: {runtime.BindingScope?.ScopeId}.",
                    failureMessage: "Binding scope is missing for the runtime page.",
                    severity: "Warning"));
            }

            bool currentPageMatchesRuntime = string.IsNullOrWhiteSpace(uiManager.CurrentPageId) ||
                activeRuntimes.Exists(runtime => string.Equals(runtime.PageId, uiManager.CurrentPageId, StringComparison.OrdinalIgnoreCase));
            results.Add(CreateValidationResult(
                "runtime.current_page_observed",
                status: currentPageMatchesRuntime ? "Pass" : "Warning",
                subject: uiManager.CurrentPageId ?? "<empty>",
                successMessage: "Current page id is represented by the observed runtimes.",
                failureMessage: "Current page id is not represented by the observed runtimes.",
                severity: "Warning"));

            results.Sort(static (left, right) =>
            {
                int severityComparison = CompareValidationSeverity(left, right);
                return severityComparison != 0
                    ? severityComparison
                    : string.Compare(left.Subject, right.Subject, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        private static FacetRuntimeValidationResultSnapshot CreateValidationResult(
            string ruleId,
            bool isSuccess,
            string subject,
            string successMessage,
            string failureMessage,
            string severity = "Error")
        {
            return CreateValidationResult(
                ruleId,
                isSuccess ? "Pass" : "Fail",
                subject,
                successMessage,
                failureMessage,
                severity);
        }

        private static FacetRuntimeValidationResultSnapshot CreateValidationResult(
            string ruleId,
            string status,
            string subject,
            string successMessage,
            string failureMessage,
            string severity = "Error")
        {
            bool isSuccess = string.Equals(status, "Pass", StringComparison.OrdinalIgnoreCase);
            return new FacetRuntimeValidationResultSnapshot
            {
                RuleId = ruleId,
                Severity = isSuccess ? "Info" : severity,
                Status = status,
                Subject = subject,
                Message = isSuccess ? successMessage : failureMessage,
            };
        }

        private static int CountValidationResults(List<FacetRuntimeValidationResultSnapshot> results, string status)
        {
            int count = 0;
            foreach (FacetRuntimeValidationResultSnapshot result in results)
            {
                if (string.Equals(result.Status, status, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareValidationSeverity(FacetRuntimeValidationResultSnapshot left, FacetRuntimeValidationResultSnapshot right)
        {
            return GetValidationSeverityRank(left).CompareTo(GetValidationSeverityRank(right));
        }

        private static int GetValidationSeverityRank(FacetRuntimeValidationResultSnapshot result)
        {
            if (string.Equals(result.Status, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(result.Status, "Fail", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return 2;
        }

        private static string CreateRuntimeDiagnosticsFingerprint(FacetRuntimeDiagnosticsSnapshot snapshot)
        {
            StringBuilder builder = new();
            builder.Append(snapshot.RuntimeSessionId);
            builder.Append('|');
            builder.Append(snapshot.CurrentPageId);
            builder.Append('|');
            builder.Append(snapshot.BackStackDepth);
            builder.Append('|');
            builder.Append(snapshot.RegisteredPageCount);
            builder.Append('|');
            builder.Append(snapshot.ActiveRuntimeCount);
            builder.Append('|');
            builder.Append(snapshot.ProjectionCount);
            builder.Append('|');
            builder.Append(snapshot.LuaRegisteredScriptCount);
            builder.Append('|');
            builder.Append(snapshot.RedDotRegisteredPathCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationResultCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationPassedCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationWarningCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationFailedCount);

            foreach (FacetRuntimeRegisteredPageSnapshot page in snapshot.RegisteredPages)
            {
                builder.Append("|page:");
                builder.Append(page.PageId);
                builder.Append(':');
                builder.Append(page.LayoutType);
                builder.Append(':');
                builder.Append(page.LayoutPath);
                builder.Append(':');
                builder.Append(page.Layer);
                builder.Append(':');
                builder.Append(page.CachePolicy);
                builder.Append(':');
                builder.Append(page.ControllerScript);
            }

            foreach (FacetRuntimePageRuntimeSnapshot runtime in snapshot.ActiveRuntimes)
            {
                builder.Append("|runtime:");
                builder.Append(runtime.PageId);
                builder.Append(':');
                builder.Append(runtime.IsCurrentPage);
                builder.Append(':');
                builder.Append(runtime.State);
                builder.Append(':');
                builder.Append(runtime.LayoutType);
                builder.Append(':');
                builder.Append(runtime.ControllerScript);
                builder.Append(':');
                builder.Append(runtime.HasLuaController);
                builder.Append(':');
                builder.Append(runtime.LuaControllerVersionToken);
                builder.Append(':');
                builder.Append(runtime.PageRootPath);

                if (runtime.BindingScope != null)
                {
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.ScopeId);
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.BindingCount);
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.RefreshCount);
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.LastRefreshReason);
                }
            }

            foreach (string projectionKey in snapshot.ProjectionKeys)
            {
                builder.Append("|projection:");
                builder.Append(projectionKey);
            }

            foreach (string script in snapshot.LuaRegisteredScripts)
            {
                builder.Append("|lua:");
                builder.Append(script);
            }

            foreach (string path in snapshot.RedDotPaths)
            {
                builder.Append("|red_dot:");
                builder.Append(path);
            }

            foreach (FacetRuntimeValidationResultSnapshot result in snapshot.ValidationResults)
            {
                builder.Append("|validation:");
                builder.Append(result.Status);
                builder.Append(':');
                builder.Append(result.Severity);
                builder.Append(':');
                builder.Append(result.RuleId);
                builder.Append(':');
                builder.Append(result.Subject);
                builder.Append(':');
                builder.Append(result.Message);
            }

            return builder.ToString();
        }

        private void PollEditorHotReloadLabRequests()
        {
            if (!FacetHotReloadLabBridge.TryLoadRequest(out FacetHotReloadLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            FacetHotReloadLabBridge.TryLoadStatus(out FacetHotReloadLabStatus? status);
            if (!FacetHotReloadLabBridge.IsPending(request, status))
            {
                return;
            }

            if (string.Equals(_lastHandledHotReloadLabRequestId, request.RequestId, StringComparison.Ordinal))
            {
                return;
            }

            _lastHandledHotReloadLabRequestId = request.RequestId;
            HandleHotReloadLabRequest(request);
        }

        private void PollEditorLayoutLabRequests()
        {
            if (!FacetLayoutLabBridge.TryLoadRequest(out FacetLayoutLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            FacetLayoutLabBridge.TryLoadStatus(out FacetLayoutLabStatus? status);
            if (!FacetLayoutLabBridge.IsPending(request, status))
            {
                return;
            }

            if (string.Equals(_lastHandledLayoutLabRequestId, request.RequestId, StringComparison.Ordinal))
            {
                return;
            }

            _lastHandledLayoutLabRequestId = request.RequestId;
            HandleLayoutLabRequest(request);
        }

        private void HandleHotReloadLabRequest(FacetHotReloadLabRequest request)
        {
            string currentPageId = Services.TryGet(out UIManager? uiManager)
                ? uiManager?.CurrentPageId ?? string.Empty
                : string.Empty;

            FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
            {
                RequestId = request.RequestId,
                Command = request.Command,
                State = FacetHotReloadLabBridge.StateRunning,
                Success = null,
                Message = "闂佸搫顦弲婊堝礉濮椻偓閵嗕線骞嬮敃鈧猾宥夋偣閸濆嫭鎯堥柛銈嗗浮閺岀喖骞侀幒鎴濆闂佽桨绀佸﹢閬嶅箯閻樼粯鐓ラ悗锝庡墯閸曢箖姊洪幐搴ｂ槈闁哄牜鍓熼、妤€顭ㄩ崼婵囧祶闂侀潧顭粻鎴﹀煕閺冨牊鍋?Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌﹀Ο渚Ш缂佹劖妫冨鍫曞煛閸屾稈鎷圭紓浣风椤曨參骞忛悩璇插嵆婵☆垰鍢叉禍?",
                IssuedBy = request.IssuedBy,
                IssuedAtUtc = request.IssuedAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = currentPageId,
            });

            try
            {
                if (!Config.EnableHotReload)
                {
                    FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
                    {
                        RequestId = request.RequestId,
                        Command = request.Command,
                        State = FacetHotReloadLabBridge.StateIgnored,
                        Success = false,
                        Message = "闂佸搫顦弲婊堝礉濮椻偓閵嗕線骞嬮敃鈧猾宥夋煠缁嬭法浠涚€殿喗鎸鹃埀顒侇問閸犳帡宕戦幘缁樼厱婵鍘ч悘锝夋煕鎼达紕绉洪柡灞借嫰椤撳ジ宕奸悢鎭掑仒闂備焦瀵х粙鎴﹀嫉椤掆偓铻為柕鍫濇噳閺嬫牠鏌￠崶鈺佹瀻闁抽攱妫冮幃璺衡槈濡偐鍔┑鈽嗗灟缁€渚€鍩?9 婵犵數鍋炲娆擃敄閸儲鍎婃い鏍仜杩?",
                        IssuedBy = request.IssuedBy,
                        IssuedAtUtc = request.IssuedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        RuntimeSessionId = CurrentSessionId,
                        RuntimePageId = currentPageId,
                    });
                    return;
                }

                bool success = request.Command switch
                {
                    FacetHotReloadLabBridge.CommandCurrentPageRoundTrip => TryRunLuaHotReloadRoundTripTest(reason: "editor.lab.current"),
                    FacetHotReloadLabBridge.CommandDungeonRoundTrip => TryRunDungeonLuaHotReloadRoundTripTest(reason: "editor.lab.dungeon"),
                    _ => false,
                };

                string state = success
                    ? FacetHotReloadLabBridge.StateCompleted
                    : request.Command switch
                    {
                        FacetHotReloadLabBridge.CommandCurrentPageRoundTrip => FacetHotReloadLabBridge.StateFailed,
                        FacetHotReloadLabBridge.CommandDungeonRoundTrip => FacetHotReloadLabBridge.StateFailed,
                        _ => FacetHotReloadLabBridge.StateIgnored,
                    };

                string message = request.Command switch
                {
                    FacetHotReloadLabBridge.CommandCurrentPageRoundTrip when success => "闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯煎仺娴?Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌﹀Ο渚Ш缂佹劖妫冨鍫曞煛閸屾稈鎷圭紓浣风椤曨參骞忛悩璇插嵆闁绘劖褰冨▓銉╂⒒娓氬洤浜濆ǎ鍥閹广垽宕橀鍢?",
                    FacetHotReloadLabBridge.CommandDungeonRoundTrip when success => "闂備線娼荤粻鎾汇€傞敂鍓х當闁告稒娼欓弰銉╁箹濞ｎ剙濡搁柍?Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌﹀Ο渚Ш缂佹劖妫冨鍫曞煛閸屾稈鎷圭紓浣风椤曨參骞忛悩璇插嵆闁绘劖褰冨▓銉╂⒒娓氬洤浜濆ǎ鍥閹广垽宕橀鍢?",
                    FacetHotReloadLabBridge.CommandCurrentPageRoundTrip => "闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯煎仺娴?Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌﹀Ο渚Ш缂佹劖妫冨鍫曞煛閸屾稈鎷圭紓浣风椤曨參骞忛悩璇插嵆闁绘梻顭堢槐锕傛⒒娓氬洤浜濆ǎ鍥閹广垽宕橀鑺ユ珫閻庡厜鍋撻柛鎰劤濞堟煡姊洪崫鍕潶闁稿骸鍟块敃?Lua.HotReload.Test 闂備礁鎼崯銊╁磿鏉堚晜宕查柡鍐ㄧ墕杩?",
                    FacetHotReloadLabBridge.CommandDungeonRoundTrip => "闂備線娼荤粻鎾汇€傞敂鍓х當闁告稒娼欓弰銉╁箹濞ｎ剙濡搁柍?Lua 闂備胶绮崺鍫ュ矗閸愵喖闂柛娑橈攻婵粓鏌﹀Ο渚Ш缂佹劖妫冨鍫曞煛閸屾稈鎷圭紓浣风椤曨參骞忛悩璇插嵆闁绘梻顭堢槐锕傛⒒娓氬洤浜濆ǎ鍥閹广垽宕橀鑺ユ珫閻庡厜鍋撻柛鎰劤濞堟煡姊洪崫鍕潶闁稿骸鍟块敃?Lua.HotReload.Test 闂備礁鎼崯銊╁磿鏉堚晜宕查柡鍐ㄧ墕杩?",
                    _ => "闂備浇銆€閸嬫捇鎮归崫鍕儓闁绘挸鍊块弻锟犲醇椤愶紕鏁栭梺缁樻惈缁绘繈骞?Hot Reload Lab 闂備礁鎲＄粙鎺楀垂濠靛绠柕鍫濐槹閺咁剟鎮橀悙鑸殿棄闁搞倖甯￠悡顐﹀炊閳哄倹鐝梺鍝勬閸旀瑦淇?",
                };

                currentPageId = Services.TryGet(out uiManager)
                    ? uiManager?.CurrentPageId ?? string.Empty
                    : currentPageId;

                FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
                {
                    RequestId = request.RequestId,
                    Command = request.Command,
                    State = state,
                    Success = success,
                    Message = message,
                    IssuedBy = request.IssuedBy,
                    IssuedAtUtc = request.IssuedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    RuntimeSessionId = CurrentSessionId,
                    RuntimePageId = currentPageId,
                });
            }
            finally
            {
                FacetHotReloadLabBridge.DeleteRequest();
            }
        }

        private void HandleLayoutLabRequest(FacetLayoutLabRequest request)
        {
            string currentPageId = Services.TryGet(out UIManager? uiManager)
                ? uiManager?.CurrentPageId ?? string.Empty
                : string.Empty;

            FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
            {
                RequestId = request.RequestId,
                Command = request.Command,
                State = FacetLayoutLabBridge.StateRunning,
                Success = null,
                Message = "Layout Lab 请求处理中，正在切换目标页面。",
                IssuedBy = request.IssuedBy,
                IssuedAtUtc = request.IssuedAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = currentPageId,
            });

            try
            {
                string? targetPageId = request.Command switch
                {
                    FacetLayoutLabBridge.CommandOpenGeneratedLayoutLab => UIPageIds.GeneratedLayoutLab,
                    FacetLayoutLabBridge.CommandOpenTemplateLayoutLab => UIPageIds.TemplateLayoutLab,
                    _ => null,
                };

                if (string.IsNullOrWhiteSpace(targetPageId))
                {
                    FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                    {
                        RequestId = request.RequestId,
                        Command = request.Command,
                        State = FacetLayoutLabBridge.StateIgnored,
                        Success = false,
                        Message = "Layout Lab 请求未被处理，请检查命令和目标页面配置。",
                        IssuedBy = request.IssuedBy,
                        IssuedAtUtc = request.IssuedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        RuntimeSessionId = CurrentSessionId,
                        RuntimePageId = currentPageId,
                    });
                    return;
                }

                if (uiManager == null)
                {
                    FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                    {
                        RequestId = request.RequestId,
                        Command = request.Command,
                        State = FacetLayoutLabBridge.StateFailed,
                        Success = false,
                        Message = "Layout Lab 请求执行失败，UIManager 未就绪。",
                        IssuedBy = request.IssuedBy,
                        IssuedAtUtc = request.IssuedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        RuntimeSessionId = CurrentSessionId,
                        RuntimePageId = currentPageId,
                    });
                    return;
                }

                UIPageRuntime runtime = uiManager.Open(targetPageId, arguments: null, pushHistory: false);
                currentPageId = uiManager.CurrentPageId ?? currentPageId;

                Logger.Info(
                    "UI.Layout",
                    "闂佹眹鍩勯崹浼村疮椤愶附鍎戞い鎺戝€甸崑鎾诲捶椤撶偘妲愰梺缁樼⊕閻熝囧焵椤掆偓缁犲秹宕愬┑瀣ュ鑸靛姈椤ュ牓鏌曡箛濞惧亾閺傘儱寰嶉柣搴㈩問閸犳帡宕戦幘缁樺€甸柣鐔哄濠€浼存煕閵婏箑鍝哄┑?",
                    new Dictionary<string, object?>
                    {
                        ["requestId"] = request.RequestId,
                        ["command"] = request.Command,
                        ["issuedBy"] = request.IssuedBy,
                        ["pageId"] = runtime.Definition.PageId,
                        ["layoutType"] = runtime.Definition.LayoutType.ToString(),
                        ["registeredNodes"] = runtime.NodeRegistry.Count,
                        ["currentPageId"] = currentPageId,
                    });

                FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                {
                    RequestId = request.RequestId,
                    Command = request.Command,
                    State = FacetLayoutLabBridge.StateCompleted,
                    Success = true,
                    Message = $"Layout lab request completed for page {runtime.Definition.PageId}.",
                    IssuedBy = request.IssuedBy,
                    IssuedAtUtc = request.IssuedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    RuntimeSessionId = CurrentSessionId,
                    RuntimePageId = currentPageId,
                });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "UI.Layout",
                    "Layout lab request execution failed.",
                    new Dictionary<string, object?>
                    {
                        ["requestId"] = request.RequestId,
                        ["command"] = request.Command,
                        ["issuedBy"] = request.IssuedBy,
                        ["currentPageId"] = currentPageId,
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });

                FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                {
                    RequestId = request.RequestId,
                    Command = request.Command,
                    State = FacetLayoutLabBridge.StateFailed,
                    Success = false,
                    Message = $"Layout lab request failed: {exception.Message}",
                    IssuedBy = request.IssuedBy,
                    IssuedAtUtc = request.IssuedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    RuntimeSessionId = CurrentSessionId,
                    RuntimePageId = currentPageId,
                });
            }
            finally
            {
                FacetLayoutLabBridge.DeleteRequest();
            }
        }

        private void CleanupConsumedLabRequests()
        {
            CleanupConsumedHotReloadLabRequest();
            CleanupConsumedLayoutLabRequest();
        }

        private void CleanupConsumedHotReloadLabRequest()
        {
            if (!FacetHotReloadLabBridge.TryLoadRequest(out FacetHotReloadLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            if (!FacetHotReloadLabBridge.TryLoadStatus(out FacetHotReloadLabStatus? status) ||
                status == null)
            {
                return;
            }

            if (!string.Equals(request.RequestId, status.RequestId, StringComparison.Ordinal) ||
                !FacetHotReloadLabBridge.IsTerminalState(status.State))
            {
                return;
            }

            FacetHotReloadLabBridge.DeleteRequest();
        }

        private void CleanupConsumedLayoutLabRequest()
        {
            if (!FacetLayoutLabBridge.TryLoadRequest(out FacetLayoutLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            if (!FacetLayoutLabBridge.TryLoadStatus(out FacetLayoutLabStatus? status) ||
                status == null)
            {
                return;
            }

            if (!string.Equals(request.RequestId, status.RequestId, StringComparison.Ordinal) ||
                !FacetLayoutLabBridge.IsTerminalState(status.State))
            {
                return;
            }

            FacetLayoutLabBridge.DeleteRequest();
        }

        private void VerifyApplicationBoundary()
        {
            try
            {
                AppResult<FacetRuntimeProbeSnapshot> currentProbeResult = Context.QueryBus
                    .QueryAsync(new FacetRuntimeProbeQuery())
                    .GetAwaiter()
                    .GetResult();

                if (!currentProbeResult.IsSuccess || currentProbeResult.Value == null)
                {
                    Logger.Warning(
                        "Application",
                        "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?2 闁荤喐绮庢晶妤呭箰閸涘﹥娅犻柣妯挎閻も偓濡炪倖鍔戦崐鏇烆嚕妤ｅ啯鐓涢悘鐐额嚙閸斻儲銇勯弴鐕佹畷鐎垫澘瀚蹇涱敃閵夋劖娲熼弻?",
                        new Dictionary<string, object?>
                        {
                            ["errorCode"] = currentProbeResult.ErrorCode,
                            ["errorMessage"] = currentProbeResult.ErrorMessage,
                        });
                    return;
                }

                FacetRuntimeProbeSnapshot snapshot = currentProbeResult.Value;
                AppResult recordResult = Context.CommandBus
                    .SendAsync(new RecordFacetRuntimeProbeCommand(snapshot))
                    .GetAwaiter()
                    .GetResult();

                if (!recordResult.IsSuccess)
                {
                    Logger.Warning(
                        "Application",
                        "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?2 闂備浇顫夋禍浠嬪垂娴犲绠柨娑樺婵ジ鏌℃径搴㈢《缂佸娼￠弻娑樷槈濞嗗繒浜伴梺纭呯堪閸婃牕顕ラ崟顒佺秶妞ゆ劑鍎涢弴銏＄厪?",
                        new Dictionary<string, object?>
                        {
                            ["errorCode"] = recordResult.ErrorCode,
                            ["errorMessage"] = recordResult.ErrorMessage,
                        });
                    return;
                }

                AppResult<FacetRuntimeProbeStatusSnapshot> statusResult = Context.QueryBus
                    .QueryAsync(new FacetRuntimeProbeStatusQuery())
                    .GetAwaiter()
                    .GetResult();

                if (!statusResult.IsSuccess || statusResult.Value == null)
                {
                    Logger.Warning(
                        "Application",
                        "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?2 闂備浇顫夋禍浠嬪垂娴犲绠柨鐔哄У閸嬫劙鏌ら崫銉毌闁稿鎸婚幏鍛村捶椤撶偛缁╅梺鑽ゅТ濞层垽宕硅ぐ鎺懳﹂柟瀵稿У鐎氬鏌曟径娑氬矝闁?",
                        new Dictionary<string, object?>
                        {
                            ["errorCode"] = statusResult.ErrorCode,
                            ["errorMessage"] = statusResult.ErrorMessage,
                        });
                    return;
                }

                FacetRuntimeProbeStatusSnapshot status = statusResult.Value;
                Logger.Info(
                    "Application",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?2 闂佸湱鍘ч悺銊ヮ潖婵犳艾鏋侀柕鍫濇缂嶅洭鏌涢敂璇插箺婵炲懏娲熷濠氬磼閵堝懎绠诲Δ鐘靛仜濡瑩銆冮妷銉ф殕闁告劦浜濋～宥夋⒑閻熸壆鎽犻柛鏃€娲橀幈銊モ槈閵忕姈?",
                    new Dictionary<string, object?>
                    {
                        ["sessionId"] = snapshot.SessionId,
                        ["hotReload"] = snapshot.HotReloadEnabled,
                        ["pageCache"] = snapshot.PageCacheEnabled,
                        ["pageCacheCapacity"] = snapshot.PageCacheCapacity,
                        ["structuredLogging"] = snapshot.StructuredLoggingEnabled,
                        ["structuredLogPath"] = snapshot.StructuredLogPath,
                        ["commandBusRegistered"] = snapshot.CommandBusRegistered,
                        ["queryBusRegistered"] = snapshot.QueryBusRegistered,
                        ["capturedAtUtc"] = snapshot.CapturedAtUtc.ToString("O"),
                        ["probeRecorded"] = status.HasSnapshot,
                        ["probeRecordCount"] = status.RecordedCount,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Application",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?2 闂佸湱鍘ч悺銊ヮ潖婵犳艾鏋侀柕鍫濇缂嶅洭鏌涢敂璇插箺婵炲懏娲熼弻銈嗙附婢跺鍑瑰銈冨劚閹虫﹢鐛惔鈾€妲堟俊顖滃劋閻︽棃鎮楀▓鍨灈闁稿﹥鎮傞幃銉╁醇閺囩倣?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyProjectionLayer()
        {
            try
            {
                bool probeSubscriberTriggered = false;
                bool metricsSubscriberTriggered = false;
                ProjectionChange? probeObservedChange = null;
                ProjectionChange? metricsObservedChange = null;

                using IDisposable probeSubscription = Context.ProjectionStore.Subscribe(
                    RuntimeProbeProjectionKey,
                    change =>
                    {
                        probeSubscriberTriggered = true;
                        probeObservedChange = change;
                    });

                using IDisposable metricsSubscription = Context.ProjectionStore.Subscribe(
                    RuntimeMetricsProjectionKey,
                    change =>
                    {
                        metricsSubscriberTriggered = true;
                        metricsObservedChange = change;
                    });

                int refreshedCount = Context.ProjectionRefreshCoordinator
                    .RefreshAllAsync()
                    .GetAwaiter()
                    .GetResult();

                bool probeProjectionExists = Context.ProjectionStore.TryGet(RuntimeProbeProjectionKey, out FacetRuntimeProbeProjection? storedProbeProjection);
                bool metricsProjectionExists = Context.ProjectionStore.TryGet(RuntimeMetricsProjectionKey, out FacetRuntimeMetricListProjection? storedMetricsProjection);

                Logger.Info(
                    "Projection",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?3 Projection 濠德板€楁慨浼村礉瀹ュ鍊堕柨鏃傜摂濞堢晫鈧厜鍋撻柛鎰典簼椤秹姊洪悷鎵憼闁告梹娲橀幈銊モ槈閵忕姈?",
                    new Dictionary<string, object?>
                    {
                        ["registeredUpdaters"] = Context.ProjectionRefreshCoordinator.Count,
                        ["refreshedCount"] = refreshedCount,
                        ["projectionKey"] = RuntimeProbeProjectionKey.ToString(),
                        ["subscriberTriggered"] = probeSubscriberTriggered,
                        ["observedChangeKind"] = probeObservedChange?.Kind.ToString(),
                        ["projectionExists"] = probeProjectionExists,
                        ["projectionCount"] = Context.ProjectionStore.Count,
                        ["recordedCount"] = storedProbeProjection?.RecordedCount,
                        ["hasSnapshot"] = storedProbeProjection?.HasSnapshot,
                        ["metricsProjectionKey"] = RuntimeMetricsProjectionKey.ToString(),
                        ["metricsSubscriberTriggered"] = metricsSubscriberTriggered,
                        ["metricsObservedChangeKind"] = metricsObservedChange?.Kind.ToString(),
                        ["metricsProjectionExists"] = metricsProjectionExists,
                        ["metricsItemCount"] = storedMetricsProjection?.Items.Count,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Projection",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?3 Projection 濠德板€楁慨浼村礉瀹ュ鍊堕柨鏃傜摂濞堢晫鈧厜鍋撻柛鎰典簼椤秹姊虹涵鍛厫缂佸鍨垮畷鐢割敇閻戝棗娈ㄩ梺绋挎湰濮樸劑宕哄Δ鍛厪?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyPageDefinitions()
        {
            try
            {
                UIPageRegistry pageRegistry = Services.GetRequired<UIPageRegistry>();

                Logger.Info(
                    "UI.Page",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?4 濠碉紕鍋戦崐妤呭极鐠囧樊鐒介柟娈垮枓閸嬫挾鎲撮崟顓犲彎缂備胶濮烽崰搴ㄥ煝閺冨牆惟闁靛鍎查弶鎼佹煟鎼达絾鍤€闁哄牜鍓熷畷鐟邦潩鐠轰綍?",
                    new Dictionary<string, object?>
                    {
                        ["registeredPages"] = pageRegistry.Count,
                        ["containsIdlePage"] = pageRegistry.Contains(UIPageIds.Idle),
                        ["containsDungeonPage"] = pageRegistry.Contains(UIPageIds.Dungeon),
                        ["containsGeneratedLayoutLab"] = pageRegistry.Contains(UIPageIds.GeneratedLayoutLab),
                        ["containsTemplateLayoutLab"] = pageRegistry.Contains(UIPageIds.TemplateLayoutLab),
                        ["routeServiceReady"] = Services.Contains<UIRouteService>(),
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "UI.Page",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?4 濠碉紕鍋戦崐妤呭极鐠囧樊鐒介柟娈垮枓閸嬫挾鎲撮崟顓犲彎缂備胶濮烽崰搴ㄥ煝閺冨牆惟闁靛鍎查弶鍛婁繆閵堝洤孝闁活厺绶氶幆浣瑰緞鐎ｎ亞绐為柡澶婄墱閸嬪顤傞梻?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyLuaRuntime()
        {
            try
            {
                ILuaScriptSource scriptSource = Services.GetRequired<ILuaScriptSource>();
                Logger.Info(
                    "Lua.Runtime",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?8 闂備焦妞挎禍鐐哄窗鎼淬劍鍋?Lua 闂佽娴烽幊鎾诲Φ濡皷鍋撻棃娑滃妞ゆ柨绻橀獮鎾诲箳閹捐埖顓荤紓鍌氬€风欢鈩冪濡ゅ懎鐒?",
                    new Dictionary<string, object?>
                    {
                        ["scriptSourceType"] = scriptSource.GetType().Name,
                        ["runtimeHostType"] = Services.GetRequired<ILuaRuntimeHost>().GetType().Name,
                        ["reloadCoordinatorReady"] = Services.Contains<LuaReloadCoordinator>(),
                        ["hotReloadEnabled"] = Config.EnableHotReload,
                        ["hotReloadPollIntervalSeconds"] = Config.HotReloadPollIntervalSeconds,
                        ["registeredScriptCount"] = scriptSource.GetRegisteredScripts().Count,
                        ["registeredScripts"] = string.Join(",", scriptSource.GetRegisteredScripts()),
                        ["routeBridgeReady"] = Services.Contains<IUIPageNavigator>(),
                        ["bindingBridgeReady"] = Services.Contains<UIBindingService>(),
                        ["redDotBridgeReady"] = Services.Contains<ILuaRedDotBridge>(),
                        ["hotReloadTestServiceReady"] = Services.Contains<LuaHotReloadTestService>(),
                        ["hotReloadTestShortcutEnabled"] = EnableLuaHotReloadTestShortcut,
                        ["hotReloadCurrentPageTestShortcut"] = EnableLuaHotReloadTestShortcut ? LuaHotReloadCurrentPageTestShortcutDescription : string.Empty,
                        ["hotReloadDungeonTestShortcut"] = EnableLuaHotReloadTestShortcut ? LuaHotReloadDungeonTestShortcutDescription : string.Empty,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Lua.Runtime",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?8 Lua 闂佽娴烽幊鎾诲Φ濡皷鍋撻棃娑欐拱妞ゃ劊鍎遍悾婵嬪礃椤忓拋娼斿┑鐘灪閸庤偐鍒掗崜褎鍠嗛柨鏇炲€歌繚?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyRedDotRuntime()
        {
            try
            {
                IRedDotService redDotService = Services.GetRequired<IRedDotService>();
                Logger.Info(
                    "RedDot.Runtime",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?10 缂傚倷妞掗崟姗€宕规繝姘；闁挎繂顦崘鈧梺纭呮彧缁茶法绮婚妷鈺傚仩婵炴垶蓱濠€鐗堜繆閻愭彃鈧悂顢欒箛娑樼煑濠㈣泛锕らˇ鏌ユ⒑缁嬭法绠為柛搴灦閸┾偓?",
                    new Dictionary<string, object?>
                    {
                        ["serviceType"] = redDotService.GetType().Name,
                        ["providerCount"] =
                            (Services.Contains<FacetRuntimeRedDotProvider>() ? 1 : 0) +
                            (Services.Contains<ManualRedDotProvider>() ? 1 : 0),
                        ["registeredPathCount"] = redDotService.RegisteredPathCount,
                        ["registeredPathsPreview"] = string.Join(",", redDotService.GetRegisteredPaths()),
                        ["luaBridgeAvailable"] = Services.GetRequired<ILuaRedDotBridge>().IsAvailable,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "RedDot.Runtime",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?10 缂傚倷妞掗崟姗€宕规繝姘；闁挎繂顦崘鈧梺纭呮彧缁茶法绮婚妷鈺傚仩婵炴垶蓱濠€鐗堜繆閻愭彃鈧綊銆冮妷銉ф殕闁告劦浜濋～宥嗙節閵忊€冲姸缂侇喖澧介幉鎾晝閸屾?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyAdvancedLayouts()
        {
            Control verificationMountRoot = new()
            {
                Name = "FacetLayoutVerificationMount",
                Visible = false,
            };
            AddChild(verificationMountRoot);

            try
            {
                UIPageLoader pageLoader = Services.GetRequired<UIPageLoader>();
                UIPageRegistry pageRegistry = Services.GetRequired<UIPageRegistry>();

                UILayoutResult generatedLayout = LoadVerificationLayout(
                    pageLoader,
                    pageRegistry.GetRequired(UIPageIds.GeneratedLayoutLab),
                    verificationMountRoot);
                VerifyRequiredNode(generatedLayout, "GeneratedTitleLabel");
                VerifyRequiredNode(generatedLayout, "GeneratedPrimaryButton");

                UILayoutResult templateLayout = LoadVerificationLayout(
                    pageLoader,
                    pageRegistry.GetRequired(UIPageIds.TemplateLayoutLab),
                    verificationMountRoot);
                VerifyRequiredNode(templateLayout, "TemplateShellTitleLabel");
                VerifyRequiredNode(templateLayout, "TemplateContentSlot");
                VerifyRequiredNode(templateLayout, "TemplateContentTitleLabel");
                VerifyRequiredNode(templateLayout, "TemplateActionPrimaryButton");

                Logger.Info(
                    "UI.Layout",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?11 婵犵妲呴崹顏堝礈濠靛棭鐔嗘俊顖氬悑閺嗘粓鏌涢幇鈺佸婵犫偓閺夊簱妲堥柟鎹愭珪閸炲鏌涢妶鍡欑煉鐎规洘绻堟俊鎼佸Ψ瑜滄导宀勬煟韫囨洖浜归柛瀣尭铻栭柣妯垮皺閻掔兘鏌ｉ敃鈧ˇ鐢哥嵁鐎ｎ喖绠涙い鎺嗗亾妞ゎ偆濞€閺?",
                    new Dictionary<string, object?>
                    {
                        ["generatedPageId"] = UIPageIds.GeneratedLayoutLab,
                        ["generatedRegisteredNodes"] = generatedLayout.NodeRegistry.Count,
                        ["templatePageId"] = UIPageIds.TemplateLayoutLab,
                        ["templateRegisteredNodes"] = templateLayout.NodeRegistry.Count,
                        ["generatedOwnsRootNode"] = generatedLayout.OwnsRootNode,
                        ["templateOwnsRootNode"] = templateLayout.OwnsRootNode,
                    });

                ReleaseVerificationLayout(generatedLayout);
                ReleaseVerificationLayout(templateLayout);
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "UI.Layout",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?11 婵犵妲呴崹顏堝礈濠靛棭鐔嗘俊顖氬悑閺嗘粓鏌涢幇鈺佸婵犫偓閺夊簱妲堥柟鎹愭珪閸炲鏌涢妶鍡欑煉鐎规洘绻堟俊鎼佸Ψ瑜滄导宀勬煟韫囨洖浜归柛瀣尭铻栭柣妯垮皺閻掔兘鏌ｉ敃鈧ˇ鏉款嚗閸曨剚缍囨い鎰╁剾閺囥垺鐓?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
            finally
            {
                if (GodotObject.IsInstanceValid(verificationMountRoot))
                {
                    verificationMountRoot.QueueFree();
                }
            }
        }

        private void VerifyRuntimeDiagnostics()
        {
            try
            {
                FacetRuntimeDiagnosticsSnapshot snapshot = CreateRuntimeDiagnosticsSnapshot();
                PublishRuntimeDiagnosticsSnapshot(force: true);

                Logger.Info(
                    "Tooling.Diagnostics",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?12 闂佸搫顦弲婊堝礉濮椻偓閵嗕線骞嬮敃鈧猾宥夋煙椤栨稑顥嬫俊鍙夊哺閺岋繝宕掗妶鍛闂佸湱鍎甸弲鐘诲箖闄囬ˇ鏌ユ煕閹搭垳绡€妤犵偞甯℃俊鐑藉Ψ閵夈儲杈堥梻?",
                    new Dictionary<string, object?>
                    {
                        ["snapshotPath"] = FacetRuntimeDiagnosticsBridge.GetSnapshotPath(),
                        ["registeredPageCount"] = snapshot.RegisteredPageCount,
                        ["activeRuntimeCount"] = snapshot.ActiveRuntimeCount,
                        ["projectionCount"] = snapshot.ProjectionCount,
                        ["luaRegisteredScriptCount"] = snapshot.LuaRegisteredScriptCount,
                        ["redDotRegisteredPathCount"] = snapshot.RedDotRegisteredPathCount,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Tooling.Diagnostics",
                    "Facet 闂傚倸鍊搁崯鎶藉春閺嶎収鏁?12 闂佸搫顦弲婊堝礉濮椻偓閵嗕線骞嬮敃鈧猾宥夋煙椤栨稑顥嬫俊鍙夊哺閺岋繝宕掗妶鍛闂佸湱鍎甸弲鐘诲箖闄囬ˇ褰掓煟濞戞瑧鎳呴柟椋庡У閹峰懘骞栭悙鐗堟線闂佽崵濮甸崝鎴﹀磿椤栫偛鐒?",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private static UILayoutResult LoadVerificationLayout(
            UIPageLoader pageLoader,
            UIPageDefinition definition,
            Node mountRoot)
        {
            return pageLoader.Load(definition, mountRoot);
        }

        private static void VerifyRequiredNode(UILayoutResult layoutResult, string nodeKey)
        {
            layoutResult.NodeResolver.GetRequired<Control>(nodeKey);
        }

        private static void ReleaseVerificationLayout(UILayoutResult layoutResult)
        {
            if (layoutResult.OwnsRootNode && GodotObject.IsInstanceValid(layoutResult.RootNode))
            {
                layoutResult.RootNode.QueueFree();
            }
        }

        private static FacetLogger CreateLogger(FacetConfig config)
        {
            return new FacetLogger(
                config.MinimumLogLevel,
                config.EnableStructuredLogging,
                config.StructuredLogPath,
                config.StructuredLogBufferCapacity,
                config.StructuredLogHistoryLimit,
                config.EnableConsoleMirrorLogging,
                config.ConsoleMirrorLogPath,
                config.ConsoleMirrorLogHistoryLimit);
        }
    }
}
