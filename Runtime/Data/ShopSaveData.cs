using System;

namespace NiumaShop.Data
{
    /// <summary>
    /// 商店模块存档数据。
    /// </summary>
    [Serializable]
    public sealed class ShopSaveData
    {
        /// <summary>
        /// 当前存档结构版本。
        /// </summary>
        public int Version = 1;

        /// <summary>
        /// 模块全局修订号。
        /// 主要用于存档脏标记、调试和总览；普通商店 UI 应优先使用单商店 Revision。
        /// </summary>
        public int Revision;

        /// <summary>
        /// 所有已产生运行时事实的商店快照。
        /// </summary>
        public ShopProgressSnapshot[] Shops = Array.Empty<ShopProgressSnapshot>();
    }
}
