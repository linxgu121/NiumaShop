using System;
using System.Collections.Generic;
using NiumaShop.Result;
using NiumaShop.ViewData;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace NiumaShop.Bridge
{
    public sealed class ShopToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Serializable] public sealed class ShopProductEvent : UnityEvent<string> { }
        [Serializable] public sealed class ShopCountEvent : UnityEvent<int> { }

        [Header("元素名称")]
        [SerializeField, Tooltip("标题 Label 的 name。默认 TitleText。")]
        private string titleLabelName = "TitleText";
        [SerializeField, Tooltip("状态 Label 的 name。默认 StatusText。")]
        private string statusLabelName = "StatusText";
        [SerializeField, Tooltip("商品列表 ListView 的 name。默认 ListRoot。")]
        private string listViewName = "ListRoot";
        [SerializeField, Tooltip("详情 Label 的 name。显示当前选中商品。")]
        private string detailLabelName = "DetailText";
        [SerializeField, Tooltip("结果 Label 的 name。显示购买结果。")]
        private string resultLabelName = "ResultText";
        [SerializeField, Tooltip("空状态节点的 name。没有商品时显示。")]
        private string emptyRootName = "EmptyRoot";

        [Header("按钮名称")]
        [SerializeField, Tooltip("购买按钮 name。点击时把当前 ProductId 传给 On Buy Requested。")]
        private string buyButtonName = "BuyButton";
        [SerializeField, Tooltip("购买数量增加按钮 name。")]
        private string increaseCountButtonName = "IncreaseCountButton";
        [SerializeField, Tooltip("购买数量减少按钮 name。")]
        private string decreaseCountButtonName = "DecreaseCountButton";

        [Header("列表")]
        [SerializeField, Tooltip("最多显示多少个商品。")]
        private int maxRows = 80;
        [SerializeField, Tooltip("商品行 USS class。")]
        private string rowClass = "niuma-shop-product-row";
        [SerializeField, Tooltip("选中行 USS class。")]
        private string selectedRowClass = "is-selected";
        [SerializeField, Tooltip("禁用行 USS class。")]
        private string disabledRowClass = "is-disabled";

        [Header("交互事件")]
        [SerializeField, Tooltip("点击商品行时触发。参数为 ProductId。")]
        private ShopProductEvent onProductSelected = new ShopProductEvent();
        [SerializeField, Tooltip("点击 BuyButton 时触发。参数为当前 ProductId。购买数量请从 ShopUIViewBridge 或你的 UI 状态读取。")]
        private ShopProductEvent onBuyRequested = new ShopProductEvent();
        [SerializeField, Tooltip("购买数量改变时触发。参数为新的购买数量。")]
        private ShopCountEvent onBuyCountChanged = new ShopCountEvent();

        protected override string DefaultProviderId => "ShopPanel";

        public override IToolkitViewBinding CreateBinding()
        {
            return new ShopToolkitBinding(
                titleLabelName,
                statusLabelName,
                listViewName,
                detailLabelName,
                resultLabelName,
                emptyRootName,
                buyButtonName,
                increaseCountButtonName,
                decreaseCountButtonName,
                maxRows,
                rowClass,
                selectedRowClass,
                disabledRowClass,
                id => onProductSelected?.Invoke(id),
                id => onBuyRequested?.Invoke(id),
                count => onBuyCountChanged?.Invoke(count));
        }
    }

    public sealed class ShopToolkitViewModel : UIPanelViewModelBase
    {
        public readonly List<ToolkitTextRowData> Rows = new List<ToolkitTextRowData>();
        public bool HasData { get; private set; }
        public ShopUIUpdate LastUpdate { get; private set; }
        public ShopPanelViewData Panel => HasData ? LastUpdate.PanelData : null;
        public ShopProductViewData SelectedProduct { get; private set; }
        public string SelectedProductId { get; private set; }
        public int BuyCount { get; private set; } = 1;
        public int PageIndex { get; private set; }
        public string SearchKeyword { get; private set; }

        public void Apply(ShopUIUpdate update, int maxRows)
        {
            LastUpdate = update;
            HasData = true;
            var panel = update.PanelData;
            SetContext(panel?.ShopId);
            SelectedProduct = update.SelectedProduct ?? FindProduct(panel, SelectedProductId);
            SelectedProductId = NormalizeSelection(panel, SelectedProduct, SelectedProductId);
            BuyCount = Mathf.Max(1, update.BuyCount);
            ClampBuyCount();
            RebuildRows(maxRows);
            MarkDirty();
        }

        public void Select(string productId)
        {
            SelectedProductId = string.IsNullOrWhiteSpace(productId) ? null : productId.Trim();
            SelectedProduct = FindProduct(Panel, SelectedProductId);
            ClampBuyCount();
            RebuildRows(int.MaxValue);
            MarkDirty();
        }

        public int AddBuyCount(int delta)
        {
            BuyCount = Mathf.Max(1, BuyCount + delta);
            ClampBuyCount();
            MarkDirty();
            return BuyCount;
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            LastUpdate = default;
            HasData = false;
            SelectedProduct = null;
            SelectedProductId = null;
            BuyCount = 1;
            PageIndex = 0;
            SearchKeyword = string.Empty;
            Rows.Clear();
        }

        private void RebuildRows(int maxRows)
        {
            Rows.Clear();
            var products = Panel?.Products ?? Array.Empty<ShopProductViewData>();
            var limit = Math.Max(1, maxRows);
            for (var i = 0; i < products.Length && i < limit; i++)
            {
                var p = products[i];
                if (p == null)
                    continue;

                var id = string.IsNullOrWhiteSpace(p.ProductId) ? $"product:{i}" : p.ProductId;
                Rows.Add(new ToolkitTextRowData(id, $"{Text(p.DisplayName, p.ProductId)} x{p.Count} | {Prices(p.Prices)} | 可买 {p.MaxPurchasableCount} | {(p.CanBuy ? "可购买" : Reasons(p))}", string.Equals(SelectedProductId, id, StringComparison.Ordinal), p.CanBuy, p));
            }
        }

        private void ClampBuyCount()
        {
            if (SelectedProduct != null && SelectedProduct.MaxPurchasableCount > 0)
                BuyCount = Mathf.Clamp(BuyCount, 1, SelectedProduct.MaxPurchasableCount);
            else
                BuyCount = Mathf.Max(1, BuyCount);
        }

        private static ShopProductViewData FindProduct(ShopPanelViewData panel, string productId)
        {
            if (panel?.Products == null || string.IsNullOrWhiteSpace(productId))
                return null;

            for (var i = 0; i < panel.Products.Length; i++)
                if (string.Equals(panel.Products[i]?.ProductId, productId, StringComparison.Ordinal))
                    return panel.Products[i];
            return null;
        }

        private static string NormalizeSelection(ShopPanelViewData panel, ShopProductViewData selected, string previous)
        {
            if (!string.IsNullOrWhiteSpace(selected?.ProductId))
                return selected.ProductId.Trim();
            if (!string.IsNullOrWhiteSpace(previous))
                return previous.Trim();
            var products = panel?.Products;
            if (products != null)
            {
                for (var i = 0; i < products.Length; i++)
                    if (!string.IsNullOrWhiteSpace(products[i]?.ProductId))
                        return products[i].ProductId.Trim();
            }
            return null;
        }

        public static string Prices(ShopPriceViewData[] prices)
        {
            if (prices == null || prices.Length == 0) return "免费";
            var text = string.Empty;
            for (var i = 0; i < prices.Length; i++)
            {
                var p = prices[i];
                if (p == null) continue;
                if (text.Length > 0) text += " + ";
                text += $"{Text(p.DisplayName, p.CurrencyItemId)} {p.Amount}/{p.PlayerOwnedCount}";
            }
            return string.IsNullOrWhiteSpace(text) ? "免费" : text;
        }

        private static string Reasons(ShopProductViewData product)
        {
            if (product?.CannotBuyReasons == null || product.CannotBuyReasons.Length == 0) return "不可购买";
            return string.Join(",", product.CannotBuyReasons);
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }

    public sealed class ShopToolkitBinding : ToolkitViewBindingBase<ShopUIUpdate, ShopToolkitViewModel>
    {
        private readonly string _titleName;
        private readonly string _statusName;
        private readonly string _listName;
        private readonly string _detailName;
        private readonly string _resultName;
        private readonly string _emptyName;
        private readonly string _buyButtonName;
        private readonly string _increaseCountButtonName;
        private readonly string _decreaseCountButtonName;
        private readonly int _maxRows;
        private readonly string _rowClass;
        private readonly string _selectedClass;
        private readonly string _disabledClass;
        private readonly Action<string> _productSelected;
        private readonly Action<string> _buyRequested;
        private readonly Action<int> _buyCountChanged;
        private readonly ToolkitListBinding<ToolkitTextRowData> _listBinding = new ToolkitListBinding<ToolkitTextRowData>();
        private Label _title;
        private Label _status;
        private Label _detail;
        private Label _result;

        public ShopToolkitBinding(string titleName, string statusName, string listName, string detailName, string resultName, string emptyName, string buyButtonName, string increaseCountButtonName, string decreaseCountButtonName, int maxRows, string rowClass, string selectedClass, string disabledClass, Action<string> productSelected, Action<string> buyRequested, Action<int> buyCountChanged)
        {
            _titleName = titleName;
            _statusName = statusName;
            _listName = listName;
            _detailName = detailName;
            _resultName = resultName;
            _emptyName = emptyName;
            _buyButtonName = buyButtonName;
            _increaseCountButtonName = increaseCountButtonName;
            _decreaseCountButtonName = decreaseCountButtonName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-shop-product-row" : rowClass.Trim();
            _selectedClass = selectedClass;
            _disabledClass = disabledClass;
            _productSelected = productSelected;
            _buyRequested = buyRequested;
            _buyCountChanged = buyCountChanged;
        }

        protected override void OnInitializeTyped()
        {
            _title = QLabel(_titleName);
            _status = QLabel(_statusName);
            _detail = QLabel(_detailName);
            _result = QLabel(_resultName);
            _listBinding.Bind(Root, _listName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, HandleRowClicked), _emptyName);
            Callbacks.RegisterButton(Root, _buyButtonName, () => InvokeSelected(_buyRequested), CanBuySelected);
            Callbacks.RegisterButton(Root, _increaseCountButtonName, () => ChangeBuyCount(1), HasSelection);
            Callbacks.RegisterButton(Root, _decreaseCountButtonName, () => ChangeBuyCount(-1), HasSelection);
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(ShopUIUpdate viewData, ShopToolkitViewModel viewModel)
        {
            viewModel.Apply(viewData, _maxRows);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _listBinding.Clear();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _listBinding.Dispose();
        }

        private void HandleRowClicked(ToolkitTextRowData row)
        {
            if (row == null)
                return;

            ViewModel.Select(row.Id);
            _productSelected?.Invoke(row.Id);
            ApplyVisualState(ViewModel);
        }

        private void ChangeBuyCount(int delta)
        {
            var next = ViewModel.AddBuyCount(delta);
            _buyCountChanged?.Invoke(next);
            ApplyVisualState(ViewModel);
        }

        private void ApplyVisualState(ShopToolkitViewModel viewModel)
        {
            var panel = viewModel?.Panel;
            var products = panel?.Products ?? Array.Empty<ShopProductViewData>();
            SetText(_title, panel == null ? "商店" : Text(panel.DisplayName, panel.ShopId));
            _listBinding.ReplaceAll(viewModel != null ? viewModel.Rows : Array.Empty<ToolkitTextRowData>());
            SetText(_status, panel == null ? $"???{(viewModel != null && viewModel.HasData ? viewModel.LastUpdate.UpdateType : ShopUIUpdateType.Cleared)}" : $"Revision {viewModel.LastUpdate.Revision} | {panel.State} | ?? {products.Length} | ???? {viewModel.BuyCount}");
            SetText(_detail, viewModel?.SelectedProduct != null ? ProductDetail(viewModel.SelectedProduct) : Text(panel?.Description, "未选择商品。"));
            SetText(_result, viewModel != null && viewModel.HasData && viewModel.LastUpdate.ResultData != null ? viewModel.LastUpdate.ResultData.Message : string.Empty);
        }

        private bool HasSelection()
        {
            return !string.IsNullOrWhiteSpace(ViewModel?.SelectedProductId);
        }

        private bool CanBuySelected()
        {
            return HasSelection() && ViewModel.SelectedProduct != null && ViewModel.SelectedProduct.CanBuy;
        }

        private void InvokeSelected(Action<string> action)
        {
            if (HasSelection())
                action?.Invoke(ViewModel.SelectedProductId);
        }

        private static string ProductDetail(ShopProductViewData p)
        {
            return p == null ? "未选择商品。" : $"选中：{Text(p.DisplayName, p.ProductId)}\nItemId：{p.ItemId}\n库存：{p.RemainingStock}\n限购：{p.PurchasedCount}/{p.MaxPurchaseCount}\n价格：{ShopToolkitViewModel.Prices(p.Prices)}\n{p.Description}".Trim();
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }
}
