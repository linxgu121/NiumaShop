using System.Collections.Generic;
using NiumaInventory.Config;
using NiumaInventory.Service;
using NiumaShop.Config;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店服务配置能力接口。
    /// 控制器使用该接口替换商店配置、同步物品定义和注入外部依赖，避免直接耦合 ShopService 具体实现。
    /// </summary>
    public interface IShopConfigurationService
    {
        /// <summary>
        /// 最近一次配置注册表校验报告。
        /// </summary>
        ShopAssetValidationReport LastValidationReport { get; }

        /// <summary>
        /// 更新背包服务引用。
        /// </summary>
        void SetInventoryService(IInventoryService inventoryService);

        /// <summary>
        /// 更新外部条件解析器。
        /// </summary>
        void SetConditionResolver(IShopConditionResolver conditionResolver);

        /// <summary>
        /// 更新外部价格解析器。
        /// </summary>
        void SetPriceResolver(IShopPriceResolver priceResolver);

        /// <summary>
        /// 重建商店配置索引。
        /// </summary>
        ShopAssetValidationReport SetShops(
            IEnumerable<ShopAsset> shopAssets,
            IEnumerable<ItemDefinition> itemDefinitions = null,
            bool strictRegistryMode = false);

        /// <summary>
        /// 同步物品定义索引。
        /// </summary>
        ShopAssetValidationReport SetItemDefinitions(
            IEnumerable<ItemDefinition> itemDefinitions,
            bool revalidateShops = true);
    }
}
