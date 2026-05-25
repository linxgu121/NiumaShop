using NiumaShop.Request;
using NiumaShop.Result;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店命令接口。
    /// 所有方法都可能修改商店或背包状态，成功后应由实现层递增 Revision。
    /// </summary>
    public interface IShopCommand
    {
        /// <summary>
        /// 购买商品。
        /// 流程为：校验 -> 扣除货币 -> 发放商品 -> 更新库存/限购。
        /// </summary>
        ShopOperationResult BuyProduct(in BuyProductRequest request);

        /// <summary>
        /// 出售物品。
        /// 第一版暂不实现，后续开放普通材料/消耗品出售时再补。
        /// </summary>
        ShopOperationResult SellItem(in SellItemRequest request);

        /// <summary>
        /// 解锁或锁定商店。
        /// </summary>
        bool SetShopUnlocked(in UnlockShopRequest request);

        /// <summary>
        /// 解锁或锁定商品。
        /// </summary>
        bool SetProductUnlocked(in UnlockShopProductRequest request);

        /// <summary>
        /// 刷新商店库存和商品运行时状态。
        /// 第一版只恢复静态默认值，后续可接时间、剧情或活动刷新规则。
        /// </summary>
        bool RefreshShop(in RefreshShopRequest request);
    }
}
