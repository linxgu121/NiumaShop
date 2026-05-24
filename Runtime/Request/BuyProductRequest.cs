namespace NiumaShop.Request
{
    /// <summary>
    /// 购买商品请求。
    /// Count 表示购买几份商品，不是最终获得的物品数量。
    /// </summary>
    public readonly struct BuyProductRequest
    {
        public readonly string ShopId;
        public readonly string ProductId;
        public readonly int Count;
        public readonly string TargetContainerId;
        public readonly string SourceModule;

        public BuyProductRequest(
            string shopId,
            string productId,
            int count,
            string targetContainerId = null,
            string sourceModule = null)
        {
            ShopId = shopId;
            ProductId = productId;
            Count = count;
            TargetContainerId = targetContainerId;
            SourceModule = sourceModule;
        }
    }
}
