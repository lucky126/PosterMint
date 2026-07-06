using System.Security.Cryptography;
using PosterMint.Application.Shops;

namespace PosterMint.Infrastructure.Shops;

/// <summary>
/// PBKDF2-SHA256 密码哈希器。v1 参数：16 字节盐 + 32 字节 hash + 210,000 次迭代（OWASP 2024 建议下限）。
/// 迭代次数写死进版本号，将来提升迭代次数或换算法就升 CurrentVersion。
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int V1Iterations = 210_000;

    public int CurrentVersion => 1;

    public (string hash, string salt) Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password must not be empty.", nameof(password));
        }

        var saltBytes = RandomNumberGenerator.GetBytes(SaltBytes);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            V1Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string password, string storedHash, string storedSalt, int version)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
        {
            return false;
        }

        var iterations = version switch
        {
            1 => V1Iterations,
            _ => 0
        };
        if (iterations == 0) return false;

        byte[] saltBytes;
        byte[] expectedHash;
        try
        {
            saltBytes = Convert.FromBase64String(storedSalt);
            expectedHash = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        // 固定时间比较，防时序侧信道
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
