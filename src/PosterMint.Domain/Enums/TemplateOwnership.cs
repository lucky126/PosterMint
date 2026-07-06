namespace PosterMint.Domain.Enums;

/// <summary>
/// 模板归属：Shop = 商铺专属（如华为/海底捞的定制模板）；
/// Category = 行业通用（如餐饮/化妆品），本行业所有商铺可用。
/// v2 没有 Public（未指定 = 系统默认按 Category 兜底）。
/// </summary>
public enum TemplateOwnership
{
    Shop = 1,
    Category = 2
}
