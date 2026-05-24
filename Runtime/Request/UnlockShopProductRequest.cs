namespace NiumaShop.Request
{
    /// <summary>
    /// 解锁或锁定商店商品请求。
    /// </summary>
    public readonly struct UnlockShopProductRequest
    {
        public readonly string ShopId;
        public readonly string ProductId;
        public readonly bool Unlock;
        public readonly string SourceModule;

        public UnlockShopProductRequest(
            string shopId,
            string productId,
            bool unlock,
            string sourceModule = null)
        {
            ShopId = shopId;
            ProductId = productId;
            Unlock = unlock;
            SourceModule = sourceModule;
        }
    }
}
