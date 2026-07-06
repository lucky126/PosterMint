namespace PosterMint.Application.Shops;

/// <summary>
/// 密码哈希器抽象。当前 PBKDF2-SHA256 实现是 Version=1，将来升级 Argon2 是 Version=2 时，
/// 用户下次登录成功即透明升级。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>当前算法版本号。</summary>
    int CurrentVersion { get; }

    /// <summary>把明文密码哈希成 (hash, salt) 对，均为 Base64。</summary>
    (string hash, string salt) Hash(string password);

    /// <summary>校验明文密码是否匹配存储的哈希。version 是当年生成 hash 时的算法版本。</summary>
    bool Verify(string password, string storedHash, string storedSalt, int version);
}
