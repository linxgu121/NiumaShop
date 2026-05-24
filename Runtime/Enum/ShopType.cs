namespace NiumaShop.Enum
{
    /// <summary>
    /// 商店类型。
    /// 第一版主要用于 UI 分类、配置校验和调试，不直接决定交易逻辑。
    /// </summary>
    public enum ShopType
    {
        /// <summary>
        /// 未定义类型。默认值必须视为无效。
        /// </summary>
        None = 0,

        /// <summary>
        /// 通用杂货商店。
        /// </summary>
        General = 10,

        /// <summary>
        /// 材料商店。
        /// </summary>
        Material = 20,

        /// <summary>
        /// 药品或消耗品商店。
        /// </summary>
        Consumable = 30,

        /// <summary>
        /// 装备相关商店。装备状态仍由 NiumaEquipment 管理。
        /// </summary>
        Equipment = 40,

        /// <summary>
        /// 剧情或氏族文化相关特殊商店。
        /// </summary>
        Story = 50,

        /// <summary>
        /// 活动或限时商店。第一版只预留类型，不实现时间刷新。
        /// </summary>
        Event = 60
    }
}
