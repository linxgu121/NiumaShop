using System;

namespace NiumaShop.Request
{
    /// <summary>
    /// 设置商店当前激活折扣的请求。
    /// DiscountIds 为 null、空数组或全空白数组时，表示显式关闭该商店当前所有折扣。
    /// 如需恢复配置默认折扣，请使用 ResetShopDiscountsRequest。
    /// </summary>
    public readonly struct SetShopDiscountsRequest
    {
        public readonly string ShopId;
        public readonly string[] DiscountIds;
        public readonly string SourceModule;

        public SetShopDiscountsRequest(
            string shopId,
            string[] discountIds,
            string sourceModule = null)
        {
            ShopId = shopId;
            DiscountIds = discountIds ?? Array.Empty<string>();
            SourceModule = sourceModule;
        }
    }
}
