namespace NiumaShop.Enum
{
    /// <summary>
    /// 商店刷新方式。
    /// 第一版不强依赖时间模块，时间类刷新仅预留枚举。
    /// </summary>
    public enum ShopRefreshMode
    {
        /// <summary>
        /// 不刷新。
        /// </summary>
        None = 0,

        /// <summary>
        /// 外部明确调用 RefreshShop 时刷新。
        /// </summary>
        Manual = 10,

        /// <summary>
        /// 剧情信号触发刷新。
        /// </summary>
        StorySignal = 20,

        /// <summary>
        /// 任务信号触发刷新。
        /// </summary>
        QuestSignal = 30,

        /// <summary>
        /// 每日刷新。后续接入时间模块后实现。
        /// </summary>
        Daily = 100,

        /// <summary>
        /// 每周刷新。后续接入时间模块后实现。
        /// </summary>
        Weekly = 110,

        /// <summary>
        /// 季节刷新。后续接入时间模块后实现。
        /// </summary>
        Season = 120,

        /// <summary>
        /// 游戏内日期刷新。后续接入时间模块后实现。
        /// </summary>
        GameDay = 130
    }
}
