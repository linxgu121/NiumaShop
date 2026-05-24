namespace NiumaShop.Enum
{
    /// <summary>
    /// 商店交易类型。
    /// </summary>
    public enum ShopTransactionType
    {
        /// <summary>
        /// 无交易类型。
        /// </summary>
        None = 0,

        /// <summary>
        /// 玩家购买商品。
        /// </summary>
        Buy = 10,

        /// <summary>
        /// 玩家出售物品。第一版作为后续阶段预留。
        /// </summary>
        Sell = 20,

        /// <summary>
        /// 刷新商店库存或商品状态。
        /// </summary>
        Refresh = 30,

        /// <summary>
        /// 解锁商店或商品。
        /// </summary>
        Unlock = 40,

        /// <summary>
        /// 锁定商店或商品。
        /// </summary>
        Lock = 50
    }
}
