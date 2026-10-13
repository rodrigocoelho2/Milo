using System.Security.Cryptography;

namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// PBKDF2-HMAC-SHA512 password hashes, stored as "pbkdf2-sha512$iterations$salt$hash".
/// </summary>
internal static class PasswordHasher
{
    /// <summary>Shortest accepted password.</summary>
    public const int MinPasswordLength = 8;

    /// <summary>Longest accepted password.</summary>
    public const int MaxPasswordLength = 128;

    private const string Prefix = "pbkdf2-sha512";
    private const int Iterations = 210_000;   // OWASP recommendation for PBKDF2-HMAC-SHA512
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private static readonly Lazy<string> DummyHash = new(() => Hash(Guid.NewGuid().ToString("N")));

    /// <summary>
    /// Hashes a password with a new random salt.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The stored form.</returns>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Checks a password against a stored hash in constant time.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="stored">The stored form.</param>
    /// <returns>True when it matches.</returns>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Spends the same time as a real check, so unknown usernames can't be told apart by response time.
    /// </summary>
    /// <param name="password">The password that was sent.</param>
    public static void VerifyAgainstDummy(string password) => Verify(password, DummyHash.Value);

    /// <summary>
    /// A readable temporary password for admin resets (no 0/O/1/l/I).
    /// </summary>
    /// <param name="length">The length.</param>
    /// <returns>The password.</returns>
    public static string GenerateTemporary(int length = 12)
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }

    /// <summary>
    /// A new security stamp; storing it signs the account out everywhere else.
    /// </summary>
    /// <returns>The stamp.</returns>
    public static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
