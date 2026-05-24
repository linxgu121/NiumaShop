using System;
using NiumaShop.Enum;
using NiumaShop.Result;

namespace NiumaShop.ViewData
{
    /// <summary>
    /// 商店面板 UI 表现数据。
    /// 第一版只包含购买面板，不生成可出售物品列表。
    /// </summary>
    [Serializable]
    public sealed class ShopPanelViewData
    {
        public int Revision;
        public string ShopId;
        public string DisplayName;
        public string Description;
        public ShopState State;
        public ShopProductViewData[] Products = Array.Empty<ShopProductViewData>();
        public ShopOperationResult LastOperationResult;
    }
}
