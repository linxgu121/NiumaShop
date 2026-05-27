using NiumaInteract.Core.Data;
using NiumaInteract.Core.Enum;
using NiumaInteract.Core.Interface;
using NiumaShop.Bridge;
using NiumaShop.Controller;
using NiumaShop.Enum;
using UnityEngine;
using UnityEngine.Events;

namespace NiumaShop.Bridge.Interact
{
    /// <summary>
    /// 商店交互入口。
    /// 负责把 NiumaInteract 的交互触发转换为打开指定 ShopId 的 UI 行为，不执行购买、不修改商品库存。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopInteractable : MonoBehaviour, IInteractable, IInteractionPromptPolicy
    {
        [Header("商店引用")]
        [Tooltip("商店模块根控制器。用于检查商店是否存在、是否已解锁。正式场景建议手动绑定。")]
        [SerializeField] private NiumaShopController shopController;

        [Tooltip("商店 UI 桥接层。交互成功后会调用它设置当前 ShopId 并刷新 UI。正式场景建议手动绑定。")]
        [SerializeField] private ShopUIViewBridge shopUIViewBridge;

        [Tooltip("商店控制器为空时是否自动在场景中查找。调试阶段可开启；正式多场景建议手动绑定。")]
        [SerializeField] private bool autoFindShopController = true;

        [Tooltip("商店 UI 桥接层为空时是否自动在场景中查找。调试阶段可开启；正式多场景建议手动绑定。")]
        [SerializeField] private bool autoFindUIViewBridge = true;

        [Header("商店配置")]
        [Tooltip("要打开的商店稳定 ID，必须对应 ShopAsset.ShopId。")]
        [SerializeField] private string shopId;

        [Tooltip("交互前是否检查商店状态。开启后只有 ShopState.Unlocked 的商店可以打开。")]
        [SerializeField] private bool requireUnlockedShop = true;

        [Tooltip("打开商店时是否清空旧选中商品。建议开启，避免从上一个商店残留选中项导致误购买。")]
        [SerializeField] private bool clearSelectedProductOnOpen = true;

        [Tooltip("交互成功后是否立刻刷新一次商店 UI。开启后不必等到下一帧 LateUpdate。")]
        [SerializeField] private bool refreshUIViewImmediately = true;

        [Header("UI 打开表现")]
        [Tooltip("需要激活的商店 UI 根节点。为空时只调用 ShopUIViewBridge，不主动 SetActive。")]
        [SerializeField] private GameObject shopPanelRoot;

        [Tooltip("交互成功后是否激活商店 UI 根节点。")]
        [SerializeField] private bool activatePanelRootOnOpen = true;

        [Tooltip("商店成功打开后触发的 UnityEvent。可用于播放音效、切换输入模式或打开 NiumaUI 视图。")]
        [SerializeField] private UnityEvent onShopOpened;

        [Tooltip("商店打开失败后触发的 UnityEvent。可用于播放失败音效或显示提示。")]
        [SerializeField] private UnityEvent onShopOpenFailed;

        [Header("显示")]
        [Tooltip("交互 ID。正式内容建议填写稳定 ID，用于任务、存档和调试追踪；为空时回退为 ShopId 或物体名称。")]
        [SerializeField] private string interactionId;

        [Tooltip("交互显示名称。为空时使用 ShopId；ShopId 也为空时使用物体名称。")]
        [SerializeField] private string displayName = "商人";

        [Tooltip("交互提示文本，例如“交易”“打开商店”。")]
        [SerializeField] private string promptText = "交易";

        [Tooltip("商店未开放时的调试提示文本。默认不会显示给玩家，因为 CanInteract 会返回 false。")]
        [SerializeField] private string closedPromptText = "商店未开放";

        [Tooltip("交互提示类型。商人头顶提示通常使用 WorldSpace。")]
        [SerializeField] private PromptType promptType = PromptType.WorldSpace;

        [Tooltip("世界空间提示挂点。为空时使用 InteractionTransform。")]
        [SerializeField] private Transform promptAnchor;

        [Header("交互")]
        [Tooltip("交互检测使用的稳定位置源。为空时使用当前物体 Transform。")]
        [SerializeField] private Transform interactionTransform;

        [Tooltip("交互排序优先级。数值越大越容易成为当前焦点目标。Inspector 限制在 0 到 100。")]
        [Range(0f, 100f)]
        [SerializeField] private float priority = 1f;

        [Tooltip("长按触发阈值，单位秒。普通交易入口使用 0。")]
        [Min(0f)]
        [SerializeField] private float longPressDuration;

        [Tooltip("该商店入口支持的交互类型。普通交易入口使用 Short。")]
        [SerializeField] private InteractKind supportedKinds = InteractKind.Short;

        [Header("日志")]
        [Tooltip("缺少引用、商店未开放或打开失败时是否输出中文警告。")]
        [SerializeField] private bool logWarnings = true;

        private bool _warnedInvalidConfig;
        private bool _warnedMissingShopController;
        private bool _warnedMissingUIViewBridge;
        private bool _warnedMissingShopState;

        public string InteractionId => BuildInteractionId();
        public Transform InteractionTransform => interactionTransform != null ? interactionTransform : transform;
        public string DisplayName => BuildDisplayName();
        public string PromptText => IsShopUnlocked(false) ? BuildPromptText() : closedPromptText;
        public PromptType PromptType => promptType;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : InteractionTransform;
        public float Priority => priority;
        public float LongPressDuration => longPressDuration;
        public InteractKind SupportedKinds => supportedKinds;

        /// <summary>
        /// 商店打开后仍然允许再次交互，所以不压制提示。
        /// </summary>
        public bool SuppressPromptAfterSuccess => false;

        /// <summary>
        /// 只有配置完整、依赖存在且商店处于可打开状态时，才允许交互系统触发打开商店。
        /// </summary>
        public bool CanInteract(in InteractionContext context)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(shopId) || !HasValidSupportedKind())
            {
                LogWarningOnce(ref _warnedInvalidConfig, "商店交互配置不完整，缺少 ShopId 或交互类型无效。");
                return false;
            }

            if (!ResolveUIViewBridge(false))
            {
                LogWarningOnce(ref _warnedMissingUIViewBridge, "未找到 ShopUIViewBridge，请在 Inspector 中绑定商店 UI 桥接层。");
                return false;
            }

            return !requireUnlockedShop || IsShopUnlocked(false);
        }

        /// <summary>
        /// 执行打开商店。
        /// 购买、出售、刷新库存等正式交易行为由商店 UI 和 ShopService 处理。
        /// </summary>
        public void Interact(in InteractionRequest request)
        {
            if ((supportedKinds & request.Kind) != request.Kind)
            {
                return;
            }

            if (!CanOpenShop(true))
            {
                onShopOpenFailed?.Invoke();
                return;
            }

            var bridgeWasActive = shopUIViewBridge.isActiveAndEnabled;

            if (clearSelectedProductOnOpen)
            {
                shopUIViewBridge.ClearSelectedProduct();
            }

            shopUIViewBridge.OpenShop(shopId);

            // 先写入 ShopId，再激活 UI 根节点，避免 ShopUIViewBridge.OnEnable 先跑出一次空刷新。
            if (activatePanelRootOnOpen && shopPanelRoot != null)
            {
                shopPanelRoot.SetActive(true);
            }

            var bridgeActivatedByPanel = !bridgeWasActive && shopUIViewBridge.isActiveAndEnabled;
            if (refreshUIViewImmediately && !bridgeActivatedByPanel)
            {
                shopUIViewBridge.RefreshShopPanel();
            }

            onShopOpened?.Invoke();
        }

        private bool CanOpenShop(bool logMissing)
        {
            if (string.IsNullOrWhiteSpace(shopId) || !HasValidSupportedKind())
            {
                LogWarning("商店交互配置不完整，缺少 ShopId 或交互类型无效。", logMissing);
                return false;
            }

            if (!ResolveUIViewBridge(logMissing))
            {
                return false;
            }

            if (requireUnlockedShop && !IsShopUnlocked(logMissing))
            {
                LogWarning($"商店当前不可打开：ShopId={shopId}。", logMissing);
                return false;
            }

            return true;
        }

        private bool IsShopUnlocked(bool logMissing)
        {
            if (!requireUnlockedShop)
            {
                return true;
            }

            if (!ResolveShopController(logMissing))
            {
                if (!logMissing)
                {
                    LogWarningOnce(ref _warnedMissingShopController, "未找到 NiumaShopController，请在 Inspector 中绑定商店控制器。");
                }

                return false;
            }

            if (!shopController.TryGetShopState(shopId, out var snapshot) || snapshot == null)
            {
                var message = $"未找到商店状态，请确认 ShopAsset 已注册且 ShopId 正确：{shopId}。";
                if (logMissing)
                {
                    LogWarning(message, true);
                }
                else
                {
                    LogWarningOnce(ref _warnedMissingShopState, message);
                }

                return false;
            }

            return snapshot.State == ShopState.Unlocked;
        }

        private bool ResolveShopController(bool logMissing)
        {
            if (shopController != null)
            {
                return true;
            }

            if (autoFindShopController)
            {
#if UNITY_2023_1_OR_NEWER
                shopController = FindFirstObjectByType<NiumaShopController>();
#else
                shopController = FindObjectOfType<NiumaShopController>();
#endif
            }

            if (shopController == null)
            {
                LogWarning("未找到 NiumaShopController，请在 Inspector 中绑定商店控制器。", logMissing);
            }

            return shopController != null;
        }

        private bool ResolveUIViewBridge(bool logMissing)
        {
            if (shopUIViewBridge != null)
            {
                return true;
            }

            if (autoFindUIViewBridge)
            {
#if UNITY_2023_1_OR_NEWER
                shopUIViewBridge = FindFirstObjectByType<ShopUIViewBridge>();
#else
                shopUIViewBridge = FindObjectOfType<ShopUIViewBridge>();
#endif
            }

            if (shopUIViewBridge == null)
            {
                LogWarning("未找到 ShopUIViewBridge，请在 Inspector 中绑定商店 UI 桥接层。", logMissing);
            }

            return shopUIViewBridge != null;
        }

        private string BuildInteractionId()
        {
            if (!string.IsNullOrWhiteSpace(interactionId))
            {
                return interactionId;
            }

            return !string.IsNullOrWhiteSpace(shopId) ? shopId : gameObject.name;
        }

        private string BuildDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return !string.IsNullOrWhiteSpace(shopId) ? shopId : gameObject.name;
        }

        private string BuildPromptText()
        {
            var text = string.IsNullOrWhiteSpace(promptText) ? "交易" : promptText;
            if (IsLongOnly() && !text.Contains("长按"))
            {
                return "长按" + text;
            }

            return text;
        }

        private bool HasValidSupportedKind()
        {
            if (supportedKinds == InteractKind.None)
            {
                return false;
            }

            if (IsLongOnly() && longPressDuration <= 0f)
            {
                return false;
            }

            return true;
        }

        private bool IsLongOnly()
        {
            return (supportedKinds & InteractKind.Long) == InteractKind.Long
                   && (supportedKinds & InteractKind.Short) != InteractKind.Short;
        }

        private void LogWarning(string message, bool logMissing)
        {
            if (logWarnings && logMissing)
            {
                Debug.LogWarning($"[ShopInteractable] {message}", this);
            }
        }

        private void LogWarningOnce(ref bool warned, string message)
        {
            if (!logWarnings || warned)
            {
                return;
            }

            warned = true;
            Debug.LogWarning($"[ShopInteractable] {message}", this);
        }

        private void OnValidate()
        {
            priority = Mathf.Clamp(priority, 0f, 100f);
            longPressDuration = Mathf.Max(0f, longPressDuration);
            _warnedInvalidConfig = false;
            _warnedMissingShopController = false;
            _warnedMissingUIViewBridge = false;
            _warnedMissingShopState = false;
        }
    }
}
