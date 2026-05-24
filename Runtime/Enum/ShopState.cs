namespace NiumaShop.Enum
{
    /// <summary>
    /// 商店运行时状态。
    /// 只表示商店是否可访问，不表示具体商品是否可购买。
    /// </summary>
    public enum ShopState
    {
        /// <summary>
        /// 未定义状态。默认值必须视为无效，服务层需要显式防御。
        /// </summary>
        None = 0,

        /// <summary>
        /// 商店已锁定，玩家无法打开。
        /// </summary>
        Locked = 10,

        /// <summary>
        /// 商店已开放，玩家可以查看商品。
        /// </summary>
        Unlocked = 20,

        /// <summary>
        /// 商店临时关闭。用于剧情、活动、场景状态等短期封锁。
        /// </summary>
        Closed = 30
    }
}
