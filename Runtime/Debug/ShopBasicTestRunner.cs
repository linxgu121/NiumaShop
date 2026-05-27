using System;
using System.Collections.Generic;
using NiumaInventory.Config;
using NiumaInventory.Data;
using NiumaInventory.Enum;
using NiumaInventory.Request;
using NiumaInventory.Service;
using NiumaShop.Config;
using NiumaShop.Data;
using NiumaShop.Enum;
using NiumaShop.Request;
using NiumaShop.Result;
using NiumaShop.Service;
using UnityEngine;

namespace NiumaShop.Debugging
{
    /// <summary>
    /// 商店模块基础测试入口。
    /// 该组件只用于开发阶段在 Unity 场景内手动验证购买、库存、限购和存档闭环，不参与正式业务。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopBasicTestRunner : MonoBehaviour
    {
        private const string MainContainerId = "main";
        private const string CoinItemId = "test_coin";
        private const string PotionItemId = "test_potion";
        private const string ScrollItemId = "test_scroll";
        private const string FillerItemId = "test_filler";
        private const string GateItemId = "test_gate_item";

        [Header("测试行为")]
        [Tooltip("运行测试后是否在 Console 输出每一步通过信息。关闭后只输出最终结果和失败原因。")]
        [SerializeField] private bool verboseLog = true;

        [ContextMenu("NiumaShopTest/运行基础测试")]
        public void RunBasicTests()
        {
            var createdAssets = new List<ScriptableObject>();
            var failures = new List<string>();

            try
            {
                var definitions = CreateTestDefinitions(createdAssets);
                var mainContainer = CreateMainContainer(createdAssets, 8);

                TestNormalPurchase(definitions, mainContainer, failures);
                TestDiscountCalculation(definitions, mainContainer, failures);
                TestDiscountCanBeDisabled(definitions, mainContainer, failures);
                TestMarkupMultiplier(definitions, mainContainer, failures);
                TestInsufficientCurrency(definitions, mainContainer, failures);
                TestInventoryCapacityRejected(definitions, failures);
                TestInfiniteStock(definitions, mainContainer, failures);
                TestPurchaseLimit(definitions, mainContainer, failures);
                TestStockAndLimitMaxPurchasable(definitions, mainContainer, failures);
                TestOneShotSoldOut(definitions, mainContainer, failures);
                TestTransactionReentryGuard(definitions, mainContainer, failures);
                TestMultipleFailureReasons(definitions, mainContainer, failures);
                TestProductAndShopLocked(definitions, mainContainer, failures);
                TestExportImportProgress(definitions, mainContainer, failures);

                if (failures.Count == 0)
                {
                    Debug.Log("[NiumaShopTest] 基础测试通过：正常购买、折扣计算、折扣关闭、恢复默认折扣、涨价倍率、货币不足、背包满、库存、限购、OneShot、防重入、多失败原因、锁定拦截、导出导入均正常。", this);
                    return;
                }

                Debug.LogError("[NiumaShopTest] 基础测试失败：\n" + string.Join("\n", failures), this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NiumaShopTest] 基础测试发生异常：{ex}", this);
            }
            finally
            {
                ReleaseCreatedAssets(createdAssets);
            }
        }

        private void TestNormalPurchase(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("正常购买测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 100)), failures);

            var shop = CreateShop("shop_normal", CreateProduct("potion_normal", PotionItemId, price: 10, initialStock: 5));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var result = service.BuyProduct(new BuyProductRequest("shop_normal", "potion_normal", 2, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopSuccess("正常购买成功", result, failures);
            ExpectEqual("正常购买后货币扣减", 80, inventory.GetItemCount(CoinItemId), failures);
            ExpectEqual("正常购买后获得商品数量", 2, inventory.GetItemCount(PotionItemId), failures);
            ExpectProductState(service, "shop_normal", "potion_normal", 3, 2, ShopProductState.Unlocked, failures);

            DestroyImmediate(shop);
            LogStep("正常购买测试完成。");
        }

        private void TestInsufficientCurrency(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("货币不足测试添加少量货币", inventory.AddItem(CreateAddRequest(CoinItemId, 3)), failures);

            var shop = CreateShop("shop_money", CreateProduct("potion_money", PotionItemId, price: 10, initialStock: 5));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var result = service.BuyProduct(new BuyProductRequest("shop_money", "potion_money", 1, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopFailed("货币不足时购买失败", result, ShopFailureReason.InsufficientCurrency, failures);
            ExpectEqual("货币不足失败后货币不变", 3, inventory.GetItemCount(CoinItemId), failures);
            ExpectEqual("货币不足失败后不发商品", 0, inventory.GetItemCount(PotionItemId), failures);
            ExpectProductState(service, "shop_money", "potion_money", 5, 0, ShopProductState.Unlocked, failures);

            DestroyImmediate(shop);
            LogStep("货币不足测试完成。");
        }

        private void TestDiscountCalculation(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("折扣测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 5)), failures);

            var product = CreateProduct("potion_discount", PotionItemId, price: 10, initialStock: 5);
            product.Tags = new[] { "festival" };
            var shop = CreateShop("shop_discount", product);
            shop.DefaultDiscounts = new[]
            {
                CreateDiscount("discount_eighty", 0.8f),
                CreateDiscount("discount_half", 0.5f, productIds: new[] { "potion_discount" }),
                CreateDiscount("discount_tag", 0.6f, productTags: new[] { "festival" })
            };

            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var panel = service.BuildShopViewData("shop_discount");
            var viewPrice = panel.Products != null
                            && panel.Products.Length > 0
                            && panel.Products[0].Prices != null
                            && panel.Products[0].Prices.Length > 0
                ? panel.Products[0].Prices[0].Amount
                : -1;
            ExpectEqual("折扣预览取最低价", 5, viewPrice, failures);
            var viewPriceData = panel.Products != null
                                && panel.Products.Length > 0
                                && panel.Products[0].Prices != null
                                && panel.Products[0].Prices.Length > 0
                ? panel.Products[0].Prices[0]
                : null;
            ExpectEqual("折扣预览保留原价", 10, viewPriceData?.OriginalAmount ?? -1, failures);
            ExpectTrue("折扣预览标记价格修正", viewPriceData != null && viewPriceData.HasPriceModifier, failures);
            ExpectEqual("折扣预览记录折扣 ID", "discount_half", viewPriceData?.AppliedDiscountId, failures);

            var result = service.BuyProduct(new BuyProductRequest("shop_discount", "potion_discount", 1, MainContainerId, nameof(ShopBasicTestRunner)));
            ExpectShopSuccess("折扣购买成功", result, failures);
            ExpectEqual("折扣后扣除 5 个货币", 0, inventory.GetItemCount(CoinItemId), failures);
            ExpectEqual("折扣后获得商品", 1, inventory.GetItemCount(PotionItemId), failures);
            ExpectEqual("折扣结果记录支付价格", 5, result?.PaidPrices != null && result.PaidPrices.Length > 0 ? result.PaidPrices[0].Amount : -1, failures);

            DestroyImmediate(shop);
            LogStep("折扣计算测试完成。");
        }

        private void TestDiscountCanBeDisabled(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("折扣关闭测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 20)), failures);

            var shop = CreateShop("shop_discount_off", CreateProduct("potion_discount_off", PotionItemId, price: 10, initialStock: 5));
            shop.DefaultDiscounts = new[] { CreateDiscount("discount_half_off", 0.5f) };

            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var beforeDisable = service.BuildShopViewData("shop_discount_off");
            var beforePrice = beforeDisable.Products?[0]?.Prices?[0]?.Amount ?? -1;
            ExpectEqual("默认折扣初始生效", 5, beforePrice, failures);

            ExpectTrue("关闭商店折扣成功", service.SetActiveDiscounts(new SetShopDiscountsRequest("shop_discount_off", Array.Empty<string>(), nameof(ShopBasicTestRunner))), failures);
            var afterDisable = service.BuildShopViewData("shop_discount_off");
            var afterPrice = afterDisable.Products?[0]?.Prices?[0]?.Amount ?? -1;
            var afterPriceData = afterDisable.Products?[0]?.Prices?[0];
            ExpectEqual("折扣关闭后恢复原价", 10, afterPrice, failures);
            ExpectTrue("折扣关闭后 UI 不标记折扣", afterPriceData != null && !afterPriceData.HasPriceModifier, failures);

            ExpectTrue("恢复默认折扣成功", service.ResetDiscountsToDefault(new ResetShopDiscountsRequest("shop_discount_off", nameof(ShopBasicTestRunner))), failures);
            var afterReset = service.BuildShopViewData("shop_discount_off");
            var resetPrice = afterReset.Products?[0]?.Prices?[0]?.Amount ?? -1;
            ExpectEqual("恢复默认折扣后重新使用折扣价", 5, resetPrice, failures);

            ExpectTrue("全空白折扣输入也会关闭折扣", service.SetActiveDiscounts(new SetShopDiscountsRequest("shop_discount_off", new[] { string.Empty, "   " }, nameof(ShopBasicTestRunner))), failures);
            var afterWhitespaceDisable = service.BuildShopViewData("shop_discount_off");
            var whitespacePrice = afterWhitespaceDisable.Products?[0]?.Prices?[0]?.Amount ?? -1;
            ExpectEqual("全空白折扣输入关闭后恢复原价", 10, whitespacePrice, failures);

            DestroyImmediate(shop);
            LogStep("折扣关闭测试完成。");
        }

        private void TestMarkupMultiplier(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("涨价倍率测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 30)), failures);

            var shop = CreateShop("shop_markup", CreateProduct("potion_markup", PotionItemId, price: 10, initialStock: 5));
            shop.DefaultDiscounts = new[] { CreateDiscount("markup_festival", 1.2f) };

            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var panel = service.BuildShopViewData("shop_markup");
            var price = panel.Products?[0]?.Prices?[0];
            ExpectEqual("PriceMultiplier 大于 1 时允许涨价", 12, price?.Amount ?? -1, failures);
            ExpectEqual("涨价倍率保留原价", 10, price?.OriginalAmount ?? -1, failures);
            ExpectTrue("涨价倍率标记价格修正", price != null && price.HasPriceModifier, failures);

            DestroyImmediate(shop);
            LogStep("涨价倍率测试完成。");
        }

        private void TestInventoryCapacityRejected(
            ItemDefinition[] definitions,
            List<string> failures)
        {
            var smallContainer = CreateMainContainer(null, 1);
            var inventory = CreateInventory(definitions, smallContainer);
            ExpectInventorySuccess("背包满测试添加占位物品", inventory.AddItem(CreateAddRequest(FillerItemId, 1)), failures);

            var shop = CreateShop("shop_full", CreateProduct("potion_full", PotionItemId, price: 0, initialStock: 5));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var result = service.BuyProduct(new BuyProductRequest("shop_full", "potion_full", 1, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopFailed("背包空间不足时购买失败", result, ShopFailureReason.InventoryRejected, failures);
            ExpectEqual("背包满失败后不发商品", 0, inventory.GetItemCount(PotionItemId), failures);
            ExpectProductState(service, "shop_full", "potion_full", 5, 0, ShopProductState.Unlocked, failures);

            DestroyImmediate(shop);
            DestroyImmediate(smallContainer);
            LogStep("背包空间不足测试完成。");
        }

        private void TestInfiniteStock(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("无限库存测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 50)), failures);

            var shop = CreateShop("shop_infinite", CreateProduct("potion_infinite", PotionItemId, price: 5, initialStock: -1));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var first = service.BuyProduct(new BuyProductRequest("shop_infinite", "potion_infinite", 1, MainContainerId, nameof(ShopBasicTestRunner)));
            var second = service.BuyProduct(new BuyProductRequest("shop_infinite", "potion_infinite", 2, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopSuccess("无限库存第一次购买成功", first, failures);
            ExpectShopSuccess("无限库存第二次购买成功", second, failures);
            ExpectEqual("无限库存购买后商品数量", 3, inventory.GetItemCount(PotionItemId), failures);
            ExpectProductState(service, "shop_infinite", "potion_infinite", -1, 3, ShopProductState.Unlocked, failures);

            DestroyImmediate(shop);
            LogStep("无限库存测试完成。");
        }

        private void TestPurchaseLimit(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("限购测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 50)), failures);

            var shop = CreateShop("shop_limit", CreateProduct("potion_limit", PotionItemId, price: 5, initialStock: 5, maxPurchaseCount: 1));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var first = service.BuyProduct(new BuyProductRequest("shop_limit", "potion_limit", 1, MainContainerId, nameof(ShopBasicTestRunner)));
            var second = service.BuyProduct(new BuyProductRequest("shop_limit", "potion_limit", 1, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopSuccess("限购第一次购买成功", first, failures);
            ExpectShopFailed("达到限购后购买失败", second, ShopFailureReason.PurchaseLimitReached, failures);
            ExpectEqual("限购失败后只获得一次商品", 1, inventory.GetItemCount(PotionItemId), failures);
            ExpectProductState(service, "shop_limit", "potion_limit", 4, 1, ShopProductState.Unlocked, failures);

            DestroyImmediate(shop);
            LogStep("限购测试完成。");
        }

        private void TestStockAndLimitMaxPurchasable(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("最大可购买测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 100)), failures);

            var shop = CreateShop("shop_max", CreateProduct("potion_max", PotionItemId, price: 1, initialStock: 3, maxPurchaseCount: 5));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var panel = service.BuildShopViewData("shop_max");
            var product = panel.Products != null && panel.Products.Length > 0 ? panel.Products[0] : null;

            ExpectEqual("库存和限购同时存在时最大可购买取较小值", 3, product?.MaxPurchasableCount ?? -1, failures);

            DestroyImmediate(shop);
            LogStep("库存与限购共同计算测试完成。");
        }

        private void TestOneShotSoldOut(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("OneShot 测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 100)), failures);

            var shop = CreateShop("shop_oneshot", CreateProduct("scroll_oneshot", ScrollItemId, price: 5, initialStock: 100, oneShot: true));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var first = service.BuyProduct(new BuyProductRequest("shop_oneshot", "scroll_oneshot", 1, MainContainerId, nameof(ShopBasicTestRunner)));
            var second = service.BuyProduct(new BuyProductRequest("shop_oneshot", "scroll_oneshot", 1, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopSuccess("OneShot 首次购买成功", first, failures);
            ExpectShopFailed("OneShot 首次购买后售罄", second, ShopFailureReason.SoldOut, failures);
            ExpectProductState(service, "shop_oneshot", "scroll_oneshot", 0, 1, ShopProductState.SoldOut, failures);

            DestroyImmediate(shop);
            LogStep("OneShot 售罄测试完成。");
        }

        private void TestTransactionReentryGuard(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var innerInventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("防重入测试添加货币", innerInventory.AddItem(CreateAddRequest(CoinItemId, 100)), failures);

            var shop = CreateShop("shop_reentry", CreateProduct("potion_reentry", PotionItemId, price: 5, initialStock: 5));
            var reentrantInventory = new ReentrantInventoryService(innerInventory);
            var service = CreateShopService(new[] { shop }, definitions, reentrantInventory);
            var request = new BuyProductRequest("shop_reentry", "potion_reentry", 1, MainContainerId, nameof(ShopBasicTestRunner));
            reentrantInventory.Bind(service, request);

            var result = service.BuyProduct(request);

            ExpectShopSuccess("外层交易应正常完成", result, failures);
            ExpectTrue("重入交易被拦截", reentrantInventory.ReentrantResult != null
                                      && !reentrantInventory.ReentrantResult.Succeeded
                                      && reentrantInventory.ReentrantResult.HasFailure(ShopFailureReason.TransactionInProgress),
                failures);
            ExpectEqual("防重入后只购买一次商品", 1, innerInventory.GetItemCount(PotionItemId), failures);

            DestroyImmediate(shop);
            LogStep("同商品防重入测试完成。");
        }

        private void TestMultipleFailureReasons(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            var condition = new ShopConditionData
            {
                ConditionType = ShopConditionType.HasItem,
                TargetId = GateItemId,
                RequiredCount = 1
            };

            var shop = CreateShop(
                "shop_multi_fail",
                CreateProduct("potion_multi_fail", PotionItemId, price: 10, initialStock: 5, buyConditions: new[] { condition }));
            var service = CreateShopService(new[] { shop }, definitions, inventory);
            var result = service.BuyProduct(new BuyProductRequest("shop_multi_fail", "potion_multi_fail", 1, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopFailed("多失败原因时购买失败", result, ShopFailureReason.ConditionNotMet, failures);
            ExpectTrue("多失败原因包含条件未满足", result != null && result.HasFailure(ShopFailureReason.ConditionNotMet), failures);
            ExpectTrue("多失败原因包含货币不足", result != null && result.HasFailure(ShopFailureReason.InsufficientCurrency), failures);

            DestroyImmediate(shop);
            LogStep("多失败原因测试完成。");
        }

        private void TestProductAndShopLocked(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("锁定测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 100)), failures);

            var lockedShop = CreateShop("shop_locked", CreateProduct("potion_shop_locked", PotionItemId, price: 1, initialStock: 5));
            lockedShop.DefaultUnlocked = false;
            var lockedProductShop = CreateShop("shop_product_locked", CreateProduct("potion_product_locked", PotionItemId, price: 1, initialStock: 5, defaultUnlocked: false));
            var service = CreateShopService(new[] { lockedShop, lockedProductShop }, definitions, inventory);

            var shopLocked = service.BuyProduct(new BuyProductRequest("shop_locked", "potion_shop_locked", 1, MainContainerId, nameof(ShopBasicTestRunner)));
            var productLocked = service.BuyProduct(new BuyProductRequest("shop_product_locked", "potion_product_locked", 1, MainContainerId, nameof(ShopBasicTestRunner)));

            ExpectShopFailed("商店锁定时购买失败", shopLocked, ShopFailureReason.ShopLocked, failures);
            ExpectShopFailed("商品锁定时购买失败", productLocked, ShopFailureReason.ProductLocked, failures);
            ExpectEqual("锁定失败后不发商品", 0, inventory.GetItemCount(PotionItemId), failures);

            DestroyImmediate(lockedShop);
            DestroyImmediate(lockedProductShop);
            LogStep("商店与商品锁定测试完成。");
        }

        private void TestExportImportProgress(
            ItemDefinition[] definitions,
            InventoryContainerConfig mainContainer,
            List<string> failures)
        {
            var inventory = CreateInventory(definitions, mainContainer);
            ExpectInventorySuccess("导出导入测试添加货币", inventory.AddItem(CreateAddRequest(CoinItemId, 100)), failures);

            var shop = CreateShop("shop_save", CreateProduct("potion_save", PotionItemId, price: 5, initialStock: 5, maxPurchaseCount: 3));
            var source = CreateShopService(new[] { shop }, definitions, inventory);
            var buy = source.BuyProduct(new BuyProductRequest("shop_save", "potion_save", 2, MainContainerId, nameof(ShopBasicTestRunner)));
            ExpectShopSuccess("导出前购买成功", buy, failures);

            var saveData = new ShopSaveData
            {
                Revision = source.Revision,
                Shops = source.ExportSnapshots()
            };

            var imported = CreateShopService(new[] { shop }, definitions, CreateInventory(definitions, mainContainer));
            ExpectTrue("导入完整 ShopSaveData 成功", imported.ImportSaveData(saveData), failures);
            ExpectProductState(imported, "shop_save", "potion_save", 3, 2, ShopProductState.Unlocked, failures);
            ExpectEqual("导入后模块 Revision 继承存档", source.Revision, imported.Revision, failures);

            var beforeReject = imported.Revision;
            imported.ImportSnapshots(Array.Empty<ShopProgressSnapshot>());
            ExpectEqual("公共 ImportSnapshots 拒绝空集合，不清空旧进度", beforeReject, imported.Revision, failures);
            ExpectProductState(imported, "shop_save", "potion_save", 3, 2, ShopProductState.Unlocked, failures);

            DestroyImmediate(shop);
            LogStep("导出导入测试完成。");
        }

        private static ItemDefinition[] CreateTestDefinitions(List<ScriptableObject> createdAssets)
        {
            return new[]
            {
                CreateItemDefinition(createdAssets, CoinItemId, "测试铜钱", ItemType.Currency, 999),
                CreateItemDefinition(createdAssets, PotionItemId, "测试药品", ItemType.Consumable, 99),
                CreateItemDefinition(createdAssets, ScrollItemId, "测试卷轴", ItemType.Material, 99),
                CreateItemDefinition(createdAssets, FillerItemId, "测试占位物", ItemType.Material, 1),
                CreateItemDefinition(createdAssets, GateItemId, "测试门槛物品", ItemType.KeyItem, 1)
            };
        }

        private static ItemDefinition CreateItemDefinition(
            List<ScriptableObject> createdAssets,
            string itemId,
            string displayName,
            ItemType itemType,
            int maxStackCount)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.ItemId = itemId;
            definition.DisplayName = displayName;
            definition.Description = displayName;
            definition.IconAddress = itemId;
            definition.ItemType = itemType;
            definition.Quality = ItemQuality.Common;
            definition.MaxStackCount = Math.Max(1, maxStackCount);
            definition.CanTrade = true;
            definition.CanSell = true;
            definition.CanMove = true;
            createdAssets?.Add(definition);
            return definition;
        }

        private static InventoryContainerConfig CreateMainContainer(List<ScriptableObject> createdAssets, int slotCount)
        {
            var config = ScriptableObject.CreateInstance<InventoryContainerConfig>();
            config.ContainerId = MainContainerId;
            config.DisplayName = "测试主背包";
            config.ContainerType = InventoryContainerType.Main;
            config.SlotCount = Math.Max(1, slotCount);
            config.AllowAutoStack = true;
            config.AllowManualMove = true;
            config.IsUnlockedByDefault = true;
            createdAssets?.Add(config);
            return config;
        }

        private static InventoryService CreateInventory(ItemDefinition[] definitions, InventoryContainerConfig mainContainer)
        {
            return new InventoryService(definitions, new[] { mainContainer });
        }

        private static ShopService CreateShopService(
            ShopAsset[] shops,
            ItemDefinition[] definitions,
            IInventoryService inventory)
        {
            return new ShopService(shops, inventory, definitions);
        }

        private static ShopAsset CreateShop(string shopId, params ShopProductData[] products)
        {
            var shop = ScriptableObject.CreateInstance<ShopAsset>();
            shop.ShopId = shopId;
            shop.DisplayName = shopId;
            shop.Description = shopId;
            shop.ShopType = ShopType.General;
            shop.DefaultUnlocked = true;
            shop.Products = products ?? Array.Empty<ShopProductData>();
            shop.OpenConditions = Array.Empty<ShopConditionData>();
            shop.DefaultDiscounts = Array.Empty<ShopDiscountData>();
            return shop;
        }

        private static ShopProductData CreateProduct(
            string productId,
            string itemId,
            int count = 1,
            int price = 0,
            int initialStock = -1,
            int maxPurchaseCount = -1,
            bool defaultUnlocked = true,
            bool oneShot = false,
            ShopConditionData[] buyConditions = null)
        {
            return new ShopProductData
            {
                ProductId = productId,
                ItemId = itemId,
                Count = Math.Max(1, count),
                InitialStock = initialStock,
                MaxPurchaseCount = maxPurchaseCount,
                DefaultUnlocked = defaultUnlocked,
                OneShot = oneShot,
                Prices = price > 0
                    ? new[] { new ShopPriceData { CurrencyItemId = CoinItemId, Amount = price } }
                    : Array.Empty<ShopPriceData>(),
                Tags = Array.Empty<string>(),
                BuyConditions = buyConditions ?? Array.Empty<ShopConditionData>()
            };
        }

        private static ShopDiscountData CreateDiscount(
            string discountId,
            float priceMultiplier,
            string[] productIds = null,
            string[] productTags = null)
        {
            return new ShopDiscountData
            {
                DiscountId = discountId,
                PriceMultiplier = priceMultiplier,
                ProductIds = productIds ?? Array.Empty<string>(),
                ProductTags = productTags ?? Array.Empty<string>(),
                Conditions = Array.Empty<ShopConditionData>()
            };
        }

        private static AddItemRequest CreateAddRequest(string itemId, int count)
        {
            return new AddItemRequest
            {
                ItemId = itemId,
                Count = count,
                TargetContainerId = MainContainerId,
                TargetSlotIndex = -1,
                AllowPartial = false,
                SourceModule = nameof(ShopBasicTestRunner)
            };
        }

        private void ExpectProductState(
            ShopService service,
            string shopId,
            string productId,
            int expectedRemainingStock,
            int expectedPurchasedCount,
            ShopProductState expectedState,
            List<string> failures)
        {
            if (!service.TryGetShopState(shopId, out var snapshot) || snapshot == null)
            {
                failures.Add($"找不到商店快照：{shopId}");
                return;
            }

            var product = FindProductSnapshot(snapshot, productId);
            if (product == null)
            {
                failures.Add($"找不到商品快照：{shopId}/{productId}");
                return;
            }

            ExpectEqual($"{productId} 剩余库存", expectedRemainingStock, product.RemainingStock, failures);
            ExpectEqual($"{productId} 已购次数", expectedPurchasedCount, product.PurchasedCount, failures);
            ExpectEqual($"{productId} 状态", expectedState, product.State, failures);
        }

        private static ShopProductProgressSnapshot FindProductSnapshot(ShopProgressSnapshot snapshot, string productId)
        {
            for (var i = 0; snapshot?.Products != null && i < snapshot.Products.Length; i++)
            {
                var product = snapshot.Products[i];
                if (product != null && string.Equals(product.ProductId, productId, StringComparison.Ordinal))
                {
                    return product;
                }
            }

            return null;
        }

        private static void ExpectInventorySuccess(
            string message,
            InventoryOperationResult result,
            List<string> failures)
        {
            if (result == null || !result.Succeeded)
            {
                failures.Add($"{message}：期望成功，实际失败。Reason={result?.Reason}, Message={result?.Message}");
            }
        }

        private static void ExpectShopSuccess(
            string message,
            ShopOperationResult result,
            List<string> failures)
        {
            if (result == null || !result.Succeeded)
            {
                failures.Add($"{message}：期望成功，实际失败。Reason={result?.PrimaryFailureReason}, Message={result?.Message}");
            }
        }

        private static void ExpectShopFailed(
            string message,
            ShopOperationResult result,
            ShopFailureReason expectedReason,
            List<string> failures)
        {
            if (result == null || result.Succeeded || !result.HasFailure(expectedReason))
            {
                failures.Add($"{message}：期望失败原因 {expectedReason}，实际 Result={(result == null ? "null" : result.PrimaryFailureReason.ToString())}。");
            }
        }

        private static void ExpectEqual<T>(string message, T expected, T actual, List<string> failures)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                failures.Add($"{message}：期望 {expected}，实际 {actual}。");
            }
        }

        private static void ExpectTrue(string message, bool condition, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        private void LogStep(string message)
        {
            if (verboseLog)
            {
                Debug.Log($"[NiumaShopTest] {message}", this);
            }
        }

        private static void ReleaseCreatedAssets(List<ScriptableObject> createdAssets)
        {
            for (var i = 0; createdAssets != null && i < createdAssets.Count; i++)
            {
                if (createdAssets[i] != null)
                {
                    DestroyImmediate(createdAssets[i]);
                }
            }
        }

        /// <summary>
        /// 用于模拟 UI 连点或业务回调重入。
        /// 在第一次扣货币时再次发起购买，验证 ShopService 的事务防重入保护是否生效。
        /// </summary>
        private sealed class ReentrantInventoryService : IInventoryService
        {
            private readonly IInventoryService _inner;
            private ShopService _shopService;
            private BuyProductRequest _request;
            private bool _triggered;

            public ReentrantInventoryService(IInventoryService inner)
            {
                _inner = inner;
            }

            public ShopOperationResult ReentrantResult { get; private set; }

            public int Revision => _inner.Revision;

            public void Bind(ShopService shopService, BuyProductRequest request)
            {
                _shopService = shopService;
                _request = request;
                _triggered = false;
                ReentrantResult = null;
            }

            public bool HasItem(string itemId, int count) => _inner.HasItem(itemId, count);

            public int GetItemCount(string itemId) => _inner.GetItemCount(itemId);

            public int GetItemCount(string itemId, string containerId) => _inner.GetItemCount(itemId, containerId);

            public bool TryGetItem(string instanceId, out InventoryItemSnapshot item) => _inner.TryGetItem(instanceId, out item);

            public bool TryGetContainerSnapshot(string containerId, out InventoryContainerSnapshot container) =>
                _inner.TryGetContainerSnapshot(containerId, out container);

            public void CopyContainerSnapshots(List<InventoryContainerSnapshot> output) => _inner.CopyContainerSnapshots(output);

            public void CopyItemSnapshots(List<InventoryItemSnapshot> output) => _inner.CopyItemSnapshots(output);

            public bool TryFindFirstEmptySlot(string containerId, out int slotIndex) =>
                _inner.TryFindFirstEmptySlot(containerId, out slotIndex);

            public InventoryOperationResult CanAddItem(AddItemRequest request) => _inner.CanAddItem(request);

            public InventoryOperationResult CanAddItemsBatch(InventoryAddBatchPreviewRequest request) =>
                _inner.CanAddItemsBatch(request);

            public InventoryOperationResult CanRemoveItem(RemoveItemRequest request) => _inner.CanRemoveItem(request);

            public InventoryOperationResult AddItem(AddItemRequest request) => _inner.AddItem(request);

            public InventoryOperationResult RemoveItem(RemoveItemRequest request)
            {
                if (!_triggered && _shopService != null)
                {
                    _triggered = true;
                    ReentrantResult = _shopService.BuyProduct(_request);
                }

                return _inner.RemoveItem(request);
            }

            public InventoryOperationResult MoveItem(MoveItemRequest request) => _inner.MoveItem(request);

            public InventoryOperationResult SplitStack(SplitStackRequest request) => _inner.SplitStack(request);

            public InventoryOperationResult MergeStack(MergeStackRequest request) => _inner.MergeStack(request);

            public InventoryOperationResult SortContainer(SortContainerRequest request) => _inner.SortContainer(request);

            public InventoryOperationResult UseItem(UseItemRequest request) => _inner.UseItem(request);

            public InventoryOperationResult LockItem(string instanceId) => _inner.LockItem(instanceId);

            public InventoryOperationResult UnlockItem(string instanceId) => _inner.UnlockItem(instanceId);

            public InventorySaveData ExportSnapshot() => _inner.ExportSnapshot();

            public void ImportSnapshot(InventorySaveData snapshot) => _inner.ImportSnapshot(snapshot);
        }
    }
}
