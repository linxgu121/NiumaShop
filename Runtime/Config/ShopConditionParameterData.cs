using System;

namespace NiumaShop.Config
{
    /// <summary>
    /// 商店条件扩展参数。
    /// 使用数组 DTO 而不是 Dictionary，避免 Unity JsonUtility 序列化时丢字段。
    /// </summary>
    [Serializable]
    public sealed class ShopConditionParameterData
    {
        /// <summary>
        /// 参数键。建议使用小写下划线命名，例如 clan_id、festival_id。
        /// </summary>
        public string Key;

        /// <summary>
        /// 参数值。由对应的外部条件解析器解释。
        /// </summary>
        public string Value;
    }
}
