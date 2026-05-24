namespace NiumaShop.Request
{
    /// <summary>
    /// 解锁或锁定商店请求。
    /// </summary>
    public readonly struct UnlockShopRequest
    {
        public readonly string ShopId;
        public readonly bool Unlock;
        public readonly string SourceModule;

        public UnlockShopRequest(string shopId, bool unlock, string sourceModule = null)
        {
            ShopId = shopId;
            Unlock = unlock;
            SourceModule = sourceModule;
        }
    }
}
