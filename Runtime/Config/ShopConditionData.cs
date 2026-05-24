using System;
using NiumaShop.Enum;
using UnityEngine;

namespace NiumaShop.Config
{
    /// <summary>
    /// 商店或商品条件配置。
    /// 通用商店只使用简单条件；特殊商店、活动商店、声望商店可通过外部解析器读取扩展参数。
    /// </summary>
    [Serializable]
    public sealed class ShopConditionData
    {
        [Tooltip("条件类型。None 表示无条件；复杂条件由 IShopConditionResolver 解析。")]
        public ShopConditionType ConditionType = ShopConditionType.None;

        [Tooltip("条件目标 ID。例如 ItemId、QuestId、StoryFlagId、ShopId 或 ProductId。")]
        public string TargetId;

        [Tooltip("需求数量。HasItem 时表示需要持有多少个目标物品。")]
        public int RequiredCount = 1;

        [Tooltip("整数范围最小值。用于声望区间、玩家等级区间等特殊条件。")]
        public int MinValue;

        [Tooltip("整数范围最大值。用于声望区间、玩家等级区间等特殊条件。0 表示未配置时由解析器自行决定。")]
        public int MaxValue;

        [Tooltip("UTC Unix 秒级开始时间。用于限时商店或活动条件。0 表示不限制开始时间。")]
        public long StartUnixSeconds;

        [Tooltip("UTC Unix 秒级结束时间。用于限时商店或活动条件。0 表示不限制结束时间。")]
        public long EndUnixSeconds;

        [Tooltip("扩展参数。只给特殊条件使用，普通商店不建议填写。")]
        public ShopConditionParameterData[] Parameters = Array.Empty<ShopConditionParameterData>();

        [Tooltip("是否反转条件结果。开启后满足变为不满足，不满足变为满足。")]
        public bool Invert;
    }
}
