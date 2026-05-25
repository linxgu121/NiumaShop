using NiumaShop.Config;
using NiumaShop.Enum;
using NiumaShop.Request;

namespace NiumaShop.Service
{
    /// <summary>
    /// 商店外部条件解析接口。
    /// ShopService 只直接处理通用条件，任务、剧情、声望、活动时间等条件交给外部解析器。
    /// </summary>
    public interface IShopConditionResolver
    {
        /// <summary>
        /// 尝试判断一个外部条件是否满足。
        /// 返回 false 表示 Resolver 不认识该条件或无法判断；isMet 表示条件裁决结果。
        /// </summary>
        bool TryEvaluate(
            ShopConditionData condition,
            in BuyProductRequest request,
            out bool isMet,
            out ShopFailureReason failureReason,
            out string message);
    }
}
