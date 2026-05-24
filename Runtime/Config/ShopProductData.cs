using System;
using UnityEngine;

namespace NiumaShop.Config
{
    /// <summary>
    /// 商品静态配置。
    /// ProductId 是商品条目 ID，ItemId 是实际发放的背包物品 ID，两者不能混用。
    /// </summary>
    [Serializable]
    public sealed class ShopProductData
    {
        [Tooltip("商品条目 ID。同一个 ItemId 可以配置为多个不同商品，因此 ProductId 不应等于 ItemId。")]
        public string ProductId;

        [Tooltip("购买成功后发放的物品 ID。必须对应 NiumaInventory 的 ItemDefinition。")]
        public string ItemId;

        [Tooltip("每购买 1 份商品时发放的物品数量。必须大于 0。")]
        public int Count = 1;

        [Tooltip("商品价格。支持多货币；免费商品也应显式配置 Amount=0。")]
        public ShopPriceData[] Prices = Array.Empty<ShopPriceData>();

        [Tooltip("初始库存。-1 表示无限库存，0 表示开局售罄，大于 0 表示有限库存。")]
        public int InitialStock = -1;

        [Tooltip("每个玩家最大购买份数。-1 表示不限购。")]
        public int MaxPurchaseCount = -1;

        [Tooltip("是否默认解锁。运行时仍可被剧情、任务或调试命令锁定/解锁。")]
        public bool DefaultUnlocked = true;

        [Tooltip("是否为一次性商品。首次购买成功后 ProductId 永久售罄，与 InitialStock 无关。")]
        public bool OneShot;

        [Tooltip("商品标签。用于筛选、折扣、UI 分类和特殊商店规则。建议统一小写。")]
        public string[] Tags = Array.Empty<string>();

        [Tooltip("购买条件。普通商店尽量只使用简单条件，复杂条件交给外部解析器。")]
        public ShopConditionData[] BuyConditions = Array.Empty<ShopConditionData>();
    }
}
