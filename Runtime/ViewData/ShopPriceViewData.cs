using System;

namespace NiumaShop.ViewData
{
    /// <summary>
    /// 商品价格 UI 表现数据。
    /// </summary>
    [Serializable]
    public sealed class ShopPriceViewData
    {
        /// <summary>
        /// 货币物品 ID。
        /// </summary>
        public string CurrencyItemId;

        /// <summary>
        /// 货币显示名称。
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 货币图标地址。后续可直接接 Addressables Key。
        /// </summary>
        public string IconAddress;

        /// <summary>
        /// 需要支付或获得的数量。
        /// </summary>
        public int Amount;

        /// <summary>
        /// 未应用折扣或涨价前的原始数量。
        /// UI 可用它显示划线原价；等于 Amount 时表示没有价格修正。
        /// </summary>
        public int OriginalAmount;

        /// <summary>
        /// 是否应用了折扣或涨价倍率。
        /// </summary>
        public bool HasPriceModifier;

        /// <summary>
        /// 当前命中的折扣 ID。
        /// 为空表示没有折扣，或价格由外部 IShopPriceResolver 完全接管。
        /// </summary>
        public string AppliedDiscountId;

        /// <summary>
        /// 实际使用的价格倍率。
        /// 1 表示原价，小于 1 表示打折，大于 1 表示涨价。
        /// </summary>
        public float PriceMultiplier = 1f;

        /// <summary>
        /// 玩家当前拥有数量。
        /// </summary>
        public int PlayerOwnedCount;

        /// <summary>
        /// 玩家当前数量是否足够支付。
        /// </summary>
        public bool IsEnough;
    }
}
