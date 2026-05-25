using System;
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

        /// <summary>
        /// 商品类型表现 Key。
        /// 由服务层从背包物品定义或商店配置映射而来，ViewData 不直接暴露 Inventory 枚举。
        /// </summary>
        public string ItemTypeKey;

        /// <summary>
        /// 商品品质表现 Key。
        /// UI 可根据该 Key 决定颜色、边框和排序，不直接依赖背包模块枚举。
        /// </summary>
        public string QualityKey;

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
