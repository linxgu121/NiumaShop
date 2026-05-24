using System;
using System.Collections.Generic;
using NiumaInventory.Config;
using NiumaShop.Config;
using UnityEngine;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店配置注册表。
    /// 负责用稳定 ShopId 建立静态配置索引，并在注册时输出结构化校验报告。
    /// </summary>
    public sealed class ShopRegistry
    {
        private readonly Dictionary<string, ShopAsset> _shops =
            new Dictionary<string, ShopAsset>(StringComparer.Ordinal);
        private readonly List<ShopAsset> _shopList = new List<ShopAsset>();

        private ShopAssetValidationReport _lastReport = new ShopAssetValidationReport();

        /// <summary>
        /// 当前已注册商店数量。
        /// </summary>
        public int Count => _shops.Count;

        /// <summary>
        /// 是否启用严格模式。
        /// 严格模式下只要校验存在错误，注册表就会清空并判定为不可用。
        /// </summary>
        public bool StrictMode { get; private set; }

        /// <summary>
        /// 最近一次重建索引时生成的校验报告。
        /// </summary>
        public ShopAssetValidationReport LastReport => _lastReport;

        /// <summary>
        /// 当前注册表是否可用于正式流程。
        /// </summary>
        public bool IsUsable => !StrictMode || (_lastReport != null && _lastReport.IsValid);

        public ShopRegistry(
            IEnumerable<ShopAsset> shopAssets = null,
            IEnumerable<ItemDefinition> itemDefinitions = null,
            bool strictMode = false)
        {
            SetShops(shopAssets, itemDefinitions, strictMode);
        }

        /// <summary>
        /// 重建商店索引。
        /// 重复 ShopId 保留第一次出现的配置，后续重复配置会记录错误并跳过，避免存档中的 ShopId 指向不确定配置。
        /// </summary>
        public ShopAssetValidationReport SetShops(
            IEnumerable<ShopAsset> shopAssets,
            IEnumerable<ItemDefinition> itemDefinitions = null,
            bool strictMode = false)
        {
            StrictMode = strictMode;
            _shops.Clear();
            _shopList.Clear();
            _lastReport = ShopAssetValidator.Validate(shopAssets, itemDefinitions);

            if (shopAssets == null)
            {
                return _lastReport;
            }

            foreach (var shop in shopAssets)
            {
                if (shop == null || string.IsNullOrWhiteSpace(shop.ShopId))
                {
                    continue;
                }

                if (_shops.ContainsKey(shop.ShopId))
                {
                    Debug.LogWarning($"[NiumaShop] 发现重复 ShopId：{shop.ShopId}，后续重复商店已跳过。", shop);
                    continue;
                }

                _shops.Add(shop.ShopId, shop);
                _shopList.Add(shop);
            }

            if (StrictMode && _lastReport.HasErrors)
            {
                _shops.Clear();
                _shopList.Clear();
            }

            return _lastReport;
        }

        /// <summary>
        /// 尝试获取指定商店配置。
        /// 返回静态配置引用，调用方不得在运行时修改它。
        /// </summary>
        public bool TryGetShop(string shopId, out ShopAsset shop)
        {
            shop = null;
            return IsUsable
                   && !string.IsNullOrWhiteSpace(shopId)
                   && _shops.TryGetValue(shopId, out shop)
                   && shop != null;
        }

        /// <summary>
        /// 是否存在指定商店。
        /// </summary>
        public bool ContainsShop(string shopId)
        {
            return TryGetShop(shopId, out _);
        }

        /// <summary>
        /// 尝试获取指定商品配置。
        /// </summary>
        public bool TryGetProduct(string shopId, string productId, out ShopProductData product)
        {
            product = null;
            return TryGetShop(shopId, out var shop)
                   && TryGetProduct(shop, productId, out product);
        }

        /// <summary>
        /// 尝试从指定商店配置中获取商品配置。
        /// </summary>
        public static bool TryGetProduct(ShopAsset shop, string productId, out ShopProductData product)
        {
            product = null;
            if (shop == null || shop.Products == null || string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            for (var i = 0; i < shop.Products.Length; i++)
            {
                var candidate = shop.Products[i];
                if (candidate != null && string.Equals(candidate.ProductId, productId, StringComparison.Ordinal))
                {
                    product = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取所有已注册商店。
        /// 返回新数组，避免外部修改内部缓存。
        /// </summary>
        public ShopAsset[] GetAllShops()
        {
            return _shopList.ToArray();
        }

        /// <summary>
        /// 复制所有已注册商店到调用方缓存列表。
        /// </summary>
        public void CopyShops(List<ShopAsset> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            output.AddRange(_shopList);
        }

        /// <summary>
        /// 获取所有默认开放的商店 ID。
        /// </summary>
        public string[] GetDefaultUnlockedShopIds()
        {
            if (_shopList.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            for (var i = 0; i < _shopList.Count; i++)
            {
                var shop = _shopList[i];
                if (shop != null && shop.DefaultUnlocked && !string.IsNullOrWhiteSpace(shop.ShopId))
                {
                    result.Add(shop.ShopId);
                }
            }

            return result.ToArray();
        }
    }
}
