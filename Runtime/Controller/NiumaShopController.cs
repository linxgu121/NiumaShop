using System;
using System.Collections.Generic;
using NiumaCore.Module;
using NiumaInventory.Config;
using NiumaInventory.Controller;
using NiumaInventory.Service;
using NiumaShop.Config;
using NiumaShop.Data;
using NiumaShop.Enum;
using NiumaShop.Request;
using NiumaShop.Result;
using NiumaShop.Service;
using NiumaShop.ViewData;
using UnityEngine;

namespace NiumaShop.Controller
{
    /// <summary>
    /// NiumaShop 商店模块根控制器。
    /// 负责把纯 C# 的 ShopService 接入 Unity 生命周期、Inspector 配置、GameContext 和基础调试入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaShopController : MonoBehaviour, IGameModule
    {
        [Header("商店配置")]
        [Tooltip("商店配置列表。请拖入当前版本或当前场景可用的 ShopAsset，ShopId 必须稳定。")]
        [SerializeField] private ShopAsset[] shopAssets = Array.Empty<ShopAsset>();

        [Tooltip("可选：物品定义列表，用于校验商品 ItemId、货币 ItemId，并补齐商店 UI 显示信息。")]
        [SerializeField] private ItemDefinition[] itemDefinitions = Array.Empty<ItemDefinition>();

        [Tooltip("是否启用严格配置模式。开启后只要商店配置存在错误，ShopRegistry 会判定为不可用。")]
        [SerializeField] private bool strictRegistryMode;

        [Header("背包依赖")]
        [Tooltip("背包模块控制器。没有统一 GameContext 时可手动绑定；商店模块会从这里读取 IInventoryService。")]
        [SerializeField] private NiumaInventoryController inventoryController;

        [Tooltip("初始化时是否尝试从 GameContext 解析 IInventoryService。使用统一模块启动器时建议开启。")]
        [SerializeField] private bool resolveInventoryFromContext = true;

        [Header("外部条件与价格")]
        [Tooltip("外部条件解析组件。该组件必须实现 IShopConditionResolver；可为空，第一版只内置通用条件。")]
        [SerializeField] private MonoBehaviour conditionResolverBehaviour;

        [Tooltip("初始化时是否尝试从 GameContext 解析 IShopConditionResolver。使用统一模块启动器时建议开启。")]
        [SerializeField] private bool resolveConditionResolverFromContext = true;

        [Tooltip("外部价格解析组件。该组件必须实现 IShopPriceResolver；可为空，服务会回退到商品基础价格。")]
        [SerializeField] private MonoBehaviour priceResolverBehaviour;

        [Tooltip("初始化时是否尝试从 GameContext 解析 IShopPriceResolver。使用统一模块启动器时建议开启。")]
        [SerializeField] private bool resolvePriceResolverFromContext = true;

        [Header("模块启动")]
        [Tooltip("Awake 时是否自动初始化商店服务。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool initializeOnAwake = true;

        [Tooltip("OnEnable 时是否自动启动商店模块。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool startOnEnable = true;

        [Tooltip("初始化时是否把 IShopService、IShopQuery、IShopCommand 注册到 GameContext。使用统一 GameContext 的项目建议开启。")]
        [SerializeField] private bool registerServiceToContext = true;

        [Header("调试：购买请求")]
        [Tooltip("调试用商店 ID。右键组件菜单会用它打开、购买、刷新或打印商店状态。")]
        [SerializeField] private string debugShopId;

        [Tooltip("调试用商品 ID。右键组件菜单会用它检查、购买、解锁或锁定商品。")]
        [SerializeField] private string debugProductId;

        [Tooltip("调试购买份数。小于等于 0 时服务会拒绝购买。")]
        [SerializeField] private int debugBuyCount = 1;

        [Tooltip("调试购买后的目标背包容器 ID。为空时由背包模块自动选择容器。")]
        [SerializeField] private string debugTargetContainerId;

        [Header("调试：出售预留")]
        [Tooltip("调试出售用背包实例 ID。第一版出售功能未开放，该字段用于后续阶段预留。")]
        [SerializeField] private string debugInventoryInstanceId;

        [Tooltip("调试出售数量。第一版出售功能未开放，该字段用于后续阶段预留。")]
        [SerializeField] private int debugSellCount = 1;

        private IShopService _shopService;
        private IShopConfigurationService _shopConfigurationService;
        private GameContext _context;
        private IInventoryService _inventoryService;
        private IShopConditionResolver _conditionResolver;
        private IShopPriceResolver _priceResolver;
        private bool _inventoryServiceLocked;
        private bool _conditionResolverLocked;
        private bool _priceResolverLocked;
        private bool _warnedMissingShops;
        private bool _warnedMissingItemDefinitions;
        private bool _warnedMissingInventoryService;
        private bool _warnedInvalidConditionResolver;
        private bool _warnedInvalidPriceResolver;
        private bool _warnedInitializeFailure;
        private bool _warnedServiceNotReady;
        private bool _autoInitializeFailed;
        private bool _isDestroyed;

        /// <summary>
        /// 模块名称。
        /// </summary>
        public string ModuleName => "NiumaShop";

        /// <summary>
        /// 商店服务门面接口。
        /// 外部模块应优先依赖 IShopService、IShopQuery 或 IShopCommand，而不是直接依赖 ShopService 实现。
        /// </summary>
        public IShopService ShopService => _shopService;

        /// <summary>
        /// 商店查询接口。
        /// UI、任务、剧情等只读模块优先依赖该接口。
        /// </summary>
        public IShopQuery ShopQuery => _shopService;

        /// <summary>
        /// 商店命令接口。
        /// 交互、剧情、任务奖励等需要修改商店状态的模块依赖该接口。
        /// </summary>
        public IShopCommand ShopCommand => _shopService;

        /// <summary>
        /// 当前模块是否已经初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 当前模块是否正在运行。
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 商店模块全局修订号。
        /// UI 打开单个商店时应优先使用 GetShopRevision(shopId)。
        /// </summary>
        public long ShopRevision => _shopService != null ? _shopService.Revision : 0L;

        /// <summary>
        /// 当前商店配置。
        /// UI 或调试面板可只读使用，正式修改请通过 SetShopAssets。
        /// </summary>
        public ShopAsset[] ShopAssets => shopAssets ?? Array.Empty<ShopAsset>();

        /// <summary>
        /// 当前用于商店校验和 UI 显示补全的物品定义配置。
        /// 外部只读使用，不要在运行时直接修改数组里的 ScriptableObject 内容。
        /// </summary>
        public ItemDefinition[] ItemDefinitions => itemDefinitions ?? Array.Empty<ItemDefinition>();

        /// <summary>
        /// 最近一次商店操作结果。
        /// </summary>
        public ShopOperationResult LastOperationResult { get; private set; }

        /// <summary>
        /// 最近一次构建的商店面板数据。
        /// </summary>
        public ShopPanelViewData LastPanelViewData { get; private set; }

        private void Awake()
        {
            if (initializeOnAwake && !IsInitialized)
            {
                Initialize(null);
            }
        }

        private void OnEnable()
        {
            if (startOnEnable && IsInitialized && !IsRunning)
            {
                StartModule();
            }
        }

        private void OnDisable()
        {
            if (IsRunning)
            {
                StopModule();
            }
        }

        private void OnDestroy()
        {
            UnregisterServicesFromContext();
            IsRunning = false;
            IsInitialized = false;
            _isDestroyed = true;
        }

        /// <summary>
        /// 初始化商店模块。
        /// 如果已有服务，会导出商店快照并在新服务中恢复，避免重复初始化丢失商店解锁、库存和限购进度。
        /// </summary>
        public void Initialize(GameContext context)
        {
            var wasRunning = IsRunning;
            var previousService = _shopService;
            var previousConfigurationService = _shopConfigurationService;
            var previousContext = _context;
            var previousInventoryService = _inventoryService;
            var previousConditionResolver = _conditionResolver;
            var previousPriceResolver = _priceResolver;
            var previousInitialized = IsInitialized;
            var targetContext = context ?? _context;
            var previousRegisteredService = targetContext != null ? targetContext.GetService<IShopService>() : null;
            var previousRegisteredQuery = targetContext != null ? targetContext.GetService<IShopQuery>() : null;
            var previousRegisteredCommand = targetContext != null ? targetContext.GetService<IShopCommand>() : null;
            var initializedSuccessfully = false;
            ShopService newService = null;
            IsRunning = false;

            try
            {
                _context = targetContext;
                WarnIfConfigMissing();

                if (!_inventoryServiceLocked)
                {
                    _inventoryService = ResolveInventoryService(_context);
                }

                if (!_conditionResolverLocked)
                {
                    _conditionResolver = ResolveConditionResolver(_context);
                }

                if (!_priceResolverLocked)
                {
                    _priceResolver = ResolvePriceResolver(_context);
                }

                WarnIfInventoryMissing();

                var snapshots = previousService != null ? previousService.ExportSnapshots() : null;
                newService = new ShopService(
                    shopAssets,
                    _inventoryService,
                    itemDefinitions,
                    _conditionResolver,
                    _priceResolver,
                    strictRegistryMode);

                if (snapshots != null)
                {
                    newService.ImportSnapshots(snapshots);
                }

                _shopService = newService;
                _shopConfigurationService = newService;
                RegisterServicesToContext();
                IsInitialized = true;
                _warnedInitializeFailure = false;
                _autoInitializeFailed = false;
                _warnedServiceNotReady = false;
                initializedSuccessfully = true;
            }
            catch (Exception exception)
            {
                if (!_warnedInitializeFailure)
                {
                    Debug.LogError($"[NiumaShop] 初始化商店模块失败：{exception.Message}", this);
                    _warnedInitializeFailure = true;
                }

                RestoreRegisteredShopServices(targetContext, previousRegisteredService, previousRegisteredQuery, previousRegisteredCommand, newService);
                DisposeServiceIfNeeded(newService);
                _shopService = previousService;
                _shopConfigurationService = previousConfigurationService;
                _context = previousContext;
                _inventoryService = previousInventoryService;
                _conditionResolver = previousConditionResolver;
                _priceResolver = previousPriceResolver;
                IsInitialized = previousInitialized;
            }
            finally
            {
                IsRunning = initializedSuccessfully
                    ? wasRunning && _shopService != null
                    : wasRunning && previousInitialized && previousService != null;
            }
        }

        /// <summary>
        /// 启动商店模块。
        /// </summary>
        public void StartModule()
        {
            if (!IsInitialized)
            {
                Initialize(_context);
            }

            IsRunning = true;
        }

        /// <summary>
        /// 停止商店模块。
        /// 这里只关闭运行标记，不导出存档；存档由 NiumaSave 或上层流程统一触发。
        /// </summary>
        public void StopModule()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 商店模块帧更新。
        /// 当前 ShopService 是请求驱动，MVP 阶段不需要每帧逻辑。
        /// </summary>
        public void Tick(float deltaTime)
        {
        }

        /// <summary>
        /// 运行时替换商店配置。
        /// 不清空玩家商店进度，会尽量按 ShopId / ProductId 迁移。
        /// </summary>
        public ShopAssetValidationReport SetShopAssets(ShopAsset[] assets)
        {
            shopAssets = assets ?? Array.Empty<ShopAsset>();
            _warnedMissingShops = false;
            _autoInitializeFailed = false;
            return _shopConfigurationService != null
                ? _shopConfigurationService.SetShops(shopAssets, itemDefinitions, strictRegistryMode)
                : null;
        }

        /// <summary>
        /// 运行时替换商店校验与 UI 显示用物品定义。
        /// InventoryController 热更新 ItemDefinition 后，应同步调用该方法。
        /// </summary>
        public ShopAssetValidationReport SetItemDefinitions(ItemDefinition[] definitions)
        {
            itemDefinitions = definitions ?? Array.Empty<ItemDefinition>();
            _warnedMissingItemDefinitions = false;
            _autoInitializeFailed = false;
            return _shopConfigurationService != null
                ? _shopConfigurationService.SetItemDefinitions(itemDefinitions)
                : null;
        }

        /// <summary>
        /// 运行时设置背包服务。
        /// 通常由统一模块启动器调用；调用后会锁定自动解析，避免后续 Initialize 静默覆盖。
        /// </summary>
        public void SetInventoryService(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
            _inventoryServiceLocked = true;
            _autoInitializeFailed = false;
            if (_shopConfigurationService != null)
            {
                TryApplyExternalDependency("设置背包服务", () => _shopConfigurationService.SetInventoryService(_inventoryService));
            }
        }

        /// <summary>
        /// 解除背包服务手动注入锁定，并重新从 Inspector 或 GameContext 解析。
        /// </summary>
        public void UnlockInventoryService()
        {
            _inventoryServiceLocked = false;
            _inventoryService = ResolveInventoryService(_context);
            if (_shopConfigurationService != null)
            {
                TryApplyExternalDependency("重新解析背包服务", () => _shopConfigurationService.SetInventoryService(_inventoryService));
            }
        }

        /// <summary>
        /// 运行时设置外部条件解析器。
        /// 通常由任务、剧情、声望或统一模块启动器注入。
        /// </summary>
        public void SetConditionResolver(IShopConditionResolver conditionResolver)
        {
            _conditionResolver = conditionResolver;
            _conditionResolverLocked = true;
            _autoInitializeFailed = false;
            if (_shopConfigurationService != null)
            {
                TryApplyExternalDependency("设置条件解析器", () => _shopConfigurationService.SetConditionResolver(_conditionResolver));
            }
        }

        /// <summary>
        /// 解除外部条件解析器手动注入锁定，并重新从 Inspector 或 GameContext 解析。
        /// </summary>
        public void UnlockConditionResolver()
        {
            _conditionResolverLocked = false;
            _conditionResolver = ResolveConditionResolver(_context);
            if (_shopConfigurationService != null)
            {
                TryApplyExternalDependency("重新解析条件解析器", () => _shopConfigurationService.SetConditionResolver(_conditionResolver));
            }
        }

        /// <summary>
        /// 运行时设置外部价格解析器。
        /// 通常由活动、折扣、声望或统一模块启动器注入。
        /// </summary>
        public void SetPriceResolver(IShopPriceResolver priceResolver)
        {
            _priceResolver = priceResolver;
            _priceResolverLocked = true;
            _autoInitializeFailed = false;
            if (_shopConfigurationService != null)
            {
                TryApplyExternalDependency("设置价格解析器", () => _shopConfigurationService.SetPriceResolver(_priceResolver));
            }
        }

        /// <summary>
        /// 解除外部价格解析器手动注入锁定，并重新从 Inspector 或 GameContext 解析。
        /// </summary>
        public void UnlockPriceResolver()
        {
            _priceResolverLocked = false;
            _priceResolver = ResolvePriceResolver(_context);
            if (_shopConfigurationService != null)
            {
                TryApplyExternalDependency("重新解析价格解析器", () => _shopConfigurationService.SetPriceResolver(_priceResolver));
            }
        }

        /// <summary>
        /// 获取指定商店的单商店修订号。
        /// </summary>
        public long GetShopRevision(string shopId)
        {
            return EnsureServiceReady(false) ? _shopService.GetRevision(shopId) : 0L;
        }

        /// <summary>
        /// 构建指定商店 UI 表现数据。
        /// </summary>
        public ShopPanelViewData BuildShopViewData(string shopId)
        {
            if (!EnsureServiceReady(false))
            {
                StoreResult(ShopOperationResult.Fail(
                    shopId,
                    null,
                    ShopTransactionType.None,
                    ShopFailureReason.ServiceNotReady,
                    "商店服务未初始化。"));

                if (LastPanelViewData != null)
                {
                    LastPanelViewData.LastOperationResult = LastOperationResult;
                    return LastPanelViewData;
                }

                return new ShopPanelViewData
                {
                    Revision = ShopRevision,
                    ShopId = shopId,
                    State = ShopState.None,
                    Products = Array.Empty<ShopProductViewData>(),
                    LastOperationResult = LastOperationResult
                };
            }

            LastPanelViewData = _shopService.BuildShopViewData(shopId);
            return LastPanelViewData;
        }

        /// <summary>
        /// 尝试获取商店运行时快照。
        /// </summary>
        public bool TryGetShopState(string shopId, out ShopProgressSnapshot snapshot)
        {
            snapshot = null;
            return EnsureServiceReady(false) && _shopService.TryGetShopState(shopId, out snapshot);
        }

        /// <summary>
        /// 检查购买请求是否可执行，只做校验，不扣货币、不发商品。
        /// </summary>
        public bool CanBuy(BuyProductRequest request, out ShopOperationResult result)
        {
            result = null;
            if (!EnsureServiceReady(false))
            {
                result = StoreResult(ShopOperationResult.Fail(
                    request.ShopId,
                    request.ProductId,
                    ShopTransactionType.Buy,
                    ShopFailureReason.ServiceNotReady,
                    "商店服务未初始化。"));
                return false;
            }

            var canBuy = _shopService.CanBuy(request, out result);
            StoreResult(result);
            return canBuy;
        }

        /// <summary>
        /// 检查出售请求是否可执行。
        /// 第一版出售功能未开放，通常返回失败。
        /// </summary>
        public bool CanSell(SellItemRequest request, out ShopOperationResult result)
        {
            result = null;
            if (!EnsureServiceReady(false))
            {
                result = StoreResult(ShopOperationResult.Fail(
                    request.ShopId,
                    null,
                    ShopTransactionType.Sell,
                    ShopFailureReason.ServiceNotReady,
                    "商店服务未初始化。"));
                return false;
            }

            var canSell = _shopService.CanSell(request, out result);
            StoreResult(result);
            return canSell;
        }

        /// <summary>
        /// 执行购买商品。
        /// </summary>
        public ShopOperationResult BuyProduct(BuyProductRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(ShopOperationResult.Fail(
                    request.ShopId,
                    request.ProductId,
                    ShopTransactionType.Buy,
                    ShopFailureReason.ServiceNotReady,
                    "商店服务未初始化。"));
            }

            return StoreResult(_shopService.BuyProduct(request));
        }

        /// <summary>
        /// 执行出售物品。
        /// 第一版出售功能未开放，通常返回失败。
        /// </summary>
        public ShopOperationResult SellItem(SellItemRequest request)
        {
            if (!EnsureServiceReady())
            {
                return StoreResult(ShopOperationResult.Fail(
                    request.ShopId,
                    null,
                    ShopTransactionType.Sell,
                    ShopFailureReason.ServiceNotReady,
                    "商店服务未初始化。"));
            }

            return StoreResult(_shopService.SellItem(request));
        }

        /// <summary>
        /// 解锁或锁定商店。
        /// </summary>
        public bool SetShopUnlocked(UnlockShopRequest request)
        {
            return EnsureServiceReady() && _shopService.SetShopUnlocked(request);
        }

        /// <summary>
        /// 解锁或锁定商品。
        /// </summary>
        public bool SetProductUnlocked(UnlockShopProductRequest request)
        {
            return EnsureServiceReady() && _shopService.SetProductUnlocked(request);
        }

        /// <summary>
        /// 设置商店当前激活折扣。
        /// DiscountIds 为 null、空数组或全空白数组时表示关闭该商店所有折扣。
        /// </summary>
        public bool SetActiveDiscounts(SetShopDiscountsRequest request)
        {
            return EnsureServiceReady() && _shopService.SetActiveDiscounts(request);
        }

        /// <summary>
        /// 恢复商店配置默认折扣。
        /// </summary>
        public bool ResetDiscountsToDefault(ResetShopDiscountsRequest request)
        {
            return EnsureServiceReady() && _shopService.ResetDiscountsToDefault(request);
        }

        /// <summary>
        /// 刷新商店库存和商品运行时状态。
        /// </summary>
        public bool RefreshShop(RefreshShopRequest request)
        {
            return EnsureServiceReady() && _shopService.RefreshShop(request);
        }

        /// <summary>
        /// 导出商店进度快照。
        /// </summary>
        public ShopProgressSnapshot[] ExportSnapshots()
        {
            return EnsureServiceReady() ? _shopService.ExportSnapshots() : Array.Empty<ShopProgressSnapshot>();
        }

        /// <summary>
        /// 复制商店进度快照到调用方缓存列表。
        /// </summary>
        public void CopyShopSnapshots(List<ShopProgressSnapshot> output)
        {
            if (!EnsureServiceReady())
            {
                output?.Clear();
                return;
            }

            _shopService.CopyShopSnapshots(output);
        }

        /// <summary>
        /// 导入商店进度快照。
        /// </summary>
        public void ImportSnapshots(IEnumerable<ShopProgressSnapshot> snapshots)
        {
            if (!EnsureServiceReady())
            {
                return;
            }

            if (snapshots == null)
            {
                Debug.LogWarning("[NiumaShop] ImportSnapshots 收到 null，已拒绝导入，避免误清空商店进度。", this);
                return;
            }

            _shopService.ImportSnapshots(snapshots);
        }

        /// <summary>
        /// 从完整商店存档数据导入。
        /// 该入口用于 NiumaSave，允许 Shops 为空表示当前存档没有商店运行时事实。
        /// </summary>
        public bool ImportSaveData(ShopSaveData saveData)
        {
            if (!EnsureServiceReady())
            {
                return false;
            }

            if (saveData == null)
            {
                Debug.LogWarning("[NiumaShop] ImportSaveData 收到 null，已拒绝导入。", this);
                return false;
            }

            return _shopService.ImportSaveData(saveData);
        }

        /// <summary>
        /// 获取最近一次导入或配置同步产生的迁移警告。
        /// </summary>
        public string[] GetMigrationWarnings()
        {
            return EnsureServiceReady() ? _shopService.LastMigrationWarnings : Array.Empty<string>();
        }

        [ContextMenu("NiumaShop/重新初始化服务")]
        private void DebugReinitialize()
        {
            Initialize(_context);
            Debug.Log("[NiumaShop] 商店服务已重新初始化。", this);
        }

        [ContextMenu("NiumaShop/解除背包服务锁定并重新解析")]
        private void DebugUnlockInventoryService()
        {
            UnlockInventoryService();
            Debug.Log($"[NiumaShop] 已重新解析背包服务：{_inventoryService != null}", this);
        }

        [ContextMenu("NiumaShop/解除条件解析器锁定并重新解析")]
        private void DebugUnlockConditionResolver()
        {
            UnlockConditionResolver();
            Debug.Log($"[NiumaShop] 已重新解析条件解析器：{_conditionResolver != null}", this);
        }

        [ContextMenu("NiumaShop/解除价格解析器锁定并重新解析")]
        private void DebugUnlockPriceResolver()
        {
            UnlockPriceResolver();
            Debug.Log($"[NiumaShop] 已重新解析价格解析器：{_priceResolver != null}", this);
        }

        [ContextMenu("NiumaShop/构建调试商店面板")]
        private void DebugBuildShopViewData()
        {
            var panel = BuildShopViewData(debugShopId);
            Debug.Log($"[NiumaShop] 商店面板 ShopId={panel.ShopId}, State={panel.State}, Products={panel.Products?.Length ?? 0}, Revision={panel.Revision}", this);
        }

        [ContextMenu("NiumaShop/检查调试购买")]
        private void DebugCanBuy()
        {
            var canBuy = CanBuy(CreateDebugBuyRequest(), out var result);
            LogResult($"检查购买 CanBuy={canBuy}", result);
        }

        [ContextMenu("NiumaShop/执行调试购买")]
        private void DebugBuyProduct()
        {
            LogResult("执行购买", BuyProduct(CreateDebugBuyRequest()));
        }

        [ContextMenu("NiumaShop/检查调试出售")]
        private void DebugCanSell()
        {
            var canSell = CanSell(CreateDebugSellRequest(), out var result);
            LogResult($"检查出售 CanSell={canSell}", result);
        }

        [ContextMenu("NiumaShop/解锁调试商店")]
        private void DebugUnlockShop()
        {
            var result = SetShopUnlocked(new UnlockShopRequest(debugShopId, true, nameof(NiumaShopController)));
            Debug.Log($"[NiumaShop] 解锁商店 ShopId={debugShopId}：{result}, Revision={GetShopRevision(debugShopId)}", this);
        }

        [ContextMenu("NiumaShop/锁定调试商店")]
        private void DebugLockShop()
        {
            var result = SetShopUnlocked(new UnlockShopRequest(debugShopId, false, nameof(NiumaShopController)));
            Debug.Log($"[NiumaShop] 锁定商店 ShopId={debugShopId}：{result}, Revision={GetShopRevision(debugShopId)}", this);
        }

        [ContextMenu("NiumaShop/解锁调试商品")]
        private void DebugUnlockProduct()
        {
            var result = SetProductUnlocked(new UnlockShopProductRequest(debugShopId, debugProductId, true, nameof(NiumaShopController)));
            Debug.Log($"[NiumaShop] 解锁商品 ShopId={debugShopId}, ProductId={debugProductId}：{result}, Revision={GetShopRevision(debugShopId)}", this);
        }

        [ContextMenu("NiumaShop/锁定调试商品")]
        private void DebugLockProduct()
        {
            var result = SetProductUnlocked(new UnlockShopProductRequest(debugShopId, debugProductId, false, nameof(NiumaShopController)));
            Debug.Log($"[NiumaShop] 锁定商品 ShopId={debugShopId}, ProductId={debugProductId}：{result}, Revision={GetShopRevision(debugShopId)}", this);
        }

        [ContextMenu("NiumaShop/刷新调试商店")]
        private void DebugRefreshShop()
        {
            var result = RefreshShop(new RefreshShopRequest(debugShopId, nameof(NiumaShopController)));
            Debug.Log($"[NiumaShop] 刷新商店 ShopId={debugShopId}：{result}, Revision={GetShopRevision(debugShopId)}", this);
        }

        [ContextMenu("NiumaShop/打印调试商店快照")]
        private void DebugPrintShopSnapshot()
        {
            if (!TryGetShopState(debugShopId, out var snapshot) || snapshot == null)
            {
                Debug.LogWarning($"[NiumaShop] 未找到商店状态：{debugShopId}", this);
                return;
            }

            Debug.Log($"[NiumaShop] 商店快照 ShopId={snapshot.ShopId}, State={snapshot.State}, Revision={snapshot.Revision}, Products={snapshot.Products?.Length ?? 0}, Refresh={snapshot.LastRefreshUnixSeconds}", this);
        }

        [ContextMenu("NiumaShop/打印全部商店快照")]
        private void DebugPrintAllShopSnapshots()
        {
            var snapshots = ExportSnapshots();
            Debug.Log($"[NiumaShop] 商店快照数量={snapshots.Length}, GlobalRevision={ShopRevision}", this);
        }

        [ContextMenu("NiumaShop/打印迁移警告")]
        private void DebugPrintMigrationWarnings()
        {
            var warnings = GetMigrationWarnings();
            if (warnings.Length == 0)
            {
                Debug.Log("[NiumaShop] 当前没有迁移警告。", this);
                return;
            }

            Debug.Log($"[NiumaShop] 迁移警告：{string.Join("；", warnings)}", this);
        }

        [ContextMenu("NiumaShop/打印配置校验报告")]
        private void DebugPrintValidationReport()
        {
            if (!EnsureServiceReady(false))
            {
                Debug.LogWarning("[NiumaShop] 商店服务未就绪，无法打印配置校验报告。", this);
                return;
            }

            var report = _shopConfigurationService?.LastValidationReport;
            Debug.Log($"[NiumaShop] 配置校验 IsValid={report?.IsValid ?? false}, Issues={report?.IssueCount ?? 0}, Errors={report?.HasErrors ?? false}, Warnings={report?.HasWarnings ?? false}, DuplicateShopIds={report?.HasDuplicateShopIds ?? false}", this);
        }

        private BuyProductRequest CreateDebugBuyRequest()
        {
            return new BuyProductRequest(
                debugShopId,
                debugProductId,
                debugBuyCount,
                debugTargetContainerId,
                nameof(NiumaShopController));
        }

        private SellItemRequest CreateDebugSellRequest()
        {
            return new SellItemRequest(
                debugShopId,
                debugInventoryInstanceId,
                debugSellCount,
                nameof(NiumaShopController));
        }

        private bool EnsureServiceReady(bool allowAutoInitialize = true)
        {
            if (_isDestroyed)
            {
                return false;
            }

            if (IsInitialized && _shopService != null)
            {
                return true;
            }

            if (!allowAutoInitialize || _autoInitializeFailed)
            {
                WarnServiceNotReadyOnce();
                return false;
            }

            Initialize(_context);
            if (_shopService != null)
            {
                return true;
            }

            _autoInitializeFailed = true;
            WarnServiceNotReadyOnce();
            return false;
        }

        private IInventoryService ResolveInventoryService(GameContext context)
        {
            if (inventoryController != null && inventoryController.InventoryService != null)
            {
                return inventoryController.InventoryService;
            }

            if (resolveInventoryFromContext && context != null && context.TryGetService<IInventoryService>(out var contextInventoryService))
            {
                return contextInventoryService;
            }

            return null;
        }

        private IShopConditionResolver ResolveConditionResolver(GameContext context)
        {
            if (conditionResolverBehaviour != null)
            {
                if (conditionResolverBehaviour is IShopConditionResolver resolver)
                {
                    return resolver;
                }

                if (!_warnedInvalidConditionResolver)
                {
                    Debug.LogWarning("[NiumaShop] conditionResolverBehaviour 未实现 IShopConditionResolver。", this);
                    _warnedInvalidConditionResolver = true;
                }
            }

            return resolveConditionResolverFromContext
                   && context != null
                   && context.TryGetService<IShopConditionResolver>(out var contextResolver)
                ? contextResolver
                : null;
        }

        private IShopPriceResolver ResolvePriceResolver(GameContext context)
        {
            if (priceResolverBehaviour != null)
            {
                if (priceResolverBehaviour is IShopPriceResolver resolver)
                {
                    return resolver;
                }

                if (!_warnedInvalidPriceResolver)
                {
                    Debug.LogWarning("[NiumaShop] priceResolverBehaviour 未实现 IShopPriceResolver。", this);
                    _warnedInvalidPriceResolver = true;
                }
            }

            return resolvePriceResolverFromContext
                   && context != null
                   && context.TryGetService<IShopPriceResolver>(out var contextResolver)
                ? contextResolver
                : null;
        }

        private void RegisterServicesToContext()
        {
            if (_context == null || !registerServiceToContext)
            {
                return;
            }

            if (_shopService == null)
            {
                Debug.LogWarning("[NiumaShop] 商店服务为空，已跳过 GameContext 注册，避免清除其它启动器注册的服务。", this);
                return;
            }

            RegisterServiceSafely(_context, _shopService as IShopService, "IShopService");
            RegisterServiceSafely(_context, _shopService as IShopQuery, "IShopQuery");
            RegisterServiceSafely(_context, _shopService as IShopCommand, "IShopCommand");
        }

        private void UnregisterServicesFromContext()
        {
            if (_context == null || !registerServiceToContext || _shopService == null)
            {
                return;
            }

            ClearRegisteredServiceIfCurrent(_context, _shopService as IShopService, "IShopService");
            ClearRegisteredServiceIfCurrent(_context, _shopService as IShopQuery, "IShopQuery");
            ClearRegisteredServiceIfCurrent(_context, _shopService as IShopCommand, "IShopCommand");
        }

        private void RestoreRegisteredShopServices(
            GameContext context,
            IShopService service,
            IShopQuery query,
            IShopCommand command,
            ShopService failedService)
        {
            if (context == null || !registerServiceToContext)
            {
                return;
            }

            // 初始化中途失败时恢复进入 Initialize 前的注册状态。
            // 如果原本没有注册服务，只在 GameContext 仍指向本次失败的新服务时才清空，避免覆盖其它启动器后续注册的有效服务。
            RestoreRegisteredService(context, service, failedService as IShopService);
            RestoreRegisteredService(context, query, failedService as IShopQuery);
            RestoreRegisteredService(context, command, failedService as IShopCommand);
        }

        private static void RestoreRegisteredService<T>(GameContext context, T previousService, T failedService)
            where T : class
        {
            if (context == null)
            {
                return;
            }

            if (previousService != null)
            {
                RegisterServiceSafely(context, previousService, typeof(T).Name);
                return;
            }

            var currentService = context.GetService<T>();
            if (currentService != null && ReferenceEquals(currentService, failedService))
            {
                ClearRegisteredServiceSafely<T>(context, typeof(T).Name);
            }
        }

        private static void RegisterServiceSafely<T>(GameContext context, T service, string serviceName)
            where T : class
        {
            if (context == null)
            {
                return;
            }

            try
            {
                context.RegisterService(service);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NiumaShop] 注册 {serviceName} 到 GameContext 失败：{exception.Message}");
            }
        }

        private static void ClearRegisteredServiceIfCurrent<T>(GameContext context, T service, string serviceName)
            where T : class
        {
            if (context == null || service == null)
            {
                return;
            }

            var currentService = context.GetService<T>();
            if (ReferenceEquals(currentService, service))
            {
                ClearRegisteredServiceSafely<T>(context, serviceName);
            }
        }

        private static void ClearRegisteredServiceSafely<T>(GameContext context, string serviceName)
            where T : class
        {
            if (context == null)
            {
                return;
            }

            try
            {
                context.UnregisterService<T>();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NiumaShop] 从 GameContext 清理 {serviceName} 失败：{exception.Message}");
            }
        }

        private static void DisposeServiceIfNeeded(object service)
        {
            var disposable = service as IDisposable;
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NiumaShop] 释放失败的商店服务实例时出现异常：{exception.Message}");
            }
        }

        private bool TryApplyExternalDependency(string actionName, Action action)
        {
            if (action == null)
            {
                return false;
            }

            try
            {
                action();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NiumaShop] {actionName}失败：{exception.Message}", this);
                return false;
            }
        }

        private void WarnServiceNotReadyOnce()
        {
            if (_warnedServiceNotReady)
            {
                return;
            }

            Debug.LogWarning("[NiumaShop] 商店服务未就绪。查询接口不会隐式重新初始化，请先调用 Initialize 或检查配置。", this);
            _warnedServiceNotReady = true;
        }

        private void WarnIfConfigMissing()
        {
            if ((shopAssets == null || shopAssets.Length == 0) && !_warnedMissingShops)
            {
                Debug.LogWarning("[NiumaShop] 未配置任何 ShopAsset。商店服务可以创建，但无法打开任何正式商店。", this);
                _warnedMissingShops = true;
            }

            if ((itemDefinitions == null || itemDefinitions.Length == 0) && !_warnedMissingItemDefinitions)
            {
                Debug.LogWarning("[NiumaShop] 未配置任何 ItemDefinition。商店服务可以创建，但商品显示、价格货币和购买校验可能进入 MissingItemDefinition。", this);
                _warnedMissingItemDefinitions = true;
            }
        }

        private void WarnIfInventoryMissing()
        {
            if (_inventoryService != null || _warnedMissingInventoryService)
            {
                return;
            }

            Debug.LogWarning("[NiumaShop] 未解析到 IInventoryService。商店 UI 可以构建，但货币检查、扣款和商品发放会失败。请绑定 inventoryController 或通过 GameContext 注册 IInventoryService。", this);
            _warnedMissingInventoryService = true;
        }

        private ShopOperationResult StoreResult(ShopOperationResult result)
        {
            LastOperationResult = result;
            return result;
        }

        private void LogResult(string actionName, ShopOperationResult result)
        {
            LastOperationResult = result;
            if (result == null)
            {
                Debug.LogWarning($"[NiumaShop] {actionName} 返回空结果。", this);
                return;
            }

            Debug.Log($"[NiumaShop] {actionName}：Succeeded={result.Succeeded}, Reason={result.PrimaryFailureReason}, Message={result.Message}, ShopId={result.ShopId}, ProductId={result.ProductId}, Paid={result.PaidPrices?.Length ?? 0}, Added={result.AddedItems?.Length ?? 0}, Removed={result.RemovedItems?.Length ?? 0}, Revision={GetShopRevision(result.ShopId)}", this);
        }
    }
}
