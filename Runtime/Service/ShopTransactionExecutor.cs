using System;
using System.Collections.Generic;
using NiumaInventory.Data;
using NiumaInventory.Request;
using NiumaInventory.Service;
using NiumaShop.Config;
using NiumaShop.Enum;
using NiumaShop.Result;
using NiumaShop.ViewData;
using UnityEngine;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店背包事务执行器。
    /// 只负责扣货币、发商品和失败补偿，不修改商店运行时库存、限购或解锁状态。
    /// </summary>
    public sealed class ShopTransactionExecutor
    {
        private const string SourceModuleName = "NiumaShop";

        private readonly IInventoryService _inventoryService;
        private readonly List<InventoryItemSnapshot> _removedItems = new List<InventoryItemSnapshot>();
        private readonly List<InventoryItemSnapshot> _addedItems = new List<InventoryItemSnapshot>();
        private readonly List<ShopPriceData> _paidPrices = new List<ShopPriceData>();
        private readonly List<InventoryItemSnapshot> _itemSnapshotBuffer = new List<InventoryItemSnapshot>();

        public ShopTransactionExecutor(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        /// <summary>
        /// 执行购买事务。
        /// 失败时会尽量把已扣货币或已发商品补偿回滚。
        /// </summary>
        public ShopOperationResult ExecuteBuy(
            string shopId,
            string productId,
            string itemId,
            int itemCount,
            string targetContainerId,
            ShopPriceData[] prices,
            ShopPriceViewData[] priceViews,
            string sourceModule)
        {
            _removedItems.Clear();
            _addedItems.Clear();
            _paidPrices.Clear();

            if (_inventoryService == null)
            {
                return ShopOperationResult.Fail(
                    shopId,
                    productId,
                    ShopTransactionType.Buy,
                    ShopFailureReason.ServiceNotReady,
                    "背包服务未就绪。");
            }

            var source = string.IsNullOrWhiteSpace(sourceModule) ? SourceModuleName : sourceModule;
            Dictionary<string, int> previousItemCounts = null;
            try
            {
                if (!PayPrices(shopId, productId, prices, source, out var payFailure))
                {
                    return payFailure;
                }

                previousItemCounts = CaptureItemCounts(itemId);
                var addResult = _inventoryService.AddItem(new AddItemRequest
                {
                    ItemId = itemId,
                    Count = itemCount,
                    TargetContainerId = targetContainerId,
                    TargetSlotIndex = -1,
                    AllowPartial = false,
                    SourceModule = source
                });

                // 即使 AddItem 返回了失败，也先记录它声明的变化，方便后续做补偿。
                AppendSnapshots(_addedItems, addResult.AddedItems);
                AppendSnapshots(_addedItems, addResult.ChangedItems);

                if (!addResult.Succeeded || addResult.OverflowItems.Length > 0)
                {
                    var productRollbackFailed = !RollbackAddedProduct(itemId, itemCount, previousItemCounts, source);
                    var currencyRollbackFailed = !RollbackPaidPrices(source);
                    return ShopOperationResult.Fail(
                        shopId,
                        productId,
                        ShopTransactionType.Buy,
                        currencyRollbackFailed || productRollbackFailed
                            ? ShopFailureReason.CurrencyRollbackFailed
                            : ShopFailureReason.InventoryRejected,
                        currencyRollbackFailed || productRollbackFailed
                            ? "商品发放失败，且补偿回滚失败。"
                            : $"商品发放失败：{addResult.Message}");
                }

                return ShopOperationResult.Success(
                    shopId,
                    productId,
                    ShopTransactionType.Buy,
                    _addedItems.ToArray(),
                    _removedItems.ToArray(),
                    priceViews,
                    null,
                    "购买成功。");
            }
            catch (Exception ex)
            {
                var productRollbackFailed = !RollbackAddedProduct(itemId, itemCount, previousItemCounts, source);
                var currencyRollbackFailed = !RollbackPaidPrices(source);
                Debug.LogError($"[NiumaShop] 购买事务异常：{ex}");
                return ShopOperationResult.Fail(
                    shopId,
                    productId,
                    ShopTransactionType.Buy,
                    currencyRollbackFailed || productRollbackFailed
                        ? ShopFailureReason.CurrencyRollbackFailed
                        : ShopFailureReason.UnknownError,
                    currencyRollbackFailed || productRollbackFailed
                        ? "购买事务异常，且补偿回滚失败。"
                        : $"购买事务异常：{ex.Message}");
            }
        }

        private bool PayPrices(
            string shopId,
            string productId,
            ShopPriceData[] prices,
            string source,
            out ShopOperationResult failure)
        {
            failure = null;
            for (var i = 0; prices != null && i < prices.Length; i++)
            {
                var price = prices[i];
                if (price == null || string.IsNullOrWhiteSpace(price.CurrencyItemId) || price.Amount <= 0)
                {
                    continue;
                }

                var removeResult = _inventoryService.RemoveItem(new RemoveItemRequest
                {
                    ItemId = price.CurrencyItemId,
                    Count = price.Amount,
                    SourceModule = source
                });

                if (!removeResult.Succeeded)
                {
                    var rollbackFailed = !RollbackPaidPrices(source);
                    failure = ShopOperationResult.Fail(
                        shopId,
                        productId,
                        ShopTransactionType.Buy,
                        rollbackFailed ? ShopFailureReason.CurrencyRollbackFailed : ShopFailureReason.InsufficientCurrency,
                        rollbackFailed
                            ? "货币扣除失败，且已扣货币回滚失败。"
                            : $"货币扣除失败：{removeResult.Message}");
                    return false;
                }

                _paidPrices.Add(new ShopPriceData
                {
                    CurrencyItemId = price.CurrencyItemId,
                    Amount = price.Amount
                });
                AppendSnapshots(_removedItems, removeResult.RemovedItems);
                AppendSnapshots(_removedItems, removeResult.ChangedItems);
            }

            return true;
        }

        private bool RollbackPaidPrices(string source)
        {
            var success = true;
            for (var i = _paidPrices.Count - 1; i >= 0; i--)
            {
                var price = _paidPrices[i];
                if (price == null || string.IsNullOrWhiteSpace(price.CurrencyItemId) || price.Amount <= 0)
                {
                    continue;
                }

                var result = _inventoryService.AddItem(new AddItemRequest
                {
                    ItemId = price.CurrencyItemId,
                    Count = price.Amount,
                    AllowPartial = false,
                    SourceModule = source
                });

                success &= result.Succeeded && result.OverflowItems.Length == 0;
            }

            return success;
        }

        private bool RollbackAddedProduct(
            string itemId,
            int expectedCount,
            Dictionary<string, int> previousCounts,
            string source)
        {
            if (_addedItems.Count == 0)
            {
                return true;
            }

            var success = true;
            var remaining = Math.Max(0, expectedCount);
            for (var i = _addedItems.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var item = _addedItems[i];
                if (item == null
                    || string.IsNullOrWhiteSpace(item.InstanceId)
                    || !string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                var rollbackCount = Math.Min(remaining, Math.Max(0, item.Count));
                if (previousCounts != null && previousCounts.TryGetValue(item.InstanceId, out var oldCount))
                {
                    rollbackCount = Math.Min(rollbackCount, Math.Max(0, item.Count - oldCount));
                }

                if (rollbackCount <= 0)
                {
                    continue;
                }

                var result = _inventoryService.RemoveItem(new RemoveItemRequest
                {
                    InstanceId = item.InstanceId,
                    Count = rollbackCount,
                    SourceModule = source
                });

                success &= result.Succeeded;
                if (result.Succeeded)
                {
                    remaining -= rollbackCount;
                }
            }

            return success && remaining <= 0;
        }

        private Dictionary<string, int> CaptureItemCounts(string itemId)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(itemId) || _inventoryService == null)
            {
                return result;
            }

            _inventoryService.CopyItemSnapshots(_itemSnapshotBuffer);
            for (var i = 0; i < _itemSnapshotBuffer.Count; i++)
            {
                var item = _itemSnapshotBuffer[i];
                if (item == null
                    || string.IsNullOrWhiteSpace(item.InstanceId)
                    || !string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                result[item.InstanceId] = Math.Max(0, item.Count);
            }

            return result;
        }

        private static void AppendSnapshots(List<InventoryItemSnapshot> target, InventoryItemSnapshot[] source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    target.Add(source[i]);
                }
            }
        }
    }
}
