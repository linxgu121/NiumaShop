using System;
using System.Collections.Generic;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店配置校验报告。
    /// 注册表重建时生成，用于控制器、调试菜单和编辑器工具查看配置质量。
    /// </summary>
    [Serializable]
    public sealed class ShopAssetValidationReport
    {
        private readonly List<ShopAssetValidationIssue> _issues = new List<ShopAssetValidationIssue>();

        /// <summary>校验问题数量。</summary>
        public int IssueCount => _issues.Count;

        /// <summary>是否存在错误级问题。</summary>
        public bool HasErrors { get; private set; }

        /// <summary>是否存在警告级问题。</summary>
        public bool HasWarnings { get; private set; }

        /// <summary>是否出现重复 ShopId。</summary>
        public bool HasDuplicateShopIds { get; private set; }

        /// <summary>
        /// 校验是否通过。
        /// 只要不存在错误级问题，就认为可进入运行时流程；警告仍需在调试阶段确认。
        /// </summary>
        public bool IsValid => !HasErrors;

        /// <summary>
        /// 添加一个校验问题。
        /// </summary>
        public void Add(ShopAssetValidationIssue issue, bool isDuplicateShopId = false)
        {
            if (issue == null)
            {
                return;
            }

            _issues.Add(issue);
            if (issue.Severity == ShopAssetValidationSeverity.Error)
            {
                HasErrors = true;
            }
            else if (issue.Severity == ShopAssetValidationSeverity.Warning)
            {
                HasWarnings = true;
            }

            if (isDuplicateShopId)
            {
                HasDuplicateShopIds = true;
            }
        }

        /// <summary>
        /// 添加错误问题。
        /// </summary>
        public void AddError(
            string shopId,
            string productId,
            string fieldName,
            string message,
            UnityEngine.Object context,
            bool isDuplicateShopId = false)
        {
            Add(ShopAssetValidationIssue.Create(
                    ShopAssetValidationSeverity.Error,
                    shopId,
                    productId,
                    fieldName,
                    message,
                    context),
                isDuplicateShopId);
        }

        /// <summary>
        /// 添加警告问题。
        /// </summary>
        public void AddWarning(
            string shopId,
            string productId,
            string fieldName,
            string message,
            UnityEngine.Object context)
        {
            Add(ShopAssetValidationIssue.Create(
                ShopAssetValidationSeverity.Warning,
                shopId,
                productId,
                fieldName,
                message,
                context));
        }

        /// <summary>
        /// 获取所有问题副本，避免外部修改内部列表。
        /// </summary>
        public ShopAssetValidationIssue[] GetIssues()
        {
            return _issues.ToArray();
        }
    }
}
