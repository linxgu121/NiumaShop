namespace NiumaShop.Request
{
    /// <summary>
    /// 出售物品请求。
    /// 第一阶段先冻结协议，出售功能在后续阶段实现。
    /// </summary>
    public readonly struct SellItemRequest
    {
        public readonly string ShopId;
        public readonly string InventoryInstanceId;
        public readonly int Count;
        public readonly string SourceModule;

        public SellItemRequest(
            string shopId,
            string inventoryInstanceId,
            int count,
            string sourceModule = null)
        {
            ShopId = shopId;
            InventoryInstanceId = inventoryInstanceId;
            Count = count;
            SourceModule = sourceModule;
        }
    }
}
