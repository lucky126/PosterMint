namespace PosterMint.Domain.Enums;

/// <summary>商铺登录尝试的结果分类，一次一条记录写入 ShopLoginLog。</summary>
public enum ShopLoginResult
{
    /// <summary>成功。</summary>
    Success = 1,
    /// <summary>用户名不存在。</summary>
    UserNotFound = 2,
    /// <summary>密码不匹配。</summary>
    BadPassword = 3,
    /// <summary>账号被禁用（Shop.Status != Active）。</summary>
    Disabled = 4,
    /// <summary>用户未设置密码（初始状态）。</summary>
    NoPasswordSet = 5
}
