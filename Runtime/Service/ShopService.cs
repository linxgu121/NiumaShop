using System;
using System.Collections.Generic;
using System.Text;
using NiumaInventory.Config;
using NiumaInventory.Request;
using NiumaInventory.Service;
using NiumaShop.Config;
using NiumaShop.Data;
using NiumaShop.Enum;
using NiumaShop.Request;
using NiumaShop.Result;
using NiumaShop.ViewData;
using UnityEngine;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店核心服务。
    /// 负责商店查询、购买校验、背包交易、库存限购更新和存档快照导入导出。
    /// </summary>
    public sealed class ShopService : IShopService, IShopConfigurationService
    {
        private const int CurrentSaveVersion = 1;
        private const string MissingKey = "missing";
        private const string DiscountsDisabledKey = "__discounts_disabled__";

        private readonly ShopRegistry _shopRegistry;
        private readonly ItemDefinitionRegistry _itemRegistry;
        private readonly Dictionary<string, ShopRuntimeState> _runtimeStates =
            new Dictionary<string, ShopRuntimeState>(StringComparer.Ordinal);
        private readonly List<ShopProductViewData> _productViewBuffer = new List<ShopProductViewData>();
        private readonly List<ShopFailureReason> _failureBuffer = new List<ShopFailureReason>();
        private readonly List<string> _migrationWarnings = new List<string>();

        private IInventoryService _inventoryService;
        private IShopConditionResolver _conditionResolver;
        private IShopPriceResolver _priceResolver;
        private ShopTransactionExecutor _transactionExecutor;
        private ItemDefinition[] _itemDefinitions;
        private long _revision;
        private bool _transactionInProgress;

        /// <summary>
        /// 商店模块全局修订号。
        /// </summary>
        public long Revision => _revision;

        /// <summary>
        /// 最近一次导入或配置同步产生的迁移警告。
        /// </summary>
        public string[] LastMigrationWarnings => _migrationWarnings.ToArray();

        /// <summary>
        /// 最近一次配置注册表校验报告。
        /// </summary>
        public ShopAssetValidationReport LastValidationReport => _shopRegistry.LastReport;

        public ShopService(
            IEnumerable<ShopAsset> shopAssets = null,
            IInventoryService inventoryService = null,
            IEnumerable<ItemDefinition> itemDefinitions = null,
            IShopConditionResolver conditionResolver = null,
            IShopPriceResolver priceResolver = null,
            bool strictRegistryMode = false)
        {
            _inventoryService = inventoryService;
            _conditionResolver = conditionResolver;
            _priceResolver = priceResolver;
            _itemDefinitions = CloneItemDefinitions(itemDefinitions);
            _itemRegistry = new ItemDefinitionRegistry(_itemDefinitions);
            _shopRegistry = new ShopRegistry(shopAssets, _itemDefinitions, strictRegistryMode);
            _transactionExecutor = new ShopTransactionExecutor(_inventoryService);

            if (!_shopRegistry.LastReport.IsValid)
            {
                Debug.LogError("[NiumaShop] 商店配置存在错误，服务可能无法正常工作。");
            }
        }

        /// <summary>
        /// 更新背包服务引用。
        /// 该方法不修改商店进度。
        /// </summary>
        public void SetInventoryService(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
            _transactionExecutor = new ShopTransactionExecutor(_inventoryService);
        }

        /// <summary>
        /// 同步物品定义索引。
        /// InventoryController 热更新或重载 ItemDefinition 后，应显式调用该方法，避免商店 UI 继续显示旧图标、旧品质或旧描述。
        /// </summary>
        public ShopAssetValidationReport SetItemDefinitions(
            IEnumerable<ItemDefinition> itemDefinitions,
            bool revalidateShops = true)
        {
            _itemDefinitions = CloneItemDefinitions(itemDefinitions) ?? Array.Empty<ItemDefinition>();
            _itemRegistry.SetDefinitions(_itemDefinitions);

            var report = _shopRegistry.LastReport;
            if (revalidateShops)
            {
                report = _shopRegistry.SetShops(_shopRegistry.GetAllShops(), _itemDefinitions, _shopRegistry.StrictMode);
                RefreshRuntimeStatesAgainstConfig();
            }

            BumpAllRuntimeRevisions();
            return report;
        }

        /// <summary>
        /// 更新外部条件解析器。
        /// </summary>
        public void SetConditionResolver(IShopConditionResolver conditionResolver)
        {
            _conditionResolver = conditionResolver;
        }

        /// <summary>
        /// 更新外部价格解析器。
        /// </summary>
        public void SetPriceResolver(IShopPriceResolver priceResolver)
        {
            _priceResolver = priceResolver;
        }

        /// <summary>
        /// 重建商店配置索引。
        /// 已有运行时状态会尽量按 ShopId / ProductId 继续保留。
        /// </summary>
        public ShopAssetValidationReport SetShops(
            IEnumerable<ShopAsset> shopAssets,
            IEnumerable<ItemDefinition> itemDefinitions = null,
            bool strictRegistryMode = false)
        {
            if (itemDefinitions != null)
            {
                _itemDefinitions = CloneItemDefinitions(itemDefinitions) ?? Array.Empty<ItemDefinition>();
                _itemRegistry.SetDefinitions(_itemDefinitions);
            }

            var report = _shopRegistry.SetShops(shopAssets, _itemDefinitions, strictRegistryMode);
            RefreshRuntimeStatesAgainstConfig();
            if (!report.IsValid)
            {
                Debug.LogError("[NiumaShop] 商店配置存在错误，服务可能无法正常工作。");
            }

            BumpRevision();
            return report;
        }

        public long GetRevision(string shopId)
        {
            return TryGetRuntimeState(shopId, out var state) && state != null ? state.Revision : 0L;
        }

        public bool TryGetShopState(string shopId, out ShopProgressSnapshot snapshot)
        {
            snapshot = null;
            if (!TryGetRuntimeState(shopId, out var state) || state == null)
            {
                return false;
            }

            snapshot = state.ToSnapshot();
            return true;
        }

        public ShopPanelViewData BuildShopViewData(string shopId)
        {
            if (!_shopRegistry.TryGetShop(shopId, out var shop)
                || !TryGetRuntimeState(shopId, out var state)
                || state == null)
            {
                return new ShopPanelViewData
                {
                    Revision = Revision,
                    ShopId = shopId,
                    DisplayName = shopId,
                    Description = string.Empty,
                    State = ShopState.None,
                    Products = Array.Empty<ShopProductViewData>(),
                    LastOperationResult = ShopOperationResult.Fail(
                        shopId,
                        null,
                        ShopTransactionType.None,
                        ShopFailureReason.ShopNotFound,
                        "找不到商店。")
                };
            }

            _productViewBuffer.Clear();
            for (var i = 0; shop.Products != null && i < shop.Products.Length; i++)
            {
                var product = shop.Products[i];
                if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                {
                    continue;
                }

                var productState = FindProductState(state, product.ProductId);
                if (productState != null && productState.State == ShopProductState.Hidden)
                {
                    continue;
                }

                _productViewBuffer.Add(BuildProductViewData(shop, state, product));
            }

            return new ShopPanelViewData
            {
                Revision = state.Revision,
                ShopId = shop.ShopId,
                DisplayName = string.IsNullOrWhiteSpace(shop.DisplayName) ? shop.ShopId : shop.DisplayName,
                Description = shop.Description,
                State = state.State,
                Products = _productViewBuffer.ToArray(),
                LastOperationResult = null
            };
        }

        public bool CanBuy(in BuyProductRequest request, out ShopOperationResult result)
        {
            var evaluation = EvaluateBuy(request, true);
            result = evaluation.ToOperationResult();
            return evaluation.CanBuy;
        }

        public bool CanSell(in SellItemRequest request, out ShopOperationResult result)
        {
            result = ShopOperationResult.Fail(
                request.ShopId,
                null,
                ShopTransactionType.Sell,
                ShopFailureReason.InvalidRequest,
                "第一版商店服务暂不开放出售功能。");
            return false;
        }

        public ShopOperationResult BuyProduct(in BuyProductRequest request)
        {
            if (_transactionInProgress)
            {
                return ShopOperationResult.Fail(
                    request.ShopId,
                    request.ProductId,
                    ShopTransactionType.Buy,
                    ShopFailureReason.TransactionInProgress,
                    "已有商店交易正在执行，已拒绝重复进入。");
            }

            var evaluation = EvaluateBuy(request, true);
            if (!evaluation.CanBuy)
            {
                return evaluation.ToOperationResult();
            }

            _transactionInProgress = true;
            try
            {
                var transactionResult = _transactionExecutor.ExecuteBuy(
                    evaluation.Shop.ShopId,
                    evaluation.Product.ProductId,
                    evaluation.Product.ItemId,
                    evaluation.ItemCount,
                    request.TargetContainerId,
                    evaluation.Prices,
                    evaluation.PriceViews,
                    request.SourceModule);

                if (!transactionResult.Succeeded)
                {
                    return transactionResult;
                }

                ApplySuccessfulPurchase(evaluation);
                return transactionResult;
            }
            finally
            {
                _transactionInProgress = false;
            }
        }

        public ShopOperationResult SellItem(in SellItemRequest request)
        {
            return ShopOperationResult.Fail(
                request.ShopId,
                null,
                ShopTransactionType.Sell,
                ShopFailureReason.InvalidRequest,
                "第一版商店服务暂不开放出售功能。");
        }

        public bool SetShopUnlocked(in UnlockShopRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId)
                || !TryGetRuntimeState(request.ShopId, out var state)
                || state == null)
            {
                return false;
            }

            var targetState = request.Unlock ? ShopState.Unlocked : ShopState.Locked;
            if (state.State == targetState)
            {
                return false;
            }

            state.State = targetState;
            BumpShopRevision(state);
            return true;
        }

        public bool SetProductUnlocked(in UnlockShopProductRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId)
                || string.IsNullOrWhiteSpace(request.ProductId)
                || !TryGetRuntimeState(request.ShopId, out var state)
                || state == null)
            {
                return false;
            }

            var productState = FindProductState(state, request.ProductId);
            if (productState == null || productState.State == ShopProductState.SoldOut)
            {
                return false;
            }

            var targetState = request.Unlock ? ShopProductState.Unlocked : ShopProductState.Locked;
            if (productState.State == targetState)
            {
                return false;
            }

            productState.State = targetState;
            BumpShopRevision(state);
            return true;
        }

        public bool SetActiveDiscounts(in SetShopDiscountsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId)
                || !TryGetRuntimeState(request.ShopId, out var state)
                || state == null)
            {
                return false;
            }

            var nextIds = NormalizeDiscountIds(request.DiscountIds);
            if (AreStringArraysSame(state.ActiveDiscountIds, nextIds))
            {
                return false;
            }

            state.ActiveDiscountIds = nextIds;
            BumpShopRevision(state);
            return true;
        }

        public bool ResetDiscountsToDefault(in ResetShopDiscountsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId)
                || !TryGetRuntimeState(request.ShopId, out var state)
                || state == null)
            {
                return false;
            }

            // 空数组是“使用配置默认折扣”的运行时状态。
            // 显式关闭折扣由 SetActiveDiscounts 写入内部禁用标记，避免两个语义混在一起。
            var defaultState = Array.Empty<string>();
            if (AreStringArraysSame(state.ActiveDiscountIds, defaultState))
            {
                return false;
            }

            state.ActiveDiscountIds = defaultState;
            BumpShopRevision(state);
            return true;
        }

        public bool RefreshShop(in RefreshShopRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId)
                || !_shopRegistry.TryGetShop(request.ShopId, out var shop)
                || !TryGetRuntimeState(request.ShopId, out var state)
                || state == null)
            {
                return false;
            }

            var previousShopState = state.State;
            var refreshed = CreateDefaultRuntimeState(shop);
            state.State = previousShopState;
            state.Products = refreshed.Products;
            state.LastRefreshUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            BumpShopRevision(state);
            return true;
        }

        public ShopProgressSnapshot[] ExportSnapshots()
        {
            if (_runtimeStates.Count == 0)
            {
                return Array.Empty<ShopProgressSnapshot>();
            }

            var result = new List<ShopProgressSnapshot>(_runtimeStates.Count);
            CopyShopSnapshots(result);
            return result.ToArray();
        }

        public void CopyShopSnapshots(List<ShopProgressSnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            foreach (var pair in _runtimeStates)
            {
                var state = pair.Value;
                if (state == null || string.IsNullOrWhiteSpace(state.ShopId))
                {
                    continue;
                }

                output.Add(state.ToSnapshot());
            }
        }

        public void ImportSnapshots(IEnumerable<ShopProgressSnapshot> snapshots)
        {
            ImportSnapshotsInternal(snapshots, false, null);
        }

        public bool ImportSaveData(ShopSaveData saveData)
        {
            if (saveData == null)
            {
                RecordMigrationWarning("商店存档数据为空，已拒绝导入。");
                return false;
            }

            return ImportSnapshotsInternal(
                saveData.Shops ?? Array.Empty<ShopProgressSnapshot>(),
                true,
                Math.Max(0L, saveData.Revision));
        }

        private ShopProductViewData BuildProductViewData(
            ShopAsset shop,
            ShopRuntimeState state,
            ShopProductData product)
        {
            var productState = FindProductState(state, product.ProductId);
            var request = new BuyProductRequest(shop.ShopId, product.ProductId, 1, null, "NiumaShopUI");
            var evaluation = EvaluateBuy(request, true);
            _itemRegistry.TryGetDefinition(product.ItemId, out var definition);

            return new ShopProductViewData
            {
                ShopId = shop.ShopId,
                ProductId = product.ProductId,
                ItemId = product.ItemId,
                DisplayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : product.ItemId,
                Description = definition != null ? definition.Description : string.Empty,
                IconAddress = definition != null ? definition.IconAddress : string.Empty,
                ItemTypeKey = MapItemTypeKey(definition),
                QualityKey = MapQualityKey(definition),
                Count = product.Count,
                Prices = evaluation.PriceViews,
                RemainingStock = productState != null ? productState.RemainingStock : product.InitialStock,
                PurchasedCount = productState != null ? productState.PurchasedCount : 0,
                MaxPurchaseCount = product.MaxPurchaseCount,
                MaxPurchasableCount = CalculateMaxPurchasable(product, productState),
                IsUnlocked = productState != null && productState.State == ShopProductState.Unlocked,
                IsSoldOut = productState != null && productState.State == ShopProductState.SoldOut,
                CanBuy = evaluation.CanBuy,
                CannotBuyReasons = evaluation.FailureReasons
            };
        }

        private ShopBuyEvaluation EvaluateBuy(in BuyProductRequest request, bool checkInventoryCapacity)
        {
            var count = request.Count;
            if (count <= 0)
            {
                return ShopBuyEvaluation.Fail(
                    request.ShopId,
                    request.ProductId,
                    count,
                    ShopFailureReason.InvalidRequest,
                    "购买份数必须大于 0。");
            }

            if (_inventoryService == null)
            {
                return ShopBuyEvaluation.Fail(
                    request.ShopId,
                    request.ProductId,
                    count,
                    ShopFailureReason.ServiceNotReady,
                    "背包服务未就绪。");
            }

            if (!_shopRegistry.TryGetShop(request.ShopId, out var shop)
                || !TryGetRuntimeState(request.ShopId, out var state)
                || state == null)
            {
                return ShopBuyEvaluation.Fail(
                    request.ShopId,
                    request.ProductId,
                    count,
                    ShopFailureReason.ShopNotFound,
                    "找不到商店。");
            }

            if (!ShopRegistry.TryGetProduct(shop, request.ProductId, out var product) || product == null)
            {
                return ShopBuyEvaluation.Fail(
                    request.ShopId,
                    request.ProductId,
                    count,
                    ShopFailureReason.ProductNotFound,
                    "找不到商品。");
            }

            var productState = FindProductState(state, product.ProductId);
            var evaluation = ShopBuyEvaluation.SuccessBase(shop, state, product, productState, request, count);
            AppendStateFailures(evaluation, shop, state, product, productState, count);
            AppendDefinitionFailures(evaluation, product);
            AppendConditionFailures(evaluation, shop.OpenConditions, request);
            AppendConditionFailures(evaluation, product.BuyConditions, request);

            var priceResolution = ResolveBuyPrices(shop, state, product, request);
            evaluation.Prices = priceResolution.Prices;
            evaluation.PriceViews = BuildPriceViews(
                priceResolution.Prices,
                priceResolution.BasePrices,
                priceResolution.AppliedMultiplier,
                priceResolution.AppliedDiscountId);
            AppendPriceFailures(evaluation);
            AppendInventoryCapacityFailure(evaluation, request, checkInventoryCapacity);

            if (evaluation.FailureReasons.Length > 0)
            {
                evaluation.CanBuy = false;
                return evaluation;
            }

            evaluation.CanBuy = true;
            return evaluation;
        }

        private void AppendStateFailures(
            ShopBuyEvaluation evaluation,
            ShopAsset shop,
            ShopRuntimeState state,
            ShopProductData product,
            ShopProductRuntimeState productState,
            int count)
        {
            if (state.State != ShopState.Unlocked)
            {
                evaluation.AddFailure(ShopFailureReason.ShopLocked);
            }

            if (productState == null)
            {
                evaluation.AddFailure(ShopFailureReason.ProductNotFound);
                return;
            }

            if (productState.State == ShopProductState.Locked || productState.State == ShopProductState.Hidden)
            {
                evaluation.AddFailure(ShopFailureReason.ProductLocked);
            }
            else if (productState.State == ShopProductState.SoldOut)
            {
                evaluation.AddFailure(ShopFailureReason.SoldOut);
            }

            var stockAvailable = productState.RemainingStock < 0 ? int.MaxValue : Math.Max(0, productState.RemainingStock);
            if (stockAvailable <= 0 || count > stockAvailable)
            {
                evaluation.AddFailure(ShopFailureReason.SoldOut);
            }

            var limitAvailable = product.MaxPurchaseCount < 0
                ? int.MaxValue
                : Math.Max(0, product.MaxPurchaseCount - productState.PurchasedCount);
            if (limitAvailable <= 0 || count > limitAvailable)
            {
                evaluation.AddFailure(ShopFailureReason.PurchaseLimitReached);
            }

            if (!TryMultiplyPositive(product.Count, count, out var itemCount))
            {
                evaluation.AddFailure(ShopFailureReason.InvalidRequest);
                evaluation.FailureMessage = "购买数量过大，超过 int 可表示范围。";
            }
            else
            {
                evaluation.ItemCount = itemCount;
            }
        }

        private void AppendDefinitionFailures(ShopBuyEvaluation evaluation, ShopProductData product)
        {
            if (string.IsNullOrWhiteSpace(product.ItemId) || !_itemRegistry.Contains(product.ItemId))
            {
                evaluation.AddFailure(ShopFailureReason.MissingItemDefinition);
            }
        }

        private void AppendConditionFailures(
            ShopBuyEvaluation evaluation,
            ShopConditionData[] conditions,
            in BuyProductRequest request)
        {
            for (var i = 0; conditions != null && i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.ConditionType == ShopConditionType.None)
                {
                    continue;
                }

                if (!IsConditionMet(condition, request, out var reason, out var message))
                {
                    evaluation.AddFailure(reason == ShopFailureReason.None ? ShopFailureReason.ConditionNotMet : reason);
                    if (!string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(evaluation.FailureMessage))
                    {
                        evaluation.FailureMessage = message;
                    }
                }
            }
        }

        private bool IsConditionMet(
            ShopConditionData condition,
            in BuyProductRequest request,
            out ShopFailureReason failureReason,
            out string message)
        {
            failureReason = ShopFailureReason.ConditionNotMet;
            message = null;
            var met = condition.ConditionType switch
            {
                ShopConditionType.ShopUnlocked => IsShopUnlocked(condition.TargetId),
                ShopConditionType.ProductUnlocked => IsProductUnlocked(request.ShopId, condition.TargetId),
                ShopConditionType.HasItem => _inventoryService != null
                                             && _inventoryService.HasItem(condition.TargetId, Math.Max(1, condition.RequiredCount)),
                _ => TryExternalCondition(condition, request, out failureReason, out message)
            };

            return condition.Invert ? !met : met;
        }

        private bool TryExternalCondition(
            ShopConditionData condition,
            in BuyProductRequest request,
            out ShopFailureReason failureReason,
            out string message)
        {
            failureReason = ShopFailureReason.ExternalRejected;
            message = null;
            if (_conditionResolver == null)
            {
                message = $"缺少外部商店条件解析器：{condition.ConditionType}。";
                return false;
            }

            if (!_conditionResolver.TryEvaluate(condition, request, out var isMet, out var resolverReason, out var resolverMessage))
            {
                message = resolverMessage ?? $"外部商店条件解析器无法处理：{condition.ConditionType}。";
                return false;
            }

            failureReason = resolverReason == ShopFailureReason.None ? ShopFailureReason.ConditionNotMet : resolverReason;
            message = resolverMessage;
            return isMet;
        }

        private void AppendPriceFailures(ShopBuyEvaluation evaluation)
        {
            if (evaluation.Prices == null || evaluation.Prices.Length == 0)
            {
                return;
            }

            for (var i = 0; i < evaluation.Prices.Length; i++)
            {
                var price = evaluation.Prices[i];
                if (price == null || price.Amount <= 0)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(price.CurrencyItemId) || !_itemRegistry.Contains(price.CurrencyItemId))
                {
                    evaluation.AddFailure(ShopFailureReason.MissingItemDefinition);
                    continue;
                }

                if (_inventoryService.GetItemCount(price.CurrencyItemId) < price.Amount)
                {
                    evaluation.AddFailure(ShopFailureReason.InsufficientCurrency);
                }
            }
        }

        private void AppendInventoryCapacityFailure(
            ShopBuyEvaluation evaluation,
            in BuyProductRequest request,
            bool checkInventoryCapacity)
        {
            if (!checkInventoryCapacity || evaluation.ItemCount <= 0)
            {
                return;
            }

            var result = _inventoryService.CanAddItem(new AddItemRequest
            {
                ItemId = evaluation.Product.ItemId,
                Count = evaluation.ItemCount,
                TargetContainerId = request.TargetContainerId,
                TargetSlotIndex = -1,
                AllowPartial = false,
                SourceModule = "NiumaShop"
            });

            if (!result.Succeeded)
            {
                evaluation.AddFailure(ShopFailureReason.InventoryRejected);
                if (string.IsNullOrWhiteSpace(evaluation.FailureMessage))
                {
                    evaluation.FailureMessage = result.Message;
                }
            }
        }

        private ShopPriceResolution ResolveBuyPrices(
            ShopAsset shop,
            ShopRuntimeState state,
            ShopProductData product,
            in BuyProductRequest request)
        {
            if (_priceResolver != null
                && _priceResolver.TryResolveBuyPrices(shop, product, request, out var resolved, out _)
                && resolved != null)
            {
                var resolvedPrices = ClonePrices(resolved);
                return new ShopPriceResolution(resolvedPrices, ClonePrices(resolvedPrices), 1f, null);
            }

            var basePrices = BuildDefaultPrices(product.Prices, request.Count);
            var discount = ResolveBestDiscount(shop, state, product, request);
            return new ShopPriceResolution(
                ApplyDiscountMultiplier(basePrices, discount.Multiplier),
                ClonePrices(basePrices),
                discount.Multiplier,
                discount.DiscountId);
        }

        /// <summary>
        /// 计算本次购买可用的最低折扣乘数。
        /// 第一版不做连续折扣叠乘，只从满足条件的折扣中取最低价格，避免折扣叠爆。
        /// </summary>
        private ShopDiscountSelection ResolveBestDiscount(
            ShopAsset shop,
            ShopRuntimeState state,
            ShopProductData product,
            in BuyProductRequest request)
        {
            var hasMatchedDiscount = false;
            var bestMultiplier = 1f;
            string bestDiscountId = null;
            for (var i = 0; shop?.DefaultDiscounts != null && i < shop.DefaultDiscounts.Length; i++)
            {
                var discount = shop.DefaultDiscounts[i];
                if (!IsDiscountApplicable(discount, state, product, request))
                {
                    continue;
                }

                var multiplier = Mathf.Max(0f, discount.PriceMultiplier);
                if (!hasMatchedDiscount || multiplier < bestMultiplier)
                {
                    hasMatchedDiscount = true;
                    bestMultiplier = multiplier;
                    bestDiscountId = discount.DiscountId;
                }
            }

            return new ShopDiscountSelection(
                hasMatchedDiscount ? bestMultiplier : 1f,
                hasMatchedDiscount ? bestDiscountId : null);
        }

        /// <summary>
        /// 判断折扣是否适用于当前商品和购买请求。
        /// ActiveDiscountIds 为空时表示使用配置默认折扣；包含内部禁用标记时表示关闭全部折扣；其它非空数组只启用被剧情/任务显式激活的折扣。
        /// </summary>
        private bool IsDiscountApplicable(
            ShopDiscountData discount,
            ShopRuntimeState state,
            ShopProductData product,
            in BuyProductRequest request)
        {
            if (discount == null || product == null)
            {
                return false;
            }

            if (!IsDiscountRuntimeActive(discount, state)
                || !IsDiscountTargetMatched(discount, product))
            {
                return false;
            }

            for (var i = 0; discount.Conditions != null && i < discount.Conditions.Length; i++)
            {
                var condition = discount.Conditions[i];
                if (condition == null || condition.ConditionType == ShopConditionType.None)
                {
                    continue;
                }

                if (!IsConditionMet(condition, request, out _, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsDiscountRuntimeActive(ShopDiscountData discount, ShopRuntimeState state)
        {
            if (ContainsString(state?.ActiveDiscountIds, DiscountsDisabledKey))
            {
                return false;
            }

            if (state?.ActiveDiscountIds == null || state.ActiveDiscountIds.Length == 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(discount.DiscountId)
                   && ContainsString(state.ActiveDiscountIds, discount.DiscountId);
        }

        private static bool IsDiscountTargetMatched(ShopDiscountData discount, ShopProductData product)
        {
            var hasProductFilter = HasAnyString(discount.ProductIds);
            var hasTagFilter = HasAnyString(discount.ProductTags);
            if (!hasProductFilter && !hasTagFilter)
            {
                return true;
            }

            return (hasProductFilter && ContainsString(discount.ProductIds, product.ProductId))
                   || (hasTagFilter && HasAnyMatchingTag(product.Tags, discount.ProductTags));
        }

        /// <summary>
        /// 把折扣乘数应用到已按购买份数计算后的价格上。
        /// 正数价格使用向上取整；只有乘数显式为 0 时才会把价格变成免费。
        /// </summary>
        private static ShopPriceData[] ApplyDiscountMultiplier(ShopPriceData[] prices, float multiplier)
        {
            if (prices == null || prices.Length == 0)
            {
                return Array.Empty<ShopPriceData>();
            }

            if (Mathf.Approximately(multiplier, 1f))
            {
                return ClonePrices(prices);
            }

            var safeMultiplier = Mathf.Max(0f, multiplier);
            var result = new List<ShopPriceData>();
            for (var i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price == null || string.IsNullOrWhiteSpace(price.CurrencyItemId))
                {
                    continue;
                }

                result.Add(new ShopPriceData
                {
                    CurrencyItemId = price.CurrencyItemId,
                    Amount = CalculateDiscountedAmount(price.Amount, safeMultiplier)
                });
            }

            return result.ToArray();
        }

        private static int CalculateDiscountedAmount(int baseAmount, float multiplier)
        {
            if (baseAmount <= 0)
            {
                return 0;
            }

            if (multiplier <= 0f)
            {
                return 0;
            }

            var discounted = Mathf.CeilToInt(baseAmount * multiplier);
            return Math.Max(1, discounted);
        }

        private ShopPriceViewData[] BuildPriceViews(
            ShopPriceData[] prices,
            ShopPriceData[] basePrices,
            float appliedMultiplier,
            string appliedDiscountId)
        {
            if (prices == null || prices.Length == 0)
            {
                return Array.Empty<ShopPriceViewData>();
            }

            var result = new List<ShopPriceViewData>();
            for (var i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price == null || string.IsNullOrWhiteSpace(price.CurrencyItemId))
                {
                    continue;
                }

                _itemRegistry.TryGetDefinition(price.CurrencyItemId, out var definition);
                var owned = _inventoryService != null ? _inventoryService.GetItemCount(price.CurrencyItemId) : 0;
                var originalAmount = FindPriceAmount(basePrices, price.CurrencyItemId, price.Amount);
                result.Add(new ShopPriceViewData
                {
                    CurrencyItemId = price.CurrencyItemId,
                    DisplayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                        ? definition.DisplayName
                        : price.CurrencyItemId,
                    IconAddress = definition != null ? definition.IconAddress : string.Empty,
                    Amount = Math.Max(0, price.Amount),
                    OriginalAmount = Math.Max(0, originalAmount),
                    HasPriceModifier = originalAmount != price.Amount || !Mathf.Approximately(appliedMultiplier, 1f),
                    AppliedDiscountId = appliedDiscountId,
                    PriceMultiplier = appliedMultiplier,
                    PlayerOwnedCount = owned,
                    IsEnough = owned >= Math.Max(0, price.Amount)
                });
            }

            return result.ToArray();
        }

        private void ApplySuccessfulPurchase(ShopBuyEvaluation evaluation)
        {
            var productState = evaluation.ProductState;
            if (productState == null)
            {
                return;
            }

            if (evaluation.Product.OneShot)
            {
                productState.State = ShopProductState.SoldOut;
                productState.RemainingStock = 0;
            }
            else if (productState.RemainingStock >= 0)
            {
                productState.RemainingStock = Math.Max(0, productState.RemainingStock - evaluation.RequestedCount);
                if (productState.RemainingStock == 0)
                {
                    productState.State = ShopProductState.SoldOut;
                }
            }

            productState.PurchasedCount = SafeAdd(productState.PurchasedCount, evaluation.RequestedCount);
            BumpShopRevision(evaluation.State);
        }

        private bool TryGetRuntimeState(string shopId, out ShopRuntimeState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(shopId))
            {
                return false;
            }

            if (_runtimeStates.TryGetValue(shopId, out state) && state != null)
            {
                return true;
            }

            if (!_shopRegistry.TryGetShop(shopId, out var shop))
            {
                return false;
            }

            state = CreateDefaultRuntimeState(shop);
            _runtimeStates[shopId] = state;
            return true;
        }

        private static ShopRuntimeState CreateDefaultRuntimeState(ShopAsset shop)
        {
            var products = shop.Products != null && shop.Products.Length > 0
                ? new ShopProductRuntimeState[shop.Products.Length]
                : Array.Empty<ShopProductRuntimeState>();

            var writeIndex = 0;
            for (var i = 0; shop.Products != null && i < shop.Products.Length; i++)
            {
                var product = shop.Products[i];
                if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                {
                    continue;
                }

                products[writeIndex++] = CreateDefaultProductRuntimeState(product);
            }

            if (writeIndex != products.Length)
            {
                Array.Resize(ref products, writeIndex);
            }

            return new ShopRuntimeState
            {
                ShopId = shop.ShopId,
                State = shop.DefaultUnlocked ? ShopState.Unlocked : ShopState.Locked,
                Revision = 0L,
                Products = products,
                ActiveDiscountIds = Array.Empty<string>(),
                LastRefreshUnixSeconds = 0L
            };
        }

        private static ShopProductRuntimeState CreateDefaultProductRuntimeState(ShopProductData product)
        {
            var stock = product.InitialStock;
            var state = product.DefaultUnlocked ? ShopProductState.Unlocked : ShopProductState.Locked;
            if (stock == 0)
            {
                state = ShopProductState.SoldOut;
            }

            return new ShopProductRuntimeState
            {
                ProductId = product.ProductId,
                State = state,
                RemainingStock = stock,
                PurchasedCount = 0
            };
        }

        private static ShopProductRuntimeState FindProductState(ShopRuntimeState state, string productId)
        {
            for (var i = 0; state != null && state.Products != null && i < state.Products.Length; i++)
            {
                var product = state.Products[i];
                if (product != null && string.Equals(product.ProductId, productId, StringComparison.Ordinal))
                {
                    return product;
                }
            }

            return null;
        }

        private bool ImportSnapshotsInternal(
            IEnumerable<ShopProgressSnapshot> snapshots,
            bool allowEmpty,
            long? importedRevision)
        {
            if (!TryMaterializeSnapshots(snapshots, out var snapshotList, out var materializeError))
            {
                RecordMigrationWarning(materializeError);
                return false;
            }

            if (snapshotList.Count == 0 && !allowEmpty)
            {
                RecordMigrationWarning("商店存档快照集合为空，已拒绝导入，避免误清空商店进度。");
                return false;
            }

            var newStates = new Dictionary<string, ShopRuntimeState>(StringComparer.Ordinal);
            var newWarnings = new List<string>();
            var hasConfigMigrationChange = false;
            for (var i = 0; i < snapshotList.Count; i++)
            {
                var snapshot = snapshotList[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ShopId))
                {
                    newWarnings.Add("商店存档中存在空快照或空 ShopId，已跳过。");
                    continue;
                }

                if (!_shopRegistry.TryGetShop(snapshot.ShopId, out var shop))
                {
                    newWarnings.Add($"商店存档中的 ShopId={snapshot.ShopId} 缺少当前配置，已跳过该商店进度。");
                    continue;
                }

                var mergedState = MergeSnapshotWithConfig(snapshot, shop, newWarnings);
                if (mergedState != null && mergedState.Revision != Math.Max(0L, snapshot.Revision))
                {
                    hasConfigMigrationChange = true;
                }

                newStates[snapshot.ShopId] = mergedState;
            }

            _runtimeStates.Clear();
            foreach (var pair in newStates)
            {
                _runtimeStates[pair.Key] = pair.Value;
            }

            _migrationWarnings.Clear();
            _migrationWarnings.AddRange(newWarnings);
            if (importedRevision.HasValue)
            {
                _revision = importedRevision.Value;
            }

            // 读档导入应继承存档 Revision，不能凭空制造一次业务变更。
            // 只有配置迁移实际修复了运行时状态时，才递增全局 Revision，让存档脏标记能感知迁移结果。
            if (!importedRevision.HasValue || hasConfigMigrationChange)
            {
                BumpRevision();
            }

            return true;
        }

        private static bool TryMaterializeSnapshots(
            IEnumerable<ShopProgressSnapshot> snapshots,
            out List<ShopProgressSnapshot> result,
            out string error)
        {
            result = null;
            error = null;
            if (snapshots == null)
            {
                error = "商店存档快照集合为空引用，已拒绝导入。";
                return false;
            }

            try
            {
                result = new List<ShopProgressSnapshot>();
                foreach (var snapshot in snapshots)
                {
                    result.Add(snapshot);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"商店存档快照枚举失败，已拒绝导入：{ex.Message}";
                return false;
            }
        }

        private ShopRuntimeState MergeSnapshotWithConfig(ShopProgressSnapshot snapshot, ShopAsset shop)
        {
            return MergeSnapshotWithConfig(snapshot, shop, _migrationWarnings);
        }

        private ShopRuntimeState MergeSnapshotWithConfig(
            ShopProgressSnapshot snapshot,
            ShopAsset shop,
            List<string> migrationWarnings)
        {
            var state = CreateDefaultRuntimeState(shop);
            state.State = snapshot.State;
            state.Revision = Math.Max(0L, snapshot.Revision);
            state.ActiveDiscountIds = CloneStringArray(snapshot.ActiveDiscountIds);
            state.LastRefreshUnixSeconds = Math.Max(0L, snapshot.LastRefreshUnixSeconds);
            var snapshotProductIds = new HashSet<string>(StringComparer.Ordinal);
            var configChanged = false;

            for (var i = 0; snapshot.Products != null && i < snapshot.Products.Length; i++)
            {
                var productSnapshot = snapshot.Products[i];
                if (productSnapshot == null || string.IsNullOrWhiteSpace(productSnapshot.ProductId))
                {
                    configChanged = true;
                    continue;
                }

                snapshotProductIds.Add(productSnapshot.ProductId);
                var productState = FindProductState(state, productSnapshot.ProductId);
                if (productState == null)
                {
                    migrationWarnings?.Add($"商店 {shop.ShopId} 的存档商品 ProductId={productSnapshot.ProductId} 缺少当前配置，已跳过该商品进度。");
                    configChanged = true;
                    continue;
                }

                productState.State = productSnapshot.State;
                productState.RemainingStock = productSnapshot.RemainingStock;
                productState.PurchasedCount = Math.Max(0, productSnapshot.PurchasedCount);
            }

            for (var i = 0; state.Products != null && i < state.Products.Length; i++)
            {
                var productState = state.Products[i];
                if (productState != null
                    && !string.IsNullOrWhiteSpace(productState.ProductId)
                    && !snapshotProductIds.Contains(productState.ProductId))
                {
                    configChanged = true;
                    break;
                }
            }

            if (configChanged)
            {
                state.BumpRevision();
            }

            return state;
        }

        private void RefreshRuntimeStatesAgainstConfig()
        {
            var keys = new List<string>(_runtimeStates.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                var shopId = keys[i];
                if (!_shopRegistry.TryGetShop(shopId, out var shop))
                {
                    _runtimeStates.Remove(shopId);
                    continue;
                }

                var snapshot = _runtimeStates[shopId]?.ToSnapshot();
                _runtimeStates[shopId] = snapshot != null
                    ? MergeSnapshotWithConfig(snapshot, shop)
                    : CreateDefaultRuntimeState(shop);
            }
        }

        private bool IsShopUnlocked(string shopId)
        {
            return TryGetRuntimeState(shopId, out var state) && state != null && state.State == ShopState.Unlocked;
        }

        private bool IsProductUnlocked(string shopId, string productId)
        {
            return TryGetRuntimeState(shopId, out var state)
                   && FindProductState(state, productId) is { State: ShopProductState.Unlocked };
        }

        private static int CalculateMaxPurchasable(ShopProductData product, ShopProductRuntimeState state)
        {
            if (product == null || state == null || state.State == ShopProductState.SoldOut)
            {
                return 0;
            }

            var stock = state.RemainingStock < 0 ? int.MaxValue : Math.Max(0, state.RemainingStock);
            var limit = product.MaxPurchaseCount < 0
                ? int.MaxValue
                : Math.Max(0, product.MaxPurchaseCount - state.PurchasedCount);
            return Math.Min(stock, limit);
        }

        private static ShopPriceData[] BuildDefaultPrices(ShopPriceData[] prices, int count)
        {
            if (prices == null || prices.Length == 0)
            {
                return Array.Empty<ShopPriceData>();
            }

            var result = new List<ShopPriceData>();
            for (var i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price == null || string.IsNullOrWhiteSpace(price.CurrencyItemId))
                {
                    continue;
                }

                result.Add(new ShopPriceData
                {
                    CurrencyItemId = price.CurrencyItemId,
                    Amount = SafeMultiply(price.Amount, count)
                });
            }

            return result.ToArray();
        }

        private static ShopPriceData[] ClonePrices(ShopPriceData[] prices)
        {
            if (prices == null || prices.Length == 0)
            {
                return Array.Empty<ShopPriceData>();
            }

            var result = new List<ShopPriceData>();
            for (var i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                if (price == null)
                {
                    continue;
                }

                result.Add(new ShopPriceData
                {
                    CurrencyItemId = price.CurrencyItemId,
                    Amount = Math.Max(0, price.Amount)
                });
            }

            return result.ToArray();
        }

        private static int FindPriceAmount(ShopPriceData[] prices, string currencyItemId, int fallback)
        {
            for (var i = 0; prices != null && i < prices.Length; i++)
            {
                var price = prices[i];
                if (price != null
                    && string.Equals(price.CurrencyItemId, currencyItemId, StringComparison.Ordinal))
                {
                    return Math.Max(0, price.Amount);
                }
            }

            return Math.Max(0, fallback);
        }

        private void BumpShopRevision(ShopRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            state.BumpRevision();
            BumpRevision();
        }

        private void BumpRevision()
        {
            if (_revision < 0)
            {
                _revision = 0;
            }

            if (_revision == long.MaxValue)
            {
                throw new InvalidOperationException("商店模块全局修订号已达到 long.MaxValue，无法继续递增。");
            }

            _revision++;
        }

        private void BumpAllRuntimeRevisions()
        {
            foreach (var pair in _runtimeStates)
            {
                pair.Value?.BumpRevision();
            }

            BumpRevision();
        }

        private void RecordMigrationWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _migrationWarnings.Add(message);
        }

        private static ItemDefinition[] CloneItemDefinitions(IEnumerable<ItemDefinition> itemDefinitions)
        {
            if (itemDefinitions == null)
            {
                return null;
            }

            var result = new List<ItemDefinition>();
            foreach (var definition in itemDefinitions)
            {
                if (definition != null)
                {
                    result.Add(definition);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<ItemDefinition>();
        }

        private static string MapItemTypeKey(ItemDefinition definition)
        {
            if (definition == null)
            {
                return MissingKey;
            }

            return definition.ItemType switch
            {
                NiumaInventory.Enum.ItemType.None => "none",
                NiumaInventory.Enum.ItemType.Material => "material",
                NiumaInventory.Enum.ItemType.Consumable => "consumable",
                NiumaInventory.Enum.ItemType.Equipment => "equipment",
                NiumaInventory.Enum.ItemType.Quest => "quest",
                NiumaInventory.Enum.ItemType.Currency => "currency",
                NiumaInventory.Enum.ItemType.KeyItem => "key_item",
                _ => ToSnakeCase(definition.ItemType.ToString())
            };
        }

        private static string MapQualityKey(ItemDefinition definition)
        {
            if (definition == null)
            {
                return MissingKey;
            }

            return definition.Quality switch
            {
                NiumaInventory.Enum.ItemQuality.Common => "common",
                NiumaInventory.Enum.ItemQuality.Uncommon => "uncommon",
                NiumaInventory.Enum.ItemQuality.Rare => "rare",
                NiumaInventory.Enum.ItemQuality.Epic => "epic",
                NiumaInventory.Enum.ItemQuality.Legendary => "legendary",
                NiumaInventory.Enum.ItemQuality.Story => "story",
                _ => ToSnakeCase(definition.Quality.ToString())
            };
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 4);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        builder.Append('_');
                    }

                    builder.Append(char.ToLowerInvariant(c));
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static bool TryMultiplyPositive(int a, int b, out int result)
        {
            result = 0;
            if (a <= 0 || b <= 0)
            {
                return false;
            }

            var value = (long)a * b;
            if (value > int.MaxValue)
            {
                return false;
            }

            result = (int)value;
            return true;
        }

        private static int SafeMultiply(int a, int b)
        {
            if (a <= 0 || b <= 0)
            {
                return 0;
            }

            var value = (long)a * b;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static int SafeAdd(int a, int b)
        {
            var value = (long)Math.Max(0, a) + Math.Max(0, b);
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static bool HasAnyString(string[] values)
        {
            for (var i = 0; values != null && i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsString(string[] values, string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            for (var i = 0; values != null && i < values.Length; i++)
            {
                if (string.Equals(values[i], target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyMatchingTag(string[] productTags, string[] discountTags)
        {
            for (var i = 0; productTags != null && i < productTags.Length; i++)
            {
                if (ContainsString(discountTags, productTags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] NormalizeDiscountIds(string[] discountIds)
        {
            if (discountIds == null || discountIds.Length == 0)
            {
                return new[] { DiscountsDisabledKey };
            }

            var result = new List<string>();
            for (var i = 0; i < discountIds.Length; i++)
            {
                var discountId = discountIds[i];
                if (string.IsNullOrWhiteSpace(discountId)
                    || string.Equals(discountId, DiscountsDisabledKey, StringComparison.Ordinal)
                    || result.Contains(discountId))
                {
                    continue;
                }

                result.Add(discountId);
            }

            return result.Count > 0 ? result.ToArray() : new[] { DiscountsDisabledKey };
        }

        private static bool AreStringArraysSame(string[] left, string[] right)
        {
            var leftLength = left?.Length ?? 0;
            var rightLength = right?.Length ?? 0;
            if (leftLength != rightLength)
            {
                return false;
            }

            for (var i = 0; i < leftLength; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] CloneStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }

        private sealed class ShopBuyEvaluation
        {
            private readonly List<ShopFailureReason> _failures = new List<ShopFailureReason>();

            public ShopAsset Shop;
            public ShopRuntimeState State;
            public ShopProductData Product;
            public ShopProductRuntimeState ProductState;
            public string ShopId;
            public string ProductId;
            public int RequestedCount;
            public int ItemCount;
            public bool CanBuy;
            public string FailureMessage;
            public ShopPriceData[] Prices = Array.Empty<ShopPriceData>();
            public ShopPriceViewData[] PriceViews = Array.Empty<ShopPriceViewData>();
            public ShopFailureReason[] FailureReasons => _failures.ToArray();

            public static ShopBuyEvaluation SuccessBase(
                ShopAsset shop,
                ShopRuntimeState state,
                ShopProductData product,
                ShopProductRuntimeState productState,
                in BuyProductRequest request,
                int count)
            {
                return new ShopBuyEvaluation
                {
                    Shop = shop,
                    State = state,
                    Product = product,
                    ProductState = productState,
                    ShopId = request.ShopId,
                    ProductId = request.ProductId,
                    RequestedCount = count
                };
            }

            public static ShopBuyEvaluation Fail(
                string shopId,
                string productId,
                int count,
                ShopFailureReason reason,
                string message)
            {
                var evaluation = new ShopBuyEvaluation
                {
                    ShopId = shopId,
                    ProductId = productId,
                    RequestedCount = count,
                    FailureMessage = message
                };
                evaluation.AddFailure(reason);
                return evaluation;
            }

            public void AddFailure(ShopFailureReason reason)
            {
                if (reason == ShopFailureReason.None || _failures.Contains(reason))
                {
                    return;
                }

                _failures.Add(reason);
            }

            public ShopOperationResult ToOperationResult()
            {
                if (CanBuy)
                {
                    return ShopOperationResult.Success(
                        ShopId,
                        ProductId,
                        ShopTransactionType.Buy,
                        paidPrices: PriceViews,
                        message: "可以购买。");
                }

                var reasons = FailureReasons;
                var primary = reasons.Length > 0 ? reasons[0] : ShopFailureReason.UnknownError;
                var additional = reasons.Length > 1
                    ? SliceReasons(reasons, 1)
                    : Array.Empty<ShopFailureReason>();

                var result = ShopOperationResult.Fail(
                    ShopId,
                    ProductId,
                    ShopTransactionType.Buy,
                    primary,
                    FailureMessage,
                    additional);
                result.PaidPrices = PriceViews;
                return result;
            }

            private static ShopFailureReason[] SliceReasons(ShopFailureReason[] source, int start)
            {
                if (source == null || start >= source.Length)
                {
                    return Array.Empty<ShopFailureReason>();
                }

                var result = new ShopFailureReason[source.Length - start];
                Array.Copy(source, start, result, 0, result.Length);
                return result;
            }
        }

        private readonly struct ShopPriceResolution
        {
            public readonly ShopPriceData[] Prices;
            public readonly ShopPriceData[] BasePrices;
            public readonly float AppliedMultiplier;
            public readonly string AppliedDiscountId;

            public ShopPriceResolution(
                ShopPriceData[] prices,
                ShopPriceData[] basePrices,
                float appliedMultiplier,
                string appliedDiscountId)
            {
                Prices = prices ?? Array.Empty<ShopPriceData>();
                BasePrices = basePrices ?? Array.Empty<ShopPriceData>();
                AppliedMultiplier = appliedMultiplier;
                AppliedDiscountId = appliedDiscountId;
            }
        }

        private readonly struct ShopDiscountSelection
        {
            public readonly float Multiplier;
            public readonly string DiscountId;

            public ShopDiscountSelection(float multiplier, string discountId)
            {
                Multiplier = multiplier;
                DiscountId = discountId;
            }
        }
    }
}
