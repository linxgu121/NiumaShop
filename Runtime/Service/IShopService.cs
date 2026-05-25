using System.Collections.Generic;
using NiumaShop.Data;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店服务门面接口。
    /// 第一版作为查询、命令和持久化的统一入口；后续接口膨胀时可拆能力接口。
    /// </summary>
    public interface IShopService : IShopQuery, IShopCommand
    {
        /// <summary>
        /// 显式导出商店存档快照。
        /// 不允许外部直接序列化运行时对象。
        /// </summary>
        ShopProgressSnapshot[] ExportSnapshots();

        /// <summary>
        /// 复制商店快照到调用方缓存列表。
        /// UI、调试面板等展示场景优先使用查询 / ViewData，不要为了刷新界面调用 ExportSnapshots。
        /// </summary>
        void CopyShopSnapshots(List<ShopProgressSnapshot> output);

        /// <summary>
        /// 从存档快照恢复商店运行时状态。
        /// </summary>
        void ImportSnapshots(IEnumerable<ShopProgressSnapshot> snapshots);
    }
}
