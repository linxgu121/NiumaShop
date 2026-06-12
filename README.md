# NiumaShop

## 模块定位
NiumaShop 是商城/商店模块，负责商店配置、商品上下架、价格、折扣、库存、限购、购买事务、存档和商店 UI 数据。

## 框架设计思路
- ShopAsset 描述商店和商品，ShopRuntimeState 保存库存、限购、折扣运行态。
- 交易走事务流程：先校验货币、库存、限购、背包容量，再扣货币、给商品。
- OneShot 表示商品条目售罄，与 InitialStock 无关。
- Revision 使用 long，不回绕，避免长期运行和存档脏标记碰撞。

## 核心流程
1. ShopController 注入 InventoryService、商品配置和物品定义。
2. BuildShopViewData 输出商品显示、价格、库存、折扣和不可购买原因。
3. BuyProduct 校验商店状态、商品可见性、库存限购、价格与背包容量。
4. 扣除货币后添加商品。
5. 成功后写入库存、购买次数和 Revision。
6. SaveAdapter 导出各商店 RuntimeState。

## 模块用法
- ShopId、ProductId 必须稳定。
- Hidden 商品不输出给 UI，Locked/OutOfStock 商品可输出置灰。
- 折扣通过 DiscountId 控制，可禁用、指定启用或恢复默认。

## 场景使用方法
推荐放置方式：`ShopRoot` 一个商店根物体承载商店服务、UI 桥接和存档；每个商人/柜台是独立交互物。

- `ShopRoot`：挂 `NiumaShopController`，绑定 ShopAsset、ItemDefinition、InventoryService、ConditionResolver、PriceResolver。
- `ShopRoot/SaveAdapter` 或全局 `SaveRoot/Providers`：挂 `NiumaShopSaveAdapter`。
- `ShopRoot/UIBridge` 或 `UIRoot/Bridges`：挂 `ShopUIViewBridge`，绑定商店 UI Receiver。
- `ShopRoot/Debug`：开发阶段挂 `ShopBasicTestRunner`。
- `NPC_Merchant_xxx` 或 `ShopCounter_xxx`：挂 `ShopInteractable`，填写 ShopId 和提示文本。
- `UIRoot/ShopPanel`：放商品列表、价格、库存、折扣、购买按钮和失败原因提示。
- 一个场景多个商店时，不要复制 ShopController；复制 ShopInteractable 并填不同 ShopId。

## 协作边界
Shop 不管理背包内部格子，不决定装备属性，也不做合成。它只通过 InventoryService 扣货币和发商品。

## 场景挂载与 Inspector 配置
### NiumaShopController
建议挂载位置：`CoreScene/BootstrapRoot/GameplayServicesRoot/ShopRoot`。

用途：管理商店配置、商品状态、库存、限购、折扣、购买事务。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Shop Assets` | 拖所有商店配置资产 | 不建议 | 没有商店可打开 |
| `Item Definitions` | 拖背包物品定义 | 不建议 | 商品 UI 无法显示物品表现数据 |
| `Inventory Controller` | 拖 `NiumaInventoryController` | 不建议 | 无法扣货币或发商品 |
| `Condition Resolver Provider` | 有解锁/声望/剧情条件时拖解析脚本 | 可以 | 留空时只支持基础状态 |
| `Price Resolver Provider` | 有动态价格时拖价格脚本 | 可以 | 留空时使用配置价格和折扣 |
| `Register Service To Context` | 核心场景开启 | 可以关闭 | 其他模块无法获取商店服务 |

### NiumaShopSaveAdapter
建议挂载位置：`CoreScene/BootstrapRoot/SaveRoot/SaveAdapters`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Shop Controller` | 拖 `NiumaShopController` | 不建议 | 库存、限购、折扣状态不存档 |
| `Save Controller` | 拖 `NiumaSaveController` | 不建议 | 无法注册存档 Provider |

### ShopUIViewBridge
建议挂载位置：商店 UI 面板。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Shop Controller` | 拖 `NiumaShopController` | 不建议 | UI 不刷新 |
| `Receiver Provider` | 拖商店 UI 接收脚本 | 不可以 | 商品列表无处显示 |
| `Current Shop Id` | 填默认打开商店 ID | 可以 | 留空时需要外部调用 OpenShop |

### ShopToolkitReceiver
建议挂载位置：`CoreScene/BootstrapRoot/UIRoot/UIBridges/ShopToolkitReceiver`。

用途：UI Toolkit 商店面板接收端。把 `ShopUIViewBridge` 生成的 `ShopUIUpdate` 推给 `UIToolkitUIManager` 的 `ShopPanel` View。

推荐绑定：

1. 在 `UIRoot/UIBridges` 下创建 `ShopToolkitReceiver` 物体。
2. 挂 `ShopToolkitReceiver`。
3. `UI Manager` 拖 `UIRoot/UIManager` 上的 `UIToolkitUIManager`。
4. `Shop View Id` 默认 `ShopPanel`，要和 `UIToolkitViewRegistrySO` 里的 ViewId 一致。
5. 把 `ShopToolkitReceiver` 拖到 `ShopUIViewBridge.Shop UI Receiver Provider`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `UI Manager` | 拖 `UIToolkitUIManager` | 不建议 | 会尝试自动查找，失败则商店 Toolkit UI 不刷新 |
| `Shop View Id` | 填 `ShopPanel` 或你注册的商店 ViewId | 不建议 | ViewId 不匹配时窗口打不开 |
| `Auto Open View` | 打开商店时建议开启 | 可以 | 关闭后需要外部先打开 `ShopPanel` |
| `Close On Cleared` | 建议开启 | 可以 | 当前商店被清空时窗口不会自动关闭 |

### ShopInteractable
建议挂载位置：商店 NPC 或柜台物体。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Shop Id` | 填商店配置中的 ShopId | 不可以 | 无法打开商店 |
| `Shop UI Bridge` | 拖商店 UI 桥接脚本 | 不建议 | 交互后不知道打开哪个 UI |
| `Require Unlocked Shop` | 未解锁商店不可打开时开启 | 可以 | 关闭后只要交互就能打开 |



### ShopToolkitBindingProvider
建议挂载位置：CoreScene/BootstrapRoot/UIRoot/UIToolkitRoot/BindingProviders/ShopBindingProvider。

用途：把 ShopToolkitReceiver 推给 ShopPanel 的 ShopUIUpdate 渲染到 UXML。没有它，商店窗口会打开但商品列表为空。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| Provider Id | 默认 ShopPanel，与 Registry 的 BindingProviderId 一致 | 不建议 | 不匹配时回退空 Binding |
| List Root Name | 商品列表容器，默认 ListRoot | 可以 | 不显示商品列表 |
| Detail Label Name | 选中商品详情，默认 DetailText | 可以 | 不显示商品详情 |
| Result Label Name | 购买结果，默认 ResultText | 可以 | 不显示购买反馈 |

UXML 至少建议包含：TitleText、StatusText、ListRoot、DetailText、ResultText、EmptyRoot。
