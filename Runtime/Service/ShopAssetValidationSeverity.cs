namespace NiumaShop.Service
{
    /// <summary>
    /// 商店配置校验严重程度。
    /// </summary>
    public enum ShopAssetValidationSeverity
    {
        /// <summary>提示信息，不影响注册。</summary>
        Info = 0,

        /// <summary>警告信息，配置可以进入运行时，但需要策划确认。</summary>
        Warning = 1,

        /// <summary>错误信息，配置不应进入正式流程。</summary>
        Error = 2
    }
}
