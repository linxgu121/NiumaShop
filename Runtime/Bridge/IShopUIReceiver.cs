namespace NiumaShop.Bridge
{
    /// <summary>
    /// 商店 UI 接收接口。
    /// 由具体 UI 组件实现；桥接层只负责把整理好的表现数据交给它，不直接操作按钮、面板或预制体。
    /// </summary>
    public interface IShopUIReceiver
    {
        /// <summary>
        /// 应用商店 UI 更新。
        /// update 中包含当前商店面板、选中商品和可选交易结果。
        /// </summary>
        void ApplyShopUpdate(ShopUIUpdate update);
    }
}
