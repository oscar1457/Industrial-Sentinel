using System.Security.Cryptography;
using System.Text;

namespace IndustrialSentinel.Infrastructure.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 200_000;

    public static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return (hash, salt);
    }

    public static bool Verify(string password, byte[] salt, byte[] expectedHash)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return CryptographicOperations.FixedTimeEquals(hash, expectedHash);
    }
}
