using System;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店配置校验问题。
    /// 只保存定位和说明，不参与运行时交易逻辑。
    /// </summary>
    [Serializable]
    public sealed class ShopAssetValidationIssue
    {
        /// <summary>问题严重程度。</summary>
        public ShopAssetValidationSeverity Severity;

        /// <summary>商店 ID。商店本身没有 ShopId 时为空。</summary>
        public string ShopId;

        /// <summary>商品 ID。问题不属于具体商品时为空。</summary>
        public string ProductId;

        /// <summary>字段名或字段路径，例如 Products[0].Prices[0].Amount。</summary>
        public string FieldName;

        /// <summary>问题说明。</summary>
        public string Message;

        /// <summary>Unity 资源上下文，用于编辑器日志定位。</summary>
        public UnityEngine.Object Context;

        public static ShopAssetValidationIssue Create(
            ShopAssetValidationSeverity severity,
            string shopId,
            string productId,
            string fieldName,
            string message,
            UnityEngine.Object context)
        {
            return new ShopAssetValidationIssue
            {
                Severity = severity,
                ShopId = shopId,
                ProductId = productId,
                FieldName = fieldName,
                Message = message,
                Context = context
            };
        }
    }
}
