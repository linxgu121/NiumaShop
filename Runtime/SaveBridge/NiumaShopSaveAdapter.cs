using System;
using System.Collections.Generic;
using System.Text;
using NiumaSave.Controller;
using NiumaSave.Data;
using NiumaSave.Provider;
using NiumaShop.Controller;
using NiumaShop.Data;
using UnityEngine;

namespace NiumaShop.SaveBridge
{
    /// <summary>
    /// NiumaShop 存档桥接器。
    /// 负责把商店运行时快照转换为 NiumaSave 的 Section 数据，并在读档时恢复到商店控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaShopSaveAdapter : MonoBehaviour, ISaveDataProvider
    {
        private const string ShopSectionId = "shop";
        private const string ShopSectionVersionV1 = "1";
        private const string CurrentShopSectionVersion = ShopSectionVersionV1;
        private const string ShopSectionFormat = "json";

        [Header("模块引用")]
        [Tooltip("商店模块根控制器。请拖入场景中的 NiumaShopController，导出和导入商店进度都会通过它完成。")]
        [SerializeField] private NiumaShopController shopController;

        [Tooltip("存档模块根控制器。开启自动注册时，请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Header("注册行为")]
        [Tooltip("启用组件时是否自动注册到 NiumaSaveController。正式场景建议开启，并确保 NiumaSaveController 更早初始化，或把本组件挂在存档控制器子物体下。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("引用为空时是否自动在场景中查找对应组件。仅建议调试阶段开启；正式多场景或 DontDestroyOnLoad 场景必须手动绑定，避免找到错误实例。")]
        [SerializeField] private bool autoFindReferences = true;

        [Header("导入行为")]
        [Tooltip("导入完成后是否打印配置迁移警告。旧存档中的商店或商品配置被删除时，ShopService 会记录这些警告。")]
        [SerializeField] private bool logMigrationWarningsAfterImport = true;

        private bool _registeredToSaveController;

        /// <summary>
        /// 商店模块的稳定存档段 ID。
        /// </summary>
        public string SectionId => ShopSectionId;

        /// <summary>
        /// 商店存档段结构版本。
        /// </summary>
        public string SectionVersion => CurrentShopSectionVersion;

        /// <summary>
        /// 商店数据修订号。
        /// NiumaShop 使用 long Revision，NiumaSave 也使用 long Provider Revision，避免折叠碰撞导致脏标记漏检。
        /// </summary>
        public long Revision => shopController != null ? shopController.ShopRevision : 0L;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            if (registerOnEnable)
            {
                RegisterToSaveController();
            }
        }

        private void OnDisable()
        {
            UnregisterFromSaveController();
        }

        /// <summary>
        /// 导出商店运行时进度为 NiumaSave Section。
        /// 通过 SaveDataProviderRegistry 批量导出时，外层会捕获导出异常并转为结构化失败结果。
        /// 若外部直接调用该方法，必须自行处理 InvalidOperationException，避免缺少引用时打断完整存档流程。
        /// </summary>
        public SaveSectionData ExportSection()
        {
            ResolveReferences(false);
            if (shopController == null)
            {
                throw new InvalidOperationException("NiumaShopSaveAdapter 缺少 NiumaShopController，无法导出商店存档。");
            }

            var saveData = new ShopSaveData
            {
                Version = 1,
                Revision = shopController.ShopRevision,
                Shops = shopController.ExportSnapshots() ?? Array.Empty<ShopProgressSnapshot>()
            };
            ValidateSaveDataForExport(saveData);

            var json = JsonUtility.ToJson(saveData);
            var bytes = Encoding.UTF8.GetBytes(json);

            return new SaveSectionData
            {
                SectionId = SectionId,
                SectionVersion = SectionVersion,
                Format = ShopSectionFormat,
                DataEncoding = SaveDataEncoding.Base64,
                EncodedData = Convert.ToBase64String(bytes)
            };
        }

        /// <summary>
        /// 从 NiumaSave Section 导入商店进度快照。
        /// </summary>
        public SaveSectionImportResult ImportSection(SaveSectionData section)
        {
            ResolveReferences(false);
            if (shopController == null)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.ConfigMissing,
                    "NiumaShopSaveAdapter 缺少 NiumaShopController，无法导入商店存档。");
            }

            if (section == null)
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.NullSection, "商店存档段为空。");
            }

            if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.SectionIdMismatch,
                    $"商店存档段 ID 不匹配：expected={SectionId}, actual={section.SectionId}");
            }

            if (!string.Equals(section.Format, ShopSectionFormat, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"商店存档段格式不支持：{section.Format}");
            }

            if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"商店存档段编码不支持：{section.DataEncoding}");
            }

            if (string.IsNullOrWhiteSpace(section.EncodedData))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "商店存档段数据为空。");
            }

            try
            {
                var readResult = TryReadShopSaveData(section, out var saveData);
                if (!readResult.Succeeded)
                {
                    return readResult;
                }

                if (!shopController.ImportSaveData(saveData))
                {
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.ImportFailed,
                        "商店控制器拒绝导入商店存档数据。");
                }

                LogMigrationWarningsAfterImport();
                return SaveSectionImportResult.Success();
            }
            catch (Exception ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"商店存档段解析失败：{ex.Message}");
            }
        }

        private static SaveSectionImportResult TryReadShopSaveData(SaveSectionData section, out ShopSaveData saveData)
        {
            saveData = null;
            switch (section.SectionVersion)
            {
                case ShopSectionVersionV1:
                    return TryReadVersion1(section, out saveData);
                default:
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.VersionUnsupported,
                        $"商店存档段版本不支持：{section.SectionVersion}");
            }
        }

        private static SaveSectionImportResult TryReadVersion1(SaveSectionData section, out ShopSaveData saveData)
        {
            saveData = null;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(section.EncodedData);
            }
            catch (FormatException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"商店存档段 Base64 解码失败：{ex.Message}");
            }

            string json;
            try
            {
                json = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"商店存档段 UTF8 解码失败：{ex.Message}");
            }

            saveData = JsonUtility.FromJson<ShopSaveData>(json);
            return ValidateImportedSaveData(saveData);
        }

        [ContextMenu("NiumaShopSave/注册到存档模块")]
        private void RegisterToSaveController()
        {
            if (_registeredToSaveController)
            {
                return;
            }

            ResolveReferences(true);
            if (saveController == null)
            {
                return;
            }

            var registered = saveController.RegisterProvider(this);
            _registeredToSaveController = registered;
            if (!registered)
            {
                Debug.LogWarning("[NiumaShopSaveAdapter] 注册商店存档 Provider 失败。", this);
            }
        }

        [ContextMenu("NiumaShopSave/从存档模块取消注册")]
        private void UnregisterFromSaveController()
        {
            ResolveReferences(false);
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        private void LogMigrationWarningsAfterImport()
        {
            if (!logMigrationWarningsAfterImport || shopController == null)
            {
                return;
            }

            var warnings = shopController.GetMigrationWarnings();
            for (var i = 0; warnings != null && i < warnings.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(warnings[i]))
                {
                    Debug.LogWarning($"[NiumaShopSaveAdapter] 商店存档迁移警告：{warnings[i]}", this);
                }
            }
        }

        private void ResolveReferences(bool logMissing)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (shopController == null)
            {
#if UNITY_2023_1_OR_NEWER
                shopController = FindFirstObjectByType<NiumaShopController>();
#else
                shopController = FindObjectOfType<NiumaShopController>();
#endif
            }

            if (saveController == null)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            if (logMissing && shopController == null)
            {
                Debug.LogWarning("[NiumaShopSaveAdapter] 未找到 NiumaShopController，请在 Inspector 中绑定。", this);
            }

            if (logMissing && saveController == null)
            {
                Debug.LogWarning("[NiumaShopSaveAdapter] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
            }
        }

        private static void ValidateSaveDataForExport(ShopSaveData saveData)
        {
            var error = ValidateSaveData(saveData);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"商店存档导出数据无效：{error}");
            }
        }

        private static SaveSectionImportResult ValidateImportedSaveData(ShopSaveData saveData)
        {
            var error = ValidateSaveData(saveData);
            return string.IsNullOrWhiteSpace(error)
                ? SaveSectionImportResult.Success()
                : SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, $"商店存档段数据无效：{error}");
        }

        private static string ValidateSaveData(ShopSaveData saveData)
        {
            if (saveData == null)
            {
                return "解析结果为空。";
            }

            if (saveData.Version != 1)
            {
                return $"版本字段无效：{saveData.Version}";
            }

            if (saveData.Revision < 0L)
            {
                return $"Revision 不能为负数：{saveData.Revision}";
            }

            if (saveData.Shops == null)
            {
                return "Shops 字段为空引用。";
            }

            var shopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < saveData.Shops.Length; i++)
            {
                var shop = saveData.Shops[i];
                if (shop == null)
                {
                    return $"Shops[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(shop.ShopId))
                {
                    return $"Shops[{i}].ShopId 为空。";
                }

                if (!shopIds.Add(shop.ShopId))
                {
                    return $"重复 ShopId：{shop.ShopId}";
                }

                if (shop.Revision < 0L)
                {
                    return $"ShopId={shop.ShopId} 的 Revision 不能为负数：{shop.Revision}";
                }

                if (shop.Products == null)
                {
                    return $"ShopId={shop.ShopId} 的 Products 字段为空引用。";
                }

                var productIds = new HashSet<string>(StringComparer.Ordinal);
                for (var j = 0; j < shop.Products.Length; j++)
                {
                    var product = shop.Products[j];
                    if (product == null)
                    {
                        return $"ShopId={shop.ShopId} 的 Products[{j}] 为空。";
                    }

                    if (string.IsNullOrWhiteSpace(product.ProductId))
                    {
                        return $"ShopId={shop.ShopId} 的 Products[{j}].ProductId 为空。";
                    }

                    if (!productIds.Add(product.ProductId))
                    {
                        return $"ShopId={shop.ShopId} 存在重复 ProductId：{product.ProductId}";
                    }

                    if (product.RemainingStock < -1)
                    {
                        return $"ShopId={shop.ShopId}, ProductId={product.ProductId} 的 RemainingStock 无效：{product.RemainingStock}";
                    }

                    if (product.PurchasedCount < 0)
                    {
                        return $"ShopId={shop.ShopId}, ProductId={product.ProductId} 的 PurchasedCount 不能为负数：{product.PurchasedCount}";
                    }
                }
            }

            return null;
        }
    }
}
