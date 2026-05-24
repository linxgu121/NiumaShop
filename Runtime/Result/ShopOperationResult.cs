using System;
using NiumaInventory.Data;
using NiumaShop.Enum;
using NiumaShop.ViewData;

namespace NiumaShop.Result
{
    /// <summary>
    /// 商店操作结果。
    /// 购买、出售、刷新、解锁等命令都应返回结构化结果，避免 UI 解析字符串。
    /// </summary>
    public sealed class ShopOperationResult
    {
        /// <summary>
        /// 本次操作是否成功。
        /// </summary>
        public bool Succeeded;

        /// <summary>
        /// 主要失败原因。用于日志标题或按钮主提示。
        /// </summary>
        public ShopFailureReason PrimaryFailureReason;

        /// <summary>
        /// 全部失败原因。用于 UI 展示完整置灰条件。
        /// </summary>
        public ShopFailureReason[] FailureReasons = Array.Empty<ShopFailureReason>();

        /// <summary>
        /// 关联商店 ID。
        /// </summary>
        public string ShopId;

        /// <summary>
        /// 关联商品 ID。
        /// </summary>
        public string ProductId;

        /// <summary>
        /// 本次交易类型。
        /// </summary>
        public ShopTransactionType TransactionType;

        /// <summary>
        /// 玩家支付的价格。
        /// </summary>
        public ShopPriceViewData[] PaidPrices = Array.Empty<ShopPriceViewData>();

        /// <summary>
        /// 玩家获得的价格或回收收益。
        /// </summary>
        public ShopPriceViewData[] ReceivedPrices = Array.Empty<ShopPriceViewData>();

        /// <summary>
        /// 本次操作新增的背包物品实例。
        /// </summary>
        public InventoryItemSnapshot[] AddedItems = Array.Empty<InventoryItemSnapshot>();

        /// <summary>
        /// 本次操作移除或减少的背包物品实例。
        /// </summary>
        public InventoryItemSnapshot[] RemovedItems = Array.Empty<InventoryItemSnapshot>();

        /// <summary>
        /// 调试或临时提示文本。正式 UI 不应依赖该字段做本地化。
        /// </summary>
        public string Message;

        /// <summary>
        /// 是否包含指定失败原因。
        /// </summary>
        public bool HasFailure(ShopFailureReason reason)
        {
            for (var i = 0; FailureReasons != null && i < FailureReasons.Length; i++)
            {
                if (FailureReasons[i] == reason)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static ShopOperationResult Success(
            string shopId,
            string productId,
            ShopTransactionType transactionType,
            InventoryItemSnapshot[] addedItems = null,
            InventoryItemSnapshot[] removedItems = null,
            ShopPriceViewData[] paidPrices = null,
            ShopPriceViewData[] receivedPrices = null,
            string message = null)
        {
            return new ShopOperationResult
            {
                Succeeded = true,
                PrimaryFailureReason = ShopFailureReason.None,
                FailureReasons = Array.Empty<ShopFailureReason>(),
                ShopId = shopId,
                ProductId = productId,
                TransactionType = transactionType,
                AddedItems = addedItems ?? Array.Empty<InventoryItemSnapshot>(),
                RemovedItems = removedItems ?? Array.Empty<InventoryItemSnapshot>(),
                PaidPrices = paidPrices ?? Array.Empty<ShopPriceViewData>(),
                ReceivedPrices = receivedPrices ?? Array.Empty<ShopPriceViewData>(),
                Message = message
            };
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static ShopOperationResult Fail(
            string shopId,
            string productId,
            ShopTransactionType transactionType,
            ShopFailureReason primaryReason,
            string message = null,
            params ShopFailureReason[] additionalReasons)
        {
            return new ShopOperationResult
            {
                Succeeded = false,
                PrimaryFailureReason = primaryReason,
                FailureReasons = BuildFailureReasons(primaryReason, additionalReasons),
                ShopId = shopId,
                ProductId = productId,
                TransactionType = transactionType,
                Message = message
            };
        }

        private static ShopFailureReason[] BuildFailureReasons(
            ShopFailureReason primaryReason,
            ShopFailureReason[] additionalReasons)
        {
            if (primaryReason == ShopFailureReason.None
                && (additionalReasons == null || additionalReasons.Length == 0))
            {
                return Array.Empty<ShopFailureReason>();
            }

            var capacity = 1 + (additionalReasons?.Length ?? 0);
            var result = new ShopFailureReason[capacity];
            var count = 0;

            if (primaryReason != ShopFailureReason.None)
            {
                result[count++] = primaryReason;
            }

            for (var i = 0; additionalReasons != null && i < additionalReasons.Length; i++)
            {
                var reason = additionalReasons[i];
                if (reason == ShopFailureReason.None || Contains(result, count, reason))
                {
                    continue;
                }

                result[count++] = reason;
            }

            if (count != result.Length)
            {
                Array.Resize(ref result, count);
            }

            return result;
        }

        private static bool Contains(ShopFailureReason[] reasons, int count, ShopFailureReason reason)
        {
            for (var i = 0; i < count; i++)
            {
                if (reasons[i] == reason)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
