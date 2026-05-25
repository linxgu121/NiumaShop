using NiumaShop.Config;
using NiumaShop.Request;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店外部价格解析接口。
    /// 第一版 ShopService 会使用商品基础价格；特殊折扣、活动价、声望价可由外部解析器覆盖。
    /// </summary>
    public interface IShopPriceResolver
    {
        /// <summary>
        /// 尝试解析本次购买价格。
        /// 返回 false 表示不接管价格，ShopService 回退到商品基础价格。
        /// 返回 true 时 prices 必须是已经按购买份数折算后的价格数组。
        /// </summary>
        bool TryResolveBuyPrices(
            ShopAsset shop,
            ShopProductData product,
            in BuyProductRequest request,
            out ShopPriceData[] prices,
            out string message);
    }
}
