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
        /// 玩家当前拥有数量。
        /// </summary>
        public int PlayerOwnedCount;

        /// <summary>
        /// 玩家当前数量是否足够支付。
        /// </summary>
        public bool IsEnough;
    }
}
