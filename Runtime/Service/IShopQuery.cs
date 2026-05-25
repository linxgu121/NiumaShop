using NiumaShop.Data;
using NiumaShop.Request;
using NiumaShop.Result;
using NiumaShop.ViewData;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店只读查询接口。
    /// UI、剧情、任务等只需要读取商店状态的模块优先依赖该接口。
    /// </summary>
    public interface IShopQuery
    {
        /// <summary>
        /// 商店模块全局修订号。
        /// 用于总览、调试或全部商店列表；普通商店 UI 应优先使用单商店 Revision。
        /// </summary>
        long Revision { get; }

        /// <summary>
        /// 最近一次导入或配置同步产生的迁移警告。
        /// 主要用于调试旧存档和配置变更，不作为正式 UI 文案。
        /// </summary>
        string[] LastMigrationWarnings { get; }

        /// <summary>
        /// 获取指定商店的单商店修订号。
        /// </summary>
        long GetRevision(string shopId);

        /// <summary>
        /// 尝试获取指定商店的运行时快照。
        /// 返回快照而不是内部运行时引用，避免外部直接修改商店状态。
        /// </summary>
        bool TryGetShopState(string shopId, out ShopProgressSnapshot snapshot);

        /// <summary>
        /// 构建指定商店的 UI 表现数据。
        /// 这是商店 UI 的主数据源，不应通过 ExportSnapshots 再拼 UI。
        /// </summary>
        ShopPanelViewData BuildShopViewData(string shopId);

        /// <summary>
        /// 校验是否可以购买商品。
        /// 只做校验，不扣货币、不发商品、不修改商店状态。
        /// </summary>
        bool CanBuy(in BuyProductRequest request, out ShopOperationResult result);

        /// <summary>
        /// 校验是否可以出售物品。
        /// 第一版出售功能未开放，默认返回失败。
        /// </summary>
        bool CanSell(in SellItemRequest request, out ShopOperationResult result);
    }
}
