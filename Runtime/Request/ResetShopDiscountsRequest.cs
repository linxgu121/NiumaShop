namespace NiumaShop.Request
{
    /// <summary>
    /// 恢复商店默认折扣的请求。
    /// 该请求会清空运行时显式折扣状态，让商店重新按 ShopAsset.DefaultDiscounts 计算价格。
    /// </summary>
    public readonly struct ResetShopDiscountsRequest
    {
        public readonly string ShopId;
        public readonly string SourceModule;

        public ResetShopDiscountsRequest(string shopId, string sourceModule = null)
        {
            ShopId = shopId;
            SourceModule = sourceModule;
        }
    }
}
