using System;
using System.Security.Cryptography;
using BCrypt.Net;

namespace BlackoutGuard.Infrastructure.Security;

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // အကောင့်အသစ်များအတွက် PBKDF2 ဖြင့် Hash လုပ်မည် (သို့မဟုတ် BCrypt သုံးလိုပါက ပြောင်းလဲနိုင်သည်)
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    // PBKDF2 နှင့် Bcrypt Hash Format နှစ်မျိုးလုံးကို စစ်ဆေးပေးသည့် Verify logic
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        // 1. Admin Account ၏ PBKDF2 Format ($pbkdf2$...) ဖြစ်နေပါက
        if (storedHash.StartsWith("pbkdf2$", StringComparison.OrdinalIgnoreCase))
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2")
                return false;

            if (!int.TryParse(parts[1], out var iterations))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        // 2. Normal User များ၏ Bcrypt Format ($2a$, $2b$, $2y$) ဖြစ်နေပါက
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch
        {
            return false;
        }
    }
}