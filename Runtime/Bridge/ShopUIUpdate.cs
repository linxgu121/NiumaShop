using NiumaShop.Result;
using NiumaShop.ViewData;

namespace NiumaShop.Bridge
{
    /// <summary>
    /// 商店 UI 更新数据。
    /// 只承载版本号、面板表现数据、选中商品和可选操作结果，避免 UI 直接读取商店运行时状态。
    /// </summary>
    public readonly struct ShopUIUpdate
    {
        /// <summary>
        /// 更新类型。
        /// </summary>
        public readonly ShopUIUpdateType UpdateType;

        /// <summary>
        /// 当前商店修订号。
        /// </summary>
        public readonly long Revision;

        /// <summary>
        /// 当前商店面板表现数据。
        /// 当前没有可显示商店时为空。
        /// </summary>
        public readonly ShopPanelViewData PanelData;

        /// <summary>
        /// 上一次商店面板表现数据。
        /// 当 UpdateType 为 Cleared 时，UI 可用它判断被清除前的最终状态。
        /// </summary>
        public readonly ShopPanelViewData PreviousPanelData;

        /// <summary>
        /// 当前选中的商品 ID。
        /// </summary>
        public readonly string SelectedProductId;

        /// <summary>
        /// 刷新前的选中商品 ID。
        /// UI 可以用它判断本次刷新是否发生了被动选中变化。
        /// </summary>
        public readonly string PreviousSelectedProductId;

        /// <summary>
        /// 当前选中的商品表现数据。
        /// </summary>
        public readonly ShopProductViewData SelectedProduct;

        /// <summary>
        /// 当前购买份数。
        /// </summary>
        public readonly int BuyCount;

        /// <summary>
        /// 商店操作结果。
        /// 只有 UpdateType 为 Result 时通常不为空。
        /// </summary>
        public readonly ShopOperationResult ResultData;

        /// <summary>
        /// 是否由桥接层在刷新时被动改变了选中商品。
        /// 例如原商品下架、隐藏或当前商店没有商品时，UI 应重置购买按钮状态，避免误购买。
        /// </summary>
        public readonly bool SelectionChangedByBridge;

        /// <summary>
        /// 当前是否存在面板数据。
        /// </summary>
        public bool HasPanelData => PanelData != null;

        /// <summary>
        /// 当前是否存在选中商品。
        /// </summary>
        public bool HasSelectedProduct => SelectedProduct != null;

        /// <summary>
        /// 当前是否存在操作结果。
        /// </summary>
        public bool HasResultData => ResultData != null;

        public ShopUIUpdate(
            ShopUIUpdateType updateType,
            long revision,
            ShopPanelViewData panelData,
            ShopPanelViewData previousPanelData,
            string selectedProductId,
            ShopProductViewData selectedProduct,
            int buyCount,
            ShopOperationResult resultData = null,
            string previousSelectedProductId = null,
            bool selectionChangedByBridge = false)
        {
            UpdateType = updateType;
            Revision = revision;
            PanelData = panelData;
            PreviousPanelData = previousPanelData;
            SelectedProductId = selectedProductId;
            PreviousSelectedProductId = previousSelectedProductId;
            SelectedProduct = selectedProduct;
            BuyCount = buyCount;
            ResultData = resultData;
            SelectionChangedByBridge = selectionChangedByBridge;
        }
    }
}
