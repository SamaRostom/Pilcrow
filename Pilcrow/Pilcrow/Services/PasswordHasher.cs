using System.Security.Cryptography;

namespace Pilcrow.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16, KeySize = 32, Iterations = 210_000;
    private static readonly HashAlgorithmName Algo = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key  = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algo, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.', 3);
        if (parts.Length != 3) return false;

        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var key  = Convert.FromBase64String(parts[2]);

        var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algo, key.Length);
        return CryptographicOperations.FixedTimeEquals(attempt, key);
    }
}