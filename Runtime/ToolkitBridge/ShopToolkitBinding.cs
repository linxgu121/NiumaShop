using System;
using NiumaShop.ViewData;
using NiumaUI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaShop.Bridge
{
    public sealed class ShopToolkitBindingProvider : MonoBehaviour, IToolkitViewBindingProvider
    {
        [SerializeField, Tooltip("BindingProviderId，默认 ShopPanel。需要和 UIToolkitViewRegistrySO 商店 View 的 BindingProviderId 一致。")] private string providerId = "ShopPanel";
        [SerializeField] private string titleLabelName = "TitleText";
        [SerializeField] private string statusLabelName = "StatusText";
        [SerializeField] private string listRootName = "ListRoot";
        [SerializeField] private string detailLabelName = "DetailText";
        [SerializeField] private string resultLabelName = "ResultText";
        [SerializeField] private string emptyRootName = "EmptyRoot";
        [SerializeField] private int maxRows = 80;
        [SerializeField] private string rowClass = "niuma-shop-product-row";

        public string ProviderId => string.IsNullOrWhiteSpace(providerId) ? "ShopPanel" : providerId.Trim();
        public IToolkitViewBinding CreateBinding() => new ShopToolkitBinding(titleLabelName, statusLabelName, listRootName, detailLabelName, resultLabelName, emptyRootName, maxRows, rowClass);
    }

    public sealed class ShopToolkitBinding : ToolkitViewBindingBase
    {
        private readonly string _titleName, _statusName, _listName, _detailName, _resultName, _emptyName, _rowClass;
        private readonly int _maxRows;
        private Label _title, _status, _detail, _result;
        private VisualElement _list, _empty;

        public ShopToolkitBinding(string titleName, string statusName, string listName, string detailName, string resultName, string emptyName, int maxRows, string rowClass)
        {
            _titleName = titleName; _statusName = statusName; _listName = listName; _detailName = detailName; _resultName = resultName; _emptyName = emptyName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-shop-product-row" : rowClass.Trim();
        }

        protected override void OnInitialize()
        {
            _title = QL(_titleName); _status = QL(_statusName); _list = QE(_listName); _detail = QL(_detailName); _result = QL(_resultName); _empty = QE(_emptyName);
            Apply(default);
        }

        protected override void OnRefresh(object viewData)
        {
            if (viewData is ShopUIUpdate update) Apply(update);
            else ApplyPanel(null, null, null, 0, ShopUIUpdateType.Cleared, 0);
        }

        protected override void OnClose() => ApplyPanel(null, null, null, 0, ShopUIUpdateType.Cleared, 0);

        private void Apply(ShopUIUpdate update) => ApplyPanel(update.PanelData, update.SelectedProduct, update.ResultData, update.BuyCount, update.UpdateType, update.Revision);

        private void ApplyPanel(ShopPanelViewData panel, ShopProductViewData selected, NiumaShop.Result.ShopOperationResult result, int buyCount, ShopUIUpdateType updateType, long revision)
        {
            Clear();
            var products = panel?.Products ?? Array.Empty<ShopProductViewData>();
            Set(_title, panel == null ? "商店" : Text(panel.DisplayName, panel.ShopId));
            SetVisible(_empty, panel == null || products.Length == 0);

            if (panel == null)
            {
                Set(_status, $"状态：{updateType}");
                Set(_detail, "暂无商店数据。");
                Set(_result, string.Empty);
                return;
            }

            Set(_status, $"Revision {revision} | {panel.State} | 商品 {products.Length} | 购买份数 {buyCount}");
            Set(_detail, selected != null ? ProductDetail(selected) : Text(panel.Description, "未选择商品。"));
            Set(_result, result == null ? string.Empty : result.Message);

            for (var i = 0; i < products.Length && i < _maxRows; i++)
            {
                var p = products[i];
                if (p == null) continue;
                Add($"{Text(p.DisplayName, p.ProductId)} x{p.Count} | {Prices(p.Prices)} | 可买 {p.MaxPurchasableCount} | {(p.CanBuy ? "可购买" : Reasons(p))}");
            }
        }

        private static string ProductDetail(ShopProductViewData p) => p == null ? "未选择商品。" : $"选中：{Text(p.DisplayName, p.ProductId)}\nItemId：{p.ItemId}\n库存：{p.RemainingStock}\n限购：{p.PurchasedCount}/{p.MaxPurchaseCount}\n价格：{Prices(p.Prices)}\n{p.Description}".Trim();

        private static string Prices(ShopPriceViewData[] prices)
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
            var text = string.Empty;
            for (var i = 0; i < product.CannotBuyReasons.Length; i++)
            {
                if (i > 0) text += ",";
                text += product.CannotBuyReasons[i];
            }
            return text;
        }

        private Label QL(string name) => string.IsNullOrWhiteSpace(name) ? null : Query<Label>(name.Trim());
        private VisualElement QE(string name) => string.IsNullOrWhiteSpace(name) ? null : Root?.Q<VisualElement>(name.Trim());
        private void Clear() { if (_list != null) _list.Clear(); }
        private void Add(string text) { if (_list == null) return; var row = new Label(text ?? string.Empty); row.AddToClassList(_rowClass); _list.Add(row); }
        private static void Set(Label label, string text) { if (label != null) label.text = text ?? string.Empty; }
        private static void SetVisible(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
        private static string Text(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }
}
