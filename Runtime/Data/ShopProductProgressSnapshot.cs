using System;
using NiumaShop.Enum;

namespace NiumaShop.Data
{
    /// <summary>
    /// 商品运行时进度快照。
    /// 只保存玩家事实，不保存 ItemId、价格、名称等静态配置。
    /// </summary>
    [Serializable]
    public sealed class ShopProductProgressSnapshot
    {
        /// <summary>
        /// 商品条目 ID。
        /// </summary>
        public string ProductId;

        /// <summary>
        /// 商品运行时状态。
        /// </summary>
        public ShopProductState State;

        /// <summary>
        /// 剩余库存。-1 表示无限库存。
        /// </summary>
        public int RemainingStock = -1;

        /// <summary>
        /// 当前玩家已购买份数。
        /// </summary>
        public int PurchasedCount;
    }
}
