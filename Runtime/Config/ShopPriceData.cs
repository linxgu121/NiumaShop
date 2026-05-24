using System;
using UnityEngine;

namespace NiumaShop.Config
{
    /// <summary>
    /// 商品价格配置。
    /// 第一版使用背包中的货币物品作为价格来源。
    /// </summary>
    [Serializable]
    public sealed class ShopPriceData
    {
        [Tooltip("货币物品 ID。必须对应 NiumaInventory 的 ItemDefinition。")]
        public string CurrencyItemId;

        [Tooltip("需要消耗的货币数量。0 表示免费商品，但必须由配置显式写出。")]
        public int Amount;
    }
}
