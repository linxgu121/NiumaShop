namespace NiumaShop.Enum
{
    /// <summary>
    /// 商店操作失败原因。
    /// UI 必须基于该枚举做提示，不要通过字符串匹配 Message。
    /// </summary>
    public enum ShopFailureReason
    {
        /// <summary>
        /// 无失败。
        /// </summary>
        None = 0,

        /// <summary>
        /// 商店服务尚未准备好。
        /// </summary>
        ServiceNotReady = 10,

        /// <summary>
        /// 找不到商店配置或运行时状态。
        /// </summary>
        ShopNotFound = 20,

        /// <summary>
        /// 商店未解锁或被关闭。
        /// </summary>
        ShopLocked = 30,

        /// <summary>
        /// 找不到商品配置或运行时状态。
        /// </summary>
        ProductNotFound = 40,

        /// <summary>
        /// 商品未解锁或被隐藏。
        /// </summary>
        ProductLocked = 50,

        /// <summary>
        /// 商品售罄。
        /// </summary>
        SoldOut = 60,

        /// <summary>
        /// 达到限购次数。
        /// </summary>
        PurchaseLimitReached = 70,

        /// <summary>
        /// 商店或商品条件未满足。
        /// </summary>
        ConditionNotMet = 80,

        /// <summary>
        /// 请求参数非法。
        /// </summary>
        InvalidRequest = 90,

        /// <summary>
        /// 商品或货币对应的物品定义缺失。
        /// </summary>
        MissingItemDefinition = 100,

        /// <summary>
        /// 玩家货币不足。
        /// </summary>
        InsufficientCurrency = 110,

        /// <summary>
        /// 背包拒绝交易，例如空间不足、重量不足、唯一物品重复等。
        /// </summary>
        InventoryRejected = 120,

        /// <summary>
        /// 商品发放失败后，货币回滚也失败。此时玩家资产可能不一致。
        /// </summary>
        CurrencyRollbackFailed = 130,

        /// <summary>
        /// 出售发放货币失败后，物品回滚也失败。此时玩家资产可能不一致。
        /// </summary>
        ProductRollbackFailed = 140,

        /// <summary>
        /// 外部条件、价格或业务解析器拒绝本次交易。
        /// </summary>
        ExternalRejected = 150,

        /// <summary>
        /// 同一交易正在执行，拒绝重复进入。
        /// </summary>
        TransactionInProgress = 160,

        /// <summary>
        /// 未知异常。只用于兜底，不应表达正常业务失败。
        /// </summary>
        UnknownError = 999
    }
}
