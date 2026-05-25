using System;
using NiumaShop.Enum;

namespace NiumaShop.Data
{
    /// <summary>
    /// 单个商店运行时进度快照。
    /// 用于存档导出、读档恢复和调试显示。
    /// </summary>
    [Serializable]
    public sealed class ShopProgressSnapshot
    {
        /// <summary>
        /// 商店稳定 ID。
        /// </summary>
        public string ShopId;

        /// <summary>
        /// 商店运行时状态。
        /// </summary>
        public ShopState State;

        /// <summary>
        /// 单商店修订号。
        /// 库存、限购、解锁、折扣、刷新状态变化时递增。
        /// </summary>
        public long Revision;

        /// <summary>
        /// 商品运行时快照。
        /// </summary>
        public ShopProductProgressSnapshot[] Products = Array.Empty<ShopProductProgressSnapshot>();

        /// <summary>
        /// 当前激活折扣 ID。
        /// </summary>
        public string[] ActiveDiscountIds = Array.Empty<string>();

        /// <summary>
        /// 最后刷新时间，UTC Unix 秒。
        /// 第一版仅作为数据预留，不依赖时间模块。
        /// </summary>
        public long LastRefreshUnixSeconds;
    }
}
