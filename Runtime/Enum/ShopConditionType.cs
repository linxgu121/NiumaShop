namespace NiumaShop.Enum
{
    /// <summary>
    /// 商店条件类型。
    /// 通用条件由 ShopService 处理，复杂条件交给 IShopConditionResolver。
    /// </summary>
    public enum ShopConditionType
    {
        /// <summary>
        /// 无条件。
        /// </summary>
        None = 0,

        /// <summary>
        /// 指定商店已解锁。
        /// </summary>
        ShopUnlocked = 10,

        /// <summary>
        /// 指定商品已解锁。
        /// </summary>
        ProductUnlocked = 20,

        /// <summary>
        /// 玩家拥有指定物品。
        /// </summary>
        HasItem = 30,

        /// <summary>
        /// 指定任务已完成。由外部条件解析器处理。
        /// </summary>
        QuestCompleted = 100,

        /// <summary>
        /// 指定剧情 Flag 满足。由外部条件解析器处理。
        /// </summary>
        StoryFlag = 110,

        /// <summary>
        /// 声望达到指定值。由外部条件解析器处理。
        /// </summary>
        ReputationReached = 120,

        /// <summary>
        /// 声望处于指定区间。由外部条件解析器处理。
        /// </summary>
        ReputationRange = 130,

        /// <summary>
        /// 玩家等级处于指定区间。由外部条件解析器处理。
        /// </summary>
        PlayerLevelRange = 140,

        /// <summary>
        /// 当前时间处于指定区间。由外部条件解析器处理。
        /// </summary>
        TimeRange = 150,

        /// <summary>
        /// 完全交给外部解析器的自定义条件。
        /// </summary>
        External = 900
    }
}
