using System.Security.Cryptography;

namespace BonyadRazi.Portal.Api.Security;

public sealed class Pbkdf2PasswordHasher
{
    public const int DefaultIterations = 100_000;
    public const int SaltSize = 16;  // 128-bit
    public const int HashSize = 32;  // 256-bit

    public (byte[] hash, byte[] salt, int iterations) Hash(string password, int iterations = DefaultIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return (hash, salt, iterations);
    }

    public bool Verify(string password, byte[] salt, int iterations, byte[] expectedHash)
    {
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }
}