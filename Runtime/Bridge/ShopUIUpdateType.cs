namespace NiumaShop.Bridge
{
    /// <summary>
    /// 商店 UI 更新类型。
    /// </summary>
    public enum ShopUIUpdateType
    {
        /// <summary>
        /// 未指定更新类型，仅用于默认值保护。
        /// </summary>
        None = 0,

        /// <summary>
        /// 刷新商店面板数据。
        /// </summary>
        Refresh = 1,

        /// <summary>
        /// 清空商店面板数据。
        /// </summary>
        Cleared = 2,

        /// <summary>
        /// 商店交易或命令执行结果。
        /// </summary>
        Result = 3
    }
}
