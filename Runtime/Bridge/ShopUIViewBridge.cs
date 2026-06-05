using System;
using NiumaShop.Controller;
using NiumaShop.Enum;
using NiumaShop.Request;
using NiumaShop.Result;
using NiumaShop.ViewData;
using UnityEngine;

namespace NiumaShop.Bridge
{
    /// <summary>
    /// 商店模块到 UI 模块的数据驱动桥接层。
    /// 桥接层只按当前 ShopId 的 Revision 拉取 ShopPanelViewData，不订阅事件，也不直接依赖具体 UI 框架。
    /// </summary>
    public sealed class ShopUIViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("商店模块根控制器。请拖入场景中的 NiumaShopController；为空时可按配置自动查找。")]
        [SerializeField] private NiumaShopController shopController;

        [Tooltip("商店面板 UI 脚本。拖团队制作的 Shop 面板脚本；该脚本负责显示商品列表、价格、购买按钮和提示。当前模块未内置正式面板，未制作 UI 时可留空。")]
        [SerializeField] private MonoBehaviour shopUIReceiverProvider;

        [Header("自动查找")]
        [Tooltip("没有手动绑定商店控制器时，是否在场景中自动查找 NiumaShopController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindShopController = true;

        [Header("刷新策略")]
        [Tooltip("启用桥接层时是否立即刷新一次商店面板。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("是否在 LateUpdate 中按当前商店 Revision 自动刷新 UI。关闭后需要外部手动调用 RefreshShopPanel。")]
        [SerializeField] private bool refreshInLateUpdate = true;

        [Tooltip("当前没有可显示商店时，是否发送 Cleared 更新给 UI 接收接口。")]
        [SerializeField] private bool notifyWhenCleared = true;

        [Header("当前商店上下文")]
        [Tooltip("当前打开的商店 ID。交互商人或商店入口打开 UI 时应调用 SetCurrentShop 修改。")]
        [SerializeField] private string currentShopId;

        [Tooltip("当前选中的商品 ID。UI 点击商品条目时可调用 SetSelectedProduct 修改。")]
        [SerializeField] private string selectedProductId;

        [Tooltip("当前购买份数。小于等于 0 时会按 1 处理。")]
        [SerializeField] private int buyCount = 1;

        [Tooltip("购买后商品默认进入的背包容器 ID。为空时由背包模块自动选择容器。")]
        [SerializeField] private string targetContainerId;

        [Header("日志")]
        [Tooltip("桥接层缺少必要引用、构建 ViewData 失败或检测到 UI 回流时是否打印警告。")]
        [SerializeField] private bool logWarnings = true;

        private IShopUIReceiver _receiver;
        private long _observedRevision = -1L;
        private ShopPanelViewData _lastPanelData;
        private bool _hadPanelData;
        private bool _isApplyingUpdate;
        private bool _refreshRequested;
        private long _lastBuildFailureRevision = long.MinValue;

        private void Reset()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(true);
            _observedRevision = -1L;
            _refreshRequested = false;
            _isApplyingUpdate = false;

            if (refreshOnEnable)
            {
                RefreshShopPanel();
            }
        }

        private void OnDisable()
        {
            _isApplyingUpdate = false;
            _refreshRequested = false;
        }

        private void LateUpdate()
        {
            if (_refreshRequested)
            {
                _refreshRequested = false;
                RefreshShopPanel();
                return;
            }

            if (!refreshInLateUpdate || !EnsureController() || string.IsNullOrWhiteSpace(currentShopId))
            {
                return;
            }

            var currentRevision = shopController.GetShopRevision(currentShopId);
            if (_observedRevision == currentRevision)
            {
                return;
            }

            RefreshShopPanel();
        }

        /// <summary>
        /// 打开或切换当前商店，并请求刷新 UI。
        /// </summary>
        public void SetCurrentShop(string shopId)
        {
            currentShopId = shopId;
            _observedRevision = -1L;
            RequestRefresh();
        }

        /// <summary>
        /// 打开指定商店。
        /// 交互桥接层可以直接调用该方法。
        /// </summary>
        public void OpenShop(string shopId)
        {
            SetCurrentShop(shopId);
        }

        /// <summary>
        /// 关闭当前商店并清空 UI。
        /// </summary>
        public void CloseShop()
        {
            currentShopId = null;
            selectedProductId = null;
            _observedRevision = -1L;
            ApplyClearUpdate();
        }

        /// <summary>
        /// 设置当前选中的商品，并请求刷新 UI。
        /// </summary>
        public void SetSelectedProduct(string productId)
        {
            selectedProductId = productId;
            RequestRefresh();
        }

        /// <summary>
        /// 清空当前选中商品，并请求刷新 UI。
        /// </summary>
        public void ClearSelectedProduct()
        {
            SetSelectedProduct(null);
        }

        /// <summary>
        /// 设置当前购买份数，并请求刷新 UI。
        /// </summary>
        public void SetBuyCount(int count)
        {
            buyCount = Mathf.Max(1, count);
            RequestRefresh();
        }

        /// <summary>
        /// 设置购买后的目标背包容器。
        /// </summary>
        public void SetTargetContainerId(string containerId)
        {
            targetContainerId = containerId;
            RequestRefresh();
        }

        /// <summary>
        /// 手动刷新商店面板。
        /// 只读取商店和背包状态，不执行购买、不修改商店运行时进度。
        /// </summary>
        public void RefreshShopPanel()
        {
            if (!EnsureController() || string.IsNullOrWhiteSpace(currentShopId))
            {
                ApplyClearUpdate();
                return;
            }

            var targetRevision = shopController.GetShopRevision(currentShopId);
            ShopPanelViewData panelData;
            try
            {
                panelData = shopController.BuildShopViewData(currentShopId);
            }
            catch (Exception exception)
            {
                _observedRevision = -1L;
                if (logWarnings && _lastBuildFailureRevision != targetRevision)
                {
                    Debug.LogError($"[NiumaShopUIBridge] 构建商店 UI 表现数据失败，桥接层会在下一次刷新时重试。ShopId={currentShopId}, Revision={targetRevision}, Error={exception.Message}", this);
                }

                _lastBuildFailureRevision = targetRevision;
                return;
            }

            _lastBuildFailureRevision = long.MinValue;
            _observedRevision = targetRevision;
            var selectedProduct = ResolveSelectedProduct(
                panelData,
                out var previousSelectedProductId,
                out var selectionChangedByBridge);
            _hadPanelData = panelData != null;
            ApplyRawUpdate(new ShopUIUpdate(
                ShopUIUpdateType.Refresh,
                _observedRevision,
                panelData,
                _lastPanelData,
                selectedProductId,
                selectedProduct,
                Mathf.Max(1, buyCount),
                previousSelectedProductId: previousSelectedProductId,
                selectionChangedByBridge: selectionChangedByBridge));

            _lastPanelData = panelData;
        }

        /// <summary>
        /// 检查当前选中商品是否可购买。
        /// UI 可以用该方法在点击前做按钮可用性判断。
        /// </summary>
        public bool CanBuySelectedProduct(out ShopOperationResult result)
        {
            result = null;
            if (!EnsureController())
            {
                result = BuildBridgeFailure(ShopFailureReason.ServiceNotReady, "商店控制器未就绪。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentShopId) || string.IsNullOrWhiteSpace(selectedProductId))
            {
                result = BuildBridgeFailure(ShopFailureReason.InvalidRequest, "未选择商店或商品。");
                return false;
            }

            return shopController.CanBuy(CreateBuyRequest(selectedProductId), out result);
        }

        /// <summary>
        /// 购买当前选中商品，并把交易结果推送给 UI。
        /// </summary>
        public ShopOperationResult BuySelectedProduct()
        {
            if (!EnsureController())
            {
                var failed = BuildBridgeFailure(ShopFailureReason.ServiceNotReady, "商店控制器未就绪。");
                ApplyResultUpdate(failed);
                return failed;
            }

            if (string.IsNullOrWhiteSpace(currentShopId) || string.IsNullOrWhiteSpace(selectedProductId))
            {
                var failed = BuildBridgeFailure(ShopFailureReason.InvalidRequest, "未选择商店或商品。");
                ApplyResultUpdate(failed);
                return failed;
            }

            var result = shopController.BuyProduct(CreateBuyRequest(selectedProductId));
            ApplyResultUpdate(result);
            return result;
        }

        /// <summary>
        /// 刷新当前商店库存，并请求 UI 更新。
        /// </summary>
        public bool RefreshCurrentShop()
        {
            if (!EnsureController() || string.IsNullOrWhiteSpace(currentShopId))
            {
                return false;
            }

            var changed = shopController.RefreshShop(new RefreshShopRequest(currentShopId, nameof(ShopUIViewBridge)));
            RequestRefresh();
            return changed;
        }

        private ShopProductViewData ResolveSelectedProduct(
            ShopPanelViewData panelData,
            out string previousSelectedProductId,
            out bool selectionChangedByBridge)
        {
            previousSelectedProductId = selectedProductId;
            selectionChangedByBridge = false;

            if (panelData == null || panelData.Products == null || panelData.Products.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(selectedProductId))
                {
                    selectedProductId = null;
                    selectionChangedByBridge = true;
                }

                return null;
            }

            var selected = FindProductViewData(panelData, selectedProductId);
            if (selected != null)
            {
                return selected;
            }

            // 没有旧选中项时，首次打开商店可以默认选中第一个商品，减少 UI 初始化成本。
            if (string.IsNullOrWhiteSpace(selectedProductId))
            {
                selected = panelData.Products[0];
                selectedProductId = selected.ProductId;
                selectionChangedByBridge = true;
                return selected;
            }

            // 旧商品在刷新后消失时，不自动跳到下一个商品，避免玩家误点购买按钮买到别的商品。
            selectedProductId = null;
            selectionChangedByBridge = true;
            return null;
        }

        private static ShopProductViewData FindProductViewData(ShopPanelViewData panelData, string productId)
        {
            if (panelData == null || panelData.Products == null || string.IsNullOrWhiteSpace(productId))
            {
                return null;
            }

            for (var i = 0; i < panelData.Products.Length; i++)
            {
                var product = panelData.Products[i];
                if (product != null && string.Equals(product.ProductId, productId, StringComparison.Ordinal))
                {
                    return product;
                }
            }

            return null;
        }

        private BuyProductRequest CreateBuyRequest(string productId)
        {
            return new BuyProductRequest(
                currentShopId,
                productId,
                Mathf.Max(1, buyCount),
                targetContainerId,
                nameof(ShopUIViewBridge));
        }

        private ShopOperationResult BuildBridgeFailure(ShopFailureReason reason, string message)
        {
            return ShopOperationResult.Fail(
                currentShopId,
                selectedProductId,
                ShopTransactionType.Buy,
                reason,
                message);
        }

        private void ApplyResultUpdate(ShopOperationResult result)
        {
            if (!EnsureController())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(result?.ShopId))
            {
                currentShopId = result.ShopId;
            }

            ShopPanelViewData panelData = null;
            ShopProductViewData selectedProduct = null;
            var targetRevision = !string.IsNullOrWhiteSpace(currentShopId)
                ? shopController.GetShopRevision(currentShopId)
                : -1L;
            var panelBuilt = false;
            var previousSelectedProductId = selectedProductId;
            var selectionChangedByBridge = false;

            if (!string.IsNullOrWhiteSpace(currentShopId))
            {
                try
                {
                    panelData = shopController.BuildShopViewData(currentShopId);
                    selectedProduct = ResolveSelectedProduct(
                        panelData,
                        out previousSelectedProductId,
                        out selectionChangedByBridge);
                    panelBuilt = true;
                }
                catch (Exception exception)
                {
                    _observedRevision = -1L;
                    _refreshRequested = true;
                    if (logWarnings)
                    {
                        Debug.LogError($"[NiumaShopUIBridge] 商店操作后刷新面板失败：{exception.Message}", this);
                    }
                }
            }

            if (panelBuilt)
            {
                _observedRevision = targetRevision;
            }

            ApplyRawUpdate(new ShopUIUpdate(
                ShopUIUpdateType.Result,
                targetRevision,
                panelData,
                _lastPanelData,
                selectedProductId,
                selectedProduct,
                Mathf.Max(1, buyCount),
                result,
                previousSelectedProductId,
                selectionChangedByBridge));

            if (panelBuilt && panelData != null)
            {
                _hadPanelData = true;
                _lastPanelData = panelData;
            }
        }

        private void ApplyClearUpdate()
        {
            if (!notifyWhenCleared && !_hadPanelData)
            {
                return;
            }

            _receiver = ResolveReceiver(true);
            ApplyRawUpdate(new ShopUIUpdate(
                ShopUIUpdateType.Cleared,
                _observedRevision,
                null,
                _lastPanelData,
                null,
                null,
                Mathf.Max(1, buyCount)));

            _hadPanelData = false;
            _lastPanelData = null;
        }

        private void ApplyRawUpdate(ShopUIUpdate update)
        {
            _receiver = ResolveReceiver(true);
            if (_receiver == null)
            {
                return;
            }

            if (_isApplyingUpdate)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaShopUIBridge] 检测到 UI 刷新重入，已跳过本次 ApplyShopUpdate。请不要在 IShopUIReceiver.ApplyShopUpdate 中修改商店状态。", this);
                }

                return;
            }

            var revisionBeforeApply = shopController != null && !string.IsNullOrWhiteSpace(currentShopId)
                ? shopController.GetShopRevision(currentShopId)
                : _observedRevision;

            _isApplyingUpdate = true;
            try
            {
                _receiver.ApplyShopUpdate(update);
            }
            finally
            {
                _isApplyingUpdate = false;
            }

            if (shopController != null
                && !string.IsNullOrWhiteSpace(currentShopId)
                && shopController.GetShopRevision(currentShopId) != revisionBeforeApply)
            {
                _observedRevision = -1L;
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaShopUIBridge] IShopUIReceiver.ApplyShopUpdate 内修改了商店数据，桥接层已请求下一帧重新刷新。请把购买、刷新库存等命令放到输入、交互或业务管线中处理。", this);
                }
            }
        }

        private void RequestRefresh()
        {
            _observedRevision = -1L;
            _refreshRequested = true;
        }

        private bool EnsureController()
        {
            ResolveShopController(true);
            return shopController != null;
        }

        private void ResolveReferences(bool logMissing)
        {
            ResolveShopController(logMissing);
            _receiver = ResolveReceiver(logMissing);
        }

        private void ResolveShopController(bool logMissing)
        {
            if (shopController != null)
            {
                return;
            }

            if (autoFindShopController)
            {
#if UNITY_2023_1_OR_NEWER
                shopController = FindFirstObjectByType<NiumaShopController>();
#else
                shopController = FindObjectOfType<NiumaShopController>();
#endif
            }

            if (shopController == null && logWarnings && logMissing)
            {
                Debug.LogWarning("[NiumaShopUIBridge] 未找到 NiumaShopController，请在 Inspector 中绑定商店控制器。", this);
            }
        }

        private IShopUIReceiver ResolveReceiver(bool logMissing)
        {
            var receiver = shopUIReceiverProvider as IShopUIReceiver;
            if (receiver == null && logWarnings && logMissing && shopUIReceiverProvider != null)
            {
                Debug.LogWarning("[NiumaShopUIBridge] Shop UI Receiver 绑定的不是商店面板脚本，请拖团队制作的 Shop 面板脚本。", this);
            }

            return receiver;
        }
    }
}
