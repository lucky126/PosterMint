namespace PosterMint.Application.Shops;

/// <summary>
/// 创建/更新商户请求。
///
/// 密码字段规则：
/// - 创建：Password 可以留空表示"暂不设账号"，也可以直接指定；Username 若留空则不建凭证。
/// - 更新：Username 传空字符串 = 清空账号；null 或未提供 = 保持不变。
///         Password 传空字符串 = 保持不变（不清密码，避免误操作）；非空 = 更新。
/// 服务端只用 IPasswordHasher 存 Hash，绝不落库明文。
/// </summary>
public sealed class ShopUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string Industry { get; set; } = "餐饮";
    public string Status { get; set; } = "Active";
    public string? Remark { get; set; }

    // 登录字段（v2 新增）
    public string? Username { get; set; }
    public string? Password { get; set; }
}
