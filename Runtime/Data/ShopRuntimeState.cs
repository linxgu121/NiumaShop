using System;
using NiumaShop.Enum;

namespace NiumaShop.Data
{
    /// <summary>
    /// 单个商店运行时状态。
    /// </summary>
    public sealed class ShopRuntimeState
    {
        /// <summary>
        /// 商店稳定 ID。
        /// </summary>
        public string ShopId;

        /// <summary>
        /// 商店运行时状态。
        /// </summary>
        public ShopState State = ShopState.Unlocked;

        /// <summary>
        /// 单商店修订号。
        /// UI 桥接层应优先按 ShopId 读取该值，避免全局 Revision 导致无意义刷新。
        /// </summary>
        public int Revision;

        /// <summary>
        /// 商品运行时状态。
        /// </summary>
        public ShopProductRuntimeState[] Products = Array.Empty<ShopProductRuntimeState>();

        /// <summary>
        /// 当前激活折扣 ID。
        /// </summary>
        public string[] ActiveDiscountIds = Array.Empty<string>();

        /// <summary>
        /// 最后刷新时间，UTC Unix 秒。
        /// </summary>
        public long LastRefreshUnixSeconds;

        /// <summary>
        /// 商店状态变化后递增单商店修订号。
        /// </summary>
        public void BumpRevision()
        {
            Revision = Revision == int.MaxValue ? 1 : Revision + 1;
        }

        /// <summary>
        /// 转换为存档快照。
        /// </summary>
        public ShopProgressSnapshot ToSnapshot()
        {
            var productSnapshots = Products != null && Products.Length > 0
                ? new ShopProductProgressSnapshot[Products.Length]
                : Array.Empty<ShopProductProgressSnapshot>();

            var writeIndex = 0;
            for (var i = 0; Products != null && i < Products.Length; i++)
            {
                var snapshot = Products[i]?.ToSnapshot();
                if (snapshot == null)
                {
                    continue;
                }

                productSnapshots[writeIndex++] = snapshot;
            }

            if (writeIndex != productSnapshots.Length)
            {
                Array.Resize(ref productSnapshots, writeIndex);
            }

            return new ShopProgressSnapshot
            {
                ShopId = ShopId,
                State = State,
                Revision = Revision,
                Products = productSnapshots,
                ActiveDiscountIds = CloneStringArray(ActiveDiscountIds),
                LastRefreshUnixSeconds = LastRefreshUnixSeconds
            };
        }

        /// <summary>
        /// 从存档快照恢复运行时状态。
        /// </summary>
        public static ShopRuntimeState FromSnapshot(ShopProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            var products = snapshot.Products != null && snapshot.Products.Length > 0
                ? new ShopProductRuntimeState[snapshot.Products.Length]
                : Array.Empty<ShopProductRuntimeState>();

            var writeIndex = 0;
            for (var i = 0; snapshot.Products != null && i < snapshot.Products.Length; i++)
            {
                var state = ShopProductRuntimeState.FromSnapshot(snapshot.Products[i]);
                if (state == null)
                {
                    continue;
                }

                products[writeIndex++] = state;
            }

            if (writeIndex != products.Length)
            {
                Array.Resize(ref products, writeIndex);
            }

            return new ShopRuntimeState
            {
                ShopId = snapshot.ShopId,
                State = snapshot.State,
                Revision = snapshot.Revision,
                Products = products,
                ActiveDiscountIds = CloneStringArray(snapshot.ActiveDiscountIds),
                LastRefreshUnixSeconds = snapshot.LastRefreshUnixSeconds
            };
        }

        private static string[] CloneStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
