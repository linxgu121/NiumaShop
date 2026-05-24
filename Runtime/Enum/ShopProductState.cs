namespace NiumaShop.Enum
{
    /// <summary>
    /// 商品运行时状态。
    /// </summary>
    public enum ShopProductState
    {
        /// <summary>
        /// 未定义状态。默认值必须视为无效。
        /// </summary>
        None = 0,

        /// <summary>
        /// 商品未解锁，不应允许购买。
        /// </summary>
        Locked = 10,

        /// <summary>
        /// 商品已解锁，可进入购买校验流程。
        /// </summary>
        Unlocked = 20,

        /// <summary>
        /// 商品已售罄。
        /// OneShot 商品首次购买成功后必须进入该状态。
        /// </summary>
        SoldOut = 30,

        /// <summary>
        /// 商品被临时隐藏。用于剧情、活动、调试等短期控制。
        /// </summary>
        Hidden = 40
    }
}
