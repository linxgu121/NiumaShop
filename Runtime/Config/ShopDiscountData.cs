using System;
using UnityEngine;

namespace NiumaShop.Config
{
    /// <summary>
    /// 商店折扣配置。
    /// 第一版只冻结数据结构，不实现复杂折扣叠加。
    /// </summary>
    [Serializable]
    public sealed class ShopDiscountData
    {
        [Tooltip("折扣唯一 ID。用于存档、调试和剧情/任务切换折扣。")]
        public string DiscountId;

        [Tooltip("价格乘数。1 表示原价，0.8 表示八折，0 表示免费。")]
        public float PriceMultiplier = 1f;

        [Tooltip("适用的商品标签。为空表示不按标签筛选。")]
        public string[] ProductTags = Array.Empty<string>();

        [Tooltip("适用的商品 ID。为空表示不按商品 ID 筛选。")]
        public string[] ProductIds = Array.Empty<string>();

        [Tooltip("折扣生效条件。")]
        public ShopConditionData[] Conditions = Array.Empty<ShopConditionData>();
    }
}
