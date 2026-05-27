using System;
using NiumaShop.Enum;
using UnityEngine;

namespace NiumaShop.Config
{
    /// <summary>
    /// 商店静态配置资源。
    /// 只描述商店可卖什么、初始规则是什么，不保存玩家运行时购买进度。
    /// </summary>
    [CreateAssetMenu(fileName = "ShopAsset", menuName = "Niuma/Shop/Shop Asset")]
    public sealed class ShopAsset : ScriptableObject
    {
        [Tooltip("商店稳定 ID。正式内容必须填写，不能依赖资源文件名。")]
        public string ShopId;

        [Tooltip("商店显示名称。")]
        public string DisplayName;

        [TextArea]
        [Tooltip("商店描述。用于 UI、调试或策划备注。")]
        public string Description;

        [Tooltip("商店类型。主要用于 UI 分类和配置校验，不直接决定交易逻辑。")]
        public ShopType ShopType = ShopType.General;

        [Tooltip("商店是否默认开放。运行时可被剧情、任务或调试命令改变。")]
        public bool DefaultUnlocked = true;

        [Tooltip("商店刷新方式。第一版只实现 None / Manual / StorySignal / QuestSignal 的数据约定。")]
        public ShopRefreshMode RefreshMode = ShopRefreshMode.None;

        [Tooltip("商品列表。同一个商店内 ProductId 不允许重复。")]
        public ShopProductData[] Products = Array.Empty<ShopProductData>();

        [Tooltip("打开商店需要满足的条件。")]
        public ShopConditionData[] OpenConditions = Array.Empty<ShopConditionData>();

        [Tooltip("默认折扣列表。ShopService 第一版会按条件取最低折扣价；复杂活动价可由 IShopPriceResolver 覆盖。")]
        public ShopDiscountData[] DefaultDiscounts = Array.Empty<ShopDiscountData>();
    }
}
