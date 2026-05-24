using System;
using NiumaInventory.Enum;
using NiumaShop.Enum;

namespace NiumaShop.ViewData
{
    /// <summary>
    /// 商品 UI 表现数据。
    /// UI 只读取该对象，不直接修改商店运行时状态。
    /// </summary>
    [Serializable]
    public sealed class ShopProductViewData
    {
        public string ShopId;
        public string ProductId;
        public string ItemId;
        public string DisplayName;
        public string Description;
        public string IconAddress;
        public ItemType ItemType;
        public ItemQuality Quality;
        public int Count;
        public ShopPriceViewData[] Prices = Array.Empty<ShopPriceViewData>();
        public int RemainingStock;
        public int PurchasedCount;
        public int MaxPurchaseCount;
        public int MaxPurchasableCount;
        public bool IsUnlocked;
        public bool IsSoldOut;
        public bool CanBuy;
        public ShopFailureReason[] CannotBuyReasons = Array.Empty<ShopFailureReason>();
    }
}
