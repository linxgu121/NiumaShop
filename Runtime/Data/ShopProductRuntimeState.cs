using NiumaShop.Enum;

namespace NiumaShop.Data
{
    /// <summary>
    /// 商品运行时状态。
    /// 该对象留在内存中使用，存档时必须显式转换为 ShopProductProgressSnapshot。
    /// </summary>
    public sealed class ShopProductRuntimeState
    {
        /// <summary>
        /// 商品条目 ID。
        /// </summary>
        public string ProductId;

        /// <summary>
        /// 商品运行时状态。
        /// </summary>
        public ShopProductState State = ShopProductState.Unlocked;

        /// <summary>
        /// 剩余库存。-1 表示无限库存。
        /// </summary>
        public int RemainingStock = -1;

        /// <summary>
        /// 当前玩家已购买份数。
        /// </summary>
        public int PurchasedCount;

        /// <summary>
        /// 转换为存档快照。
        /// </summary>
        public ShopProductProgressSnapshot ToSnapshot()
        {
            return new ShopProductProgressSnapshot
            {
                ProductId = ProductId,
                State = State,
                RemainingStock = RemainingStock,
                PurchasedCount = PurchasedCount
            };
        }

        /// <summary>
        /// 从存档快照恢复运行时状态。
        /// </summary>
        public static ShopProductRuntimeState FromSnapshot(ShopProductProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new ShopProductRuntimeState
            {
                ProductId = snapshot.ProductId,
                State = snapshot.State,
                RemainingStock = snapshot.RemainingStock,
                PurchasedCount = snapshot.PurchasedCount
            };
        }
    }
}
