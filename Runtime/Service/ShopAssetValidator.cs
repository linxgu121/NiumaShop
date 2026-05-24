using System;
using System.Collections.Generic;
using NiumaInventory.Config;
using NiumaShop.Config;
using NiumaShop.Enum;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店配置资产校验器。
    /// 只检查 ShopAsset 静态配置，不读取玩家运行时购买进度。
    /// </summary>
    public static class ShopAssetValidator
    {
        /// <summary>
        /// 校验一组商店配置。
        /// itemDefinitions 为空时会跳过 ItemId 是否存在的校验，方便早期只检查商店结构。
        /// </summary>
        public static ShopAssetValidationReport Validate(
            IEnumerable<ShopAsset> shopAssets,
            IEnumerable<ItemDefinition> itemDefinitions = null)
        {
            return ValidateKnownItemIds(shopAssets, BuildItemIdSet(itemDefinitions));
        }

        /// <summary>
        /// 校验一组商店配置。
        /// knownItemIds 由外部提供，适合编辑器工具或后续服务器配置导入流程。
        /// </summary>
        public static ShopAssetValidationReport ValidateKnownItemIds(
            IEnumerable<ShopAsset> shopAssets,
            IEnumerable<string> knownItemIds)
        {
            var report = new ShopAssetValidationReport();
            var knownItemIdSet = BuildStringSet(knownItemIds);
            var shopIds = new Dictionary<string, ShopAsset>(StringComparer.Ordinal);

            if (shopAssets == null)
            {
                report.AddWarning(null, null, "ShopAsset", "未传入任何商店配置。", null);
                return report;
            }

            foreach (var shop in shopAssets)
            {
                if (shop == null)
                {
                    report.AddWarning(null, null, "ShopAsset", "商店配置数组中存在空引用，已跳过。", null);
                    continue;
                }

                ValidateShopId(shop, shopIds, report);
                ValidateShop(shop, knownItemIdSet, report);
            }

            return report;
        }

        private static void ValidateShopId(
            ShopAsset shop,
            Dictionary<string, ShopAsset> shopIds,
            ShopAssetValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(shop.ShopId))
            {
                report.AddError(null, null, nameof(ShopAsset.ShopId), "ShopId 不能为空。", shop);
                return;
            }

            if (shopIds.TryGetValue(shop.ShopId, out var existing))
            {
                report.AddError(
                    shop.ShopId,
                    null,
                    nameof(ShopAsset.ShopId),
                    $"ShopId 重复：{shop.ShopId}，已存在于 {GetAssetName(existing)}。后续重复商店不能覆盖先注册商店。",
                    shop,
                    true);
                return;
            }

            shopIds.Add(shop.ShopId, shop);
        }

        private static void ValidateShop(
            ShopAsset shop,
            HashSet<string> knownItemIds,
            ShopAssetValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(shop.DisplayName))
            {
                report.AddWarning(shop.ShopId, null, nameof(ShopAsset.DisplayName), "DisplayName 为空，UI 会退回显示 ShopId 或空文本。", shop);
            }

            ValidateConditions(shop.ShopId, null, nameof(ShopAsset.OpenConditions), shop.OpenConditions, knownItemIds, report, shop);
            ValidateProducts(shop, knownItemIds, report);
            ValidateDiscounts(shop, report);
        }

        private static void ValidateProducts(
            ShopAsset shop,
            HashSet<string> knownItemIds,
            ShopAssetValidationReport report)
        {
            if (shop.Products == null || shop.Products.Length == 0)
            {
                report.AddWarning(shop.ShopId, null, nameof(ShopAsset.Products), "商店没有任何商品。空商店可以用于剧情占位，但正式内容需要确认。", shop);
                return;
            }

            var productIds = new HashSet<string>(StringComparer.Ordinal);
            var itemOwnerById = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < shop.Products.Length; i++)
            {
                var product = shop.Products[i];
                var fieldPrefix = $"{nameof(ShopAsset.Products)}[{i}]";
                if (product == null)
                {
                    report.AddError(shop.ShopId, null, fieldPrefix, "商品配置为空。", shop);
                    continue;
                }

                ValidateProductId(shop, product, productIds, fieldPrefix, report);
                ValidateProductItem(shop, product, knownItemIds, fieldPrefix, itemOwnerById, report);
                ValidateProductRules(shop, product, fieldPrefix, report);
                ValidatePrices(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.Prices), product.Prices, knownItemIds, report, shop);
                ValidateConditions(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.BuyConditions), product.BuyConditions, knownItemIds, report, shop);
                ValidateTags(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.Tags), product.Tags, report, shop);
            }
        }

        private static void ValidateProductId(
            ShopAsset shop,
            ShopProductData product,
            HashSet<string> productIds,
            string fieldPrefix,
            ShopAssetValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(product.ProductId))
            {
                report.AddError(shop.ShopId, null, fieldPrefix + "." + nameof(ShopProductData.ProductId), "ProductId 不能为空。", shop);
                return;
            }

            if (!productIds.Add(product.ProductId))
            {
                report.AddError(
                    shop.ShopId,
                    product.ProductId,
                    fieldPrefix + "." + nameof(ShopProductData.ProductId),
                    $"同一商店内 ProductId 重复：{product.ProductId}。",
                    shop);
            }
        }

        private static void ValidateProductItem(
            ShopAsset shop,
            ShopProductData product,
            HashSet<string> knownItemIds,
            string fieldPrefix,
            Dictionary<string, string> itemOwnerById,
            ShopAssetValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(product.ItemId))
            {
                report.AddError(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.ItemId), "商品 ItemId 不能为空。", shop);
            }
            else
            {
                if (knownItemIds != null && !knownItemIds.Contains(product.ItemId))
                {
                    report.AddError(
                        shop.ShopId,
                        product.ProductId,
                        fieldPrefix + "." + nameof(ShopProductData.ItemId),
                        $"商品 ItemId 不存在于 ItemDefinition：{product.ItemId}。",
                        shop);
                }

                if (itemOwnerById.TryGetValue(product.ItemId, out var existingProductId))
                {
                    report.AddWarning(
                        shop.ShopId,
                        product.ProductId,
                        fieldPrefix + "." + nameof(ShopProductData.ItemId),
                        $"同一商店内多个商品使用同一个 ItemId：{product.ItemId}，已有商品 {existingProductId}。如果是不同价格/限购版本请保留，否则建议合并。",
                        shop);
                }
                else
                {
                    itemOwnerById.Add(product.ItemId, product.ProductId);
                }
            }

            if (product.Count <= 0)
            {
                report.AddError(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.Count), "商品数量 Count 必须大于 0。", shop);
            }
        }

        private static void ValidateProductRules(
            ShopAsset shop,
            ShopProductData product,
            string fieldPrefix,
            ShopAssetValidationReport report)
        {
            if (product.InitialStock < -1)
            {
                report.AddError(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.InitialStock), "InitialStock 不能小于 -1。-1 表示无限库存。", shop);
            }

            if (product.MaxPurchaseCount < -1)
            {
                report.AddError(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.MaxPurchaseCount), "MaxPurchaseCount 不能小于 -1。-1 表示不限购。", shop);
            }

            if (product.InitialStock == 0)
            {
                report.AddWarning(shop.ShopId, product.ProductId, fieldPrefix + "." + nameof(ShopProductData.InitialStock), "商品初始库存为 0，开局即售罄。", shop);
            }

            if (product.OneShot && product.InitialStock > 1)
            {
                report.AddWarning(
                    shop.ShopId,
                    product.ProductId,
                    fieldPrefix + "." + nameof(ShopProductData.OneShot),
                    "OneShot 商品首次购买成功后会直接永久售罄，InitialStock > 1 不会继续参与扣减，请确认这是有意配置。",
                    shop);
            }
        }

        private static void ValidatePrices(
            string shopId,
            string productId,
            string fieldPrefix,
            ShopPriceData[] prices,
            HashSet<string> knownItemIds,
            ShopAssetValidationReport report,
            UnityEngine.Object context)
        {
            if (prices == null || prices.Length == 0)
            {
                report.AddWarning(shopId, productId, fieldPrefix, "商品没有价格配置。如果不是免费赠品，请补充价格；免费商品也建议显式配置 Amount=0。", context);
                return;
            }

            var allFree = true;
            var currencyIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                var pricePath = $"{fieldPrefix}[{i}]";
                if (price == null)
                {
                    report.AddError(shopId, productId, pricePath, "价格配置为空。", context);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(price.CurrencyItemId))
                {
                    report.AddError(shopId, productId, pricePath + "." + nameof(ShopPriceData.CurrencyItemId), "CurrencyItemId 不能为空。", context);
                }
                else
                {
                    if (!currencyIds.Add(price.CurrencyItemId))
                    {
                        report.AddWarning(shopId, productId, pricePath + "." + nameof(ShopPriceData.CurrencyItemId), $"同一商品重复配置同一种货币：{price.CurrencyItemId}。", context);
                    }

                    if (knownItemIds != null && !knownItemIds.Contains(price.CurrencyItemId))
                    {
                        report.AddError(
                            shopId,
                            productId,
                            pricePath + "." + nameof(ShopPriceData.CurrencyItemId),
                            $"货币 ItemId 不存在于 ItemDefinition：{price.CurrencyItemId}。",
                            context);
                    }
                }

                if (price.Amount < 0)
                {
                    report.AddError(shopId, productId, pricePath + "." + nameof(ShopPriceData.Amount), "价格 Amount 不能小于 0。", context);
                }
                else if (price.Amount > 0)
                {
                    allFree = false;
                }
            }

            if (allFree)
            {
                report.AddWarning(shopId, productId, fieldPrefix, "该商品所有价格 Amount 都为 0，会被视为免费商品，请确认这是有意配置。", context);
            }
        }

        private static void ValidateDiscounts(
            ShopAsset shop,
            ShopAssetValidationReport report)
        {
            if (shop.DefaultDiscounts == null || shop.DefaultDiscounts.Length == 0)
            {
                return;
            }

            var discountIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < shop.DefaultDiscounts.Length; i++)
            {
                var discount = shop.DefaultDiscounts[i];
                var fieldPrefix = $"{nameof(ShopAsset.DefaultDiscounts)}[{i}]";
                if (discount == null)
                {
                    report.AddError(shop.ShopId, null, fieldPrefix, "折扣配置为空。", shop);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(discount.DiscountId))
                {
                    report.AddWarning(shop.ShopId, null, fieldPrefix + "." + nameof(ShopDiscountData.DiscountId), "DiscountId 为空，后续剧情/任务切换折扣时无法稳定定位。", shop);
                }
                else if (!discountIds.Add(discount.DiscountId))
                {
                    report.AddError(shop.ShopId, null, fieldPrefix + "." + nameof(ShopDiscountData.DiscountId), $"同一商店内 DiscountId 重复：{discount.DiscountId}。", shop);
                }

                if (discount.PriceMultiplier < 0f)
                {
                    report.AddError(shop.ShopId, null, fieldPrefix + "." + nameof(ShopDiscountData.PriceMultiplier), "PriceMultiplier 不能小于 0。", shop);
                }
                else if (discount.PriceMultiplier == 0f)
                {
                    report.AddWarning(shop.ShopId, null, fieldPrefix + "." + nameof(ShopDiscountData.PriceMultiplier), "PriceMultiplier 为 0，该折扣会让适用商品免费。", shop);
                }

                ValidateTags(shop.ShopId, null, fieldPrefix + "." + nameof(ShopDiscountData.ProductTags), discount.ProductTags, report, shop);
                ValidateConditions(shop.ShopId, null, fieldPrefix + "." + nameof(ShopDiscountData.Conditions), discount.Conditions, null, report, shop);
            }
        }

        private static void ValidateConditions(
            string shopId,
            string productId,
            string fieldPrefix,
            ShopConditionData[] conditions,
            HashSet<string> knownItemIds,
            ShopAssetValidationReport report,
            UnityEngine.Object context)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return;
            }

            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                var conditionPath = $"{fieldPrefix}[{i}]";
                if (condition == null)
                {
                    report.AddError(shopId, productId, conditionPath, "条件配置为空。", context);
                    continue;
                }

                ValidateCondition(shopId, productId, conditionPath, condition, knownItemIds, report, context);
            }
        }

        private static void ValidateCondition(
            string shopId,
            string productId,
            string fieldPath,
            ShopConditionData condition,
            HashSet<string> knownItemIds,
            ShopAssetValidationReport report,
            UnityEngine.Object context)
        {
            switch (condition.ConditionType)
            {
                case ShopConditionType.None:
                    if (!string.IsNullOrWhiteSpace(condition.TargetId) || condition.Invert)
                    {
                        report.AddWarning(shopId, productId, fieldPath, "条件类型为 None，但填写了目标或反转开关，这些配置不会产生业务效果。", context);
                    }
                    break;

                case ShopConditionType.HasItem:
                    RequireTargetId(shopId, productId, fieldPath, condition, report, context);
                    if (condition.RequiredCount <= 0)
                    {
                        report.AddError(shopId, productId, fieldPath + "." + nameof(ShopConditionData.RequiredCount), "HasItem 条件的 RequiredCount 必须大于 0。", context);
                    }

                    if (!string.IsNullOrWhiteSpace(condition.TargetId)
                        && knownItemIds != null
                        && !knownItemIds.Contains(condition.TargetId))
                    {
                        report.AddError(shopId, productId, fieldPath + "." + nameof(ShopConditionData.TargetId), $"条件目标 ItemId 不存在于 ItemDefinition：{condition.TargetId}。", context);
                    }
                    break;

                case ShopConditionType.ShopUnlocked:
                case ShopConditionType.ProductUnlocked:
                case ShopConditionType.QuestCompleted:
                case ShopConditionType.StoryFlag:
                case ShopConditionType.ReputationReached:
                case ShopConditionType.ReputationRange:
                case ShopConditionType.PlayerLevelRange:
                case ShopConditionType.External:
                    RequireTargetId(shopId, productId, fieldPath, condition, report, context);
                    break;

                case ShopConditionType.TimeRange:
                    break;

                default:
                    report.AddWarning(shopId, productId, fieldPath + "." + nameof(ShopConditionData.ConditionType), $"未识别的条件类型：{condition.ConditionType}，后续将交给外部解析器或被判定失败。", context);
                    break;
            }

            if ((condition.ConditionType == ShopConditionType.ReputationRange
                 || condition.ConditionType == ShopConditionType.PlayerLevelRange)
                && condition.MaxValue != 0
                && condition.MaxValue < condition.MinValue)
            {
                report.AddError(shopId, productId, fieldPath, "条件范围错误：MaxValue 小于 MinValue。", context);
            }

            if (condition.ConditionType == ShopConditionType.TimeRange
                && condition.StartUnixSeconds > 0
                && condition.EndUnixSeconds > 0
                && condition.EndUnixSeconds < condition.StartUnixSeconds)
            {
                report.AddError(shopId, productId, fieldPath, "TimeRange 条件错误：EndUnixSeconds 早于 StartUnixSeconds。", context);
            }

            ValidateConditionParameters(shopId, productId, fieldPath + "." + nameof(ShopConditionData.Parameters), condition.Parameters, report, context);
        }

        private static void RequireTargetId(
            string shopId,
            string productId,
            string fieldPath,
            ShopConditionData condition,
            ShopAssetValidationReport report,
            UnityEngine.Object context)
        {
            if (string.IsNullOrWhiteSpace(condition.TargetId))
            {
                report.AddError(shopId, productId, fieldPath + "." + nameof(ShopConditionData.TargetId), $"{condition.ConditionType} 条件必须填写 TargetId。", context);
            }
        }

        private static void ValidateConditionParameters(
            string shopId,
            string productId,
            string fieldPrefix,
            ShopConditionParameterData[] parameters,
            ShopAssetValidationReport report,
            UnityEngine.Object context)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var parameterPath = $"{fieldPrefix}[{i}]";
                if (parameter == null)
                {
                    report.AddWarning(shopId, productId, parameterPath, "条件扩展参数为空。", context);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parameter.Key))
                {
                    report.AddWarning(shopId, productId, parameterPath + "." + nameof(ShopConditionParameterData.Key), "条件扩展参数 Key 为空，外部解析器无法稳定读取。", context);
                }
                else if (!keys.Add(parameter.Key))
                {
                    report.AddWarning(shopId, productId, parameterPath + "." + nameof(ShopConditionParameterData.Key), $"条件扩展参数 Key 重复：{parameter.Key}。", context);
                }
            }
        }

        private static void ValidateTags(
            string shopId,
            string productId,
            string fieldPrefix,
            string[] tags,
            ShopAssetValidationReport report,
            UnityEngine.Object context)
        {
            if (tags == null || tags.Length == 0)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                var tagPath = $"{fieldPrefix}[{i}]";
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.AddWarning(shopId, productId, tagPath, "标签为空。", context);
                    continue;
                }

                if (!seen.Add(tag))
                {
                    report.AddWarning(shopId, productId, tagPath, $"标签重复：{tag}。", context);
                }

                if (!string.Equals(tag, tag.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    report.AddWarning(shopId, productId, tagPath, $"标签建议统一小写，当前值：{tag}。", context);
                }
            }
        }

        private static HashSet<string> BuildItemIdSet(IEnumerable<ItemDefinition> itemDefinitions)
        {
            if (itemDefinitions == null)
            {
                return null;
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in itemDefinitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    continue;
                }

                result.Add(definition.ItemId);
            }

            return result;
        }

        private static HashSet<string> BuildStringSet(IEnumerable<string> values)
        {
            if (values == null)
            {
                return null;
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                result.Add(value);
            }

            return result;
        }

        private static string GetAssetName(UnityEngine.Object asset)
        {
            return asset != null ? asset.name : "null";
        }
    }
}
