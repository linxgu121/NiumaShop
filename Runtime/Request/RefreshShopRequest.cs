namespace NiumaShop.Request
{
    /// <summary>
    /// 刷新商店请求。
    /// </summary>
    public readonly struct RefreshShopRequest
    {
        public readonly string ShopId;
        public readonly string SourceModule;

        public RefreshShopRequest(string shopId, string sourceModule = null)
        {
            ShopId = shopId;
            SourceModule = sourceModule;
        }
    }
}
